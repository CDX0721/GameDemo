"""
Wood Texture Generator — GUI with live preview.
Requires: Pillow, numpy (same deps as the CLI script).
"""

import tkinter as tk
from tkinter import ttk, filedialog, messagebox, colorchooser
from PIL import Image, ImageTk
import random

import wood_texture_generator as wtg

ZOOM_LEVELS = [0.125, 0.25, 0.5, 1.0, 2.0, 4.0, 8.0, 16.0]


class WoodTextureGUI:
    def __init__(self, root):
        self.root = root
        root.title("Wood Texture Generator")
        root.resizable(True, True)
        root.minsize(900, 550)

        style = ttk.Style()
        style.theme_use("clam")

        # ── State ──
        self.preview_img = None
        self.preview_tk = None
        self._update_id = None
        self._zoom_level = 0  # 0 = fit to canvas
        self._pan_x = 0
        self._pan_y = 0
        self._drag_start = None
        self._bg_color = "#2b2b2b"  # preview background; "checker" for checkerboard

        # ── Main layout ──
        self.paned = ttk.PanedWindow(root, orient=tk.HORIZONTAL)
        self.paned.pack(fill=tk.BOTH, expand=True, padx=6, pady=6)

        self.left_frame = ttk.Frame(self.paned, width=320)
        self.right_frame = ttk.Frame(self.paned)
        self.paned.add(self.left_frame, weight=0)
        self.paned.add(self.right_frame, weight=1)

        self._build_controls(self.left_frame)
        self._build_preview(self.right_frame)

        root.bind("<Configure>", self._on_resize)
        self._schedule_update()

    # ═══════════════════════════════════════════════════════════════
    #  Helper: slider + spinbox row
    # ═══════════════════════════════════════════════════════════════

    def _slider_row(self, parent, label, var, from_, to, *,
                    step=None, unit="", width=6, pad=True):
        """Create a row: label | slider | spinbox, all bound to `var`."""
        row = ttk.Frame(parent)
        row.pack(fill=tk.X, pady=(2 if pad else 0))
        ttk.Label(row, text=label + ":", width=14, anchor="e").pack(side=tk.LEFT, padx=(0, 4))

        s = ttk.Scale(row, from_=from_, to=to, variable=var, command=self._mark_dirty)
        s.pack(side=tk.LEFT, fill=tk.X, expand=True, padx=2)

        if isinstance(var, tk.IntVar):
            sb = ttk.Spinbox(row, from_=from_, to=to, increment=step or 1,
                             textvariable=var, width=width, command=self._mark_dirty)
        else:
            sb = ttk.Spinbox(row, from_=from_, to=to, increment=step or 0.001,
                             textvariable=var, width=width, command=self._mark_dirty,
                             format="%.3f")
        sb.pack(side=tk.RIGHT)
        # Trigger update on Enter or focus-out so typed values take effect immediately
        sb.bind("<Return>", lambda e: self._mark_dirty())
        sb.bind("<FocusOut>", lambda e: self._mark_dirty())
        ttk.Label(row, text=unit, width=3, anchor="w").pack(side=tk.RIGHT, padx=(2, 0))
        return row

    # ═══════════════════════════════════════════════════════════════
    #  Control panel
    # ═══════════════════════════════════════════════════════════════

    def _build_controls(self, parent):
        inner_canvas = tk.Canvas(parent, width=310, highlightthickness=0)
        scrollbar = ttk.Scrollbar(parent, orient=tk.VERTICAL, command=inner_canvas.yview)
        self.control_inner = ttk.Frame(inner_canvas)

        self.control_inner.bind(
            "<Configure>",
            lambda e: inner_canvas.configure(scrollregion=inner_canvas.bbox("all"))
        )
        inner_canvas.create_window((0, 0), window=self.control_inner, anchor="nw")
        inner_canvas.configure(yscrollcommand=scrollbar.set)

        inner_canvas.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

        def _on_mousewheel(event):
            inner_canvas.yview_scroll(-1 * (event.delta // 120), "units")
        inner_canvas.bind("<Enter>", lambda e: inner_canvas.bind_all("<MouseWheel>", _on_mousewheel))
        inner_canvas.bind("<Leave>", lambda e: inner_canvas.unbind_all("<MouseWheel>"))

        cf = self.control_inner

        # —— Size ——
        ttk.Label(cf, text="Size", font=("", 10, "bold")).pack(anchor="w", pady=(8, 2))
        self.var_width = tk.IntVar(value=256)
        self._slider_row(cf, "Width", self.var_width, 8, 2048, step=8, unit="px")
        self.var_height = tk.IntVar(value=256)
        self._slider_row(cf, "Height", self.var_height, 8, 2048, step=8, unit="px")

        # —— Wood type ——
        ttk.Label(cf, text="Wood Type", font=("", 10, "bold")).pack(anchor="w", pady=(10, 2))
        self.var_wood = tk.StringVar(value="oak")
        combo = ttk.Combobox(cf, textvariable=self.var_wood,
                             values=list(wtg.WOOD_TYPES.keys()),
                             state="readonly", width=28)
        combo.pack(fill=tk.X, pady=2)
        combo.bind("<<ComboboxSelected>>", self._mark_dirty)

        # —— Element type ——
        ttk.Label(cf, text="Element Type", font=("", 10, "bold")).pack(anchor="w", pady=(10, 2))
        self.var_element = tk.StringVar(value="background")
        combo2 = ttk.Combobox(cf, textvariable=self.var_element,
                              values=["background", "button", "panel", "frame"],
                              state="readonly", width=28)
        combo2.pack(fill=tk.X, pady=2)
        combo2.bind("<<ComboboxSelected>>", self._mark_dirty)

        # —— Angle ——
        ttk.Label(cf, text="Angle", font=("", 10, "bold")).pack(anchor="w", pady=(10, 2))
        self.var_angle = tk.DoubleVar(value=0.0)
        self._slider_row(cf, "Angle", self.var_angle, -45, 45, step=0.5, unit="°", width=5)

        # —— Curvature ——
        ttk.Label(cf, text="Ring Curvature", font=("", 10, "bold")).pack(anchor="w", pady=(10, 2))
        self.var_curve = tk.DoubleVar(value=0.0)
        self._slider_row(cf, "Curvature", self.var_curve, 0.0, 0.05, step=0.001, unit="", width=5)

        # —— Grain amount ——
        ttk.Label(cf, text="Grain Amount", font=("", 10, "bold")).pack(anchor="w", pady=(10, 2))
        self.var_grain = tk.DoubleVar(value=1.0)
        self._slider_row(cf, "Grain", self.var_grain, 0.0, 1.0, step=0.05, unit="", width=4)

        # —— Corner radius ——
        ttk.Label(cf, text="Corner Radius", font=("", 10, "bold")).pack(anchor="w", pady=(10, 2))
        self.var_corner = tk.IntVar(value=0)
        self._slider_row(cf, "Radius", self.var_corner, 0, 80, step=1, unit="px")

        # —— Shadow ——
        ttk.Label(cf, text="Drop Shadow", font=("", 10, "bold")).pack(anchor="w", pady=(10, 2))
        self.var_shadow_enable = tk.BooleanVar(value=False)
        ttk.Checkbutton(cf, text="Enable", variable=self.var_shadow_enable,
                        command=self._mark_dirty).pack(anchor="w")

        self.var_shadow_x = tk.IntVar(value=6)
        self._slider_row(cf, "Offset X", self.var_shadow_x, -20, 20, step=1, unit="px")
        self.var_shadow_y = tk.IntVar(value=6)
        self._slider_row(cf, "Offset Y", self.var_shadow_y, -20, 20, step=1, unit="px")
        self.var_shadow_blur = tk.IntVar(value=8)
        self._slider_row(cf, "Blur", self.var_shadow_blur, 0, 30, step=1, unit="px")
        self.var_shadow_opacity = tk.IntVar(value=120)
        self._slider_row(cf, "Opacity", self.var_shadow_opacity, 0, 255, step=1, unit="")

        # —— Border ——
        ttk.Label(cf, text="Border", font=("", 10, "bold")).pack(anchor="w", pady=(10, 2))
        self.var_border_width = tk.IntVar(value=0)
        self._slider_row(cf, "Width", self.var_border_width, 0, 20, step=1, unit="px")
        self.var_border_inset = tk.IntVar(value=0)
        self._slider_row(cf, "Inset", self.var_border_inset, 0, 20, step=1, unit="px")

        row = ttk.Frame(cf)
        row.pack(fill=tk.X, pady=2)
        ttk.Label(row, text="Style:", width=14, anchor="e").pack(side=tk.LEFT, padx=(0, 4))
        self.var_border_style = tk.StringVar(value="flat")
        combo_bs = ttk.Combobox(row, textvariable=self.var_border_style,
                                values=["flat", "raised", "sunken", "groove", "ridge"],
                                state="readonly", width=12)
        combo_bs.pack(side=tk.LEFT, fill=tk.X, expand=True)
        combo_bs.bind("<<ComboboxSelected>>", self._mark_dirty)

        # Border color: swatch + picker button
        row2 = ttk.Frame(cf)
        row2.pack(fill=tk.X, pady=2)
        ttk.Label(row2, text="Color:", width=14, anchor="e").pack(side=tk.LEFT, padx=(0, 4))
        self._border_color = (60, 40, 20)
        hex_init = f"#{self._border_color[0]:02x}{self._border_color[1]:02x}{self._border_color[2]:02x}"
        self._border_swatch = tk.Canvas(row2, width=28, height=22, bg=hex_init,
                                        highlightthickness=1, highlightbackground="#888")
        self._border_swatch.pack(side=tk.LEFT, padx=4)
        ttk.Button(row2, text="Pick Color…", width=10,
                   command=self._pick_border_color).pack(side=tk.LEFT)

        # —— Custom mode toggle ——
        self.var_custom_mode = tk.BooleanVar(value=False)
        ttk.Checkbutton(cf, text="Custom per-side widths / per-corner radii",
                        variable=self.var_custom_mode,
                        command=self._toggle_custom_mode).pack(anchor="w", pady=(6, 0))
        self._custom_frame = ttk.Frame(cf)

        ttk.Label(self._custom_frame, text="Side Widths (T,R,B,L):",
                  font=("", 9)).pack(anchor="w", pady=(4, 0))
        row_cw = ttk.Frame(self._custom_frame)
        row_cw.pack(fill=tk.X)
        self.var_cw_top = tk.IntVar(value=4)
        self.var_cw_right = tk.IntVar(value=4)
        self.var_cw_bottom = tk.IntVar(value=4)
        self.var_cw_left = tk.IntVar(value=4)
        for label, var in [("T", self.var_cw_top), ("R", self.var_cw_right),
                            ("B", self.var_cw_bottom), ("L", self.var_cw_left)]:
            ttk.Label(row_cw, text=label, font=("", 8)).pack(side=tk.LEFT, padx=(4, 0))
            sb = ttk.Spinbox(row_cw, from_=0, to=40, increment=1, textvariable=var,
                             width=4, command=self._mark_dirty)
            sb.pack(side=tk.LEFT)
            sb.bind("<Return>", lambda e: self._mark_dirty())
            sb.bind("<FocusOut>", lambda e: self._mark_dirty())

        ttk.Label(self._custom_frame, text="Corner Radii (TL,TR,BL,BR):",
                  font=("", 9)).pack(anchor="w", pady=(6, 0))
        row_cr = ttk.Frame(self._custom_frame)
        row_cr.pack(fill=tk.X)
        self.var_cr_tl = tk.IntVar(value=8)
        self.var_cr_tr = tk.IntVar(value=8)
        self.var_cr_bl = tk.IntVar(value=8)
        self.var_cr_br = tk.IntVar(value=8)
        for label, var in [("TL", self.var_cr_tl), ("TR", self.var_cr_tr),
                            ("BL", self.var_cr_bl), ("BR", self.var_cr_br)]:
            ttk.Label(row_cr, text=label, font=("", 8)).pack(side=tk.LEFT, padx=(4, 0))
            sb = ttk.Spinbox(row_cr, from_=0, to=80, increment=1, textvariable=var,
                             width=4, command=self._mark_dirty)
            sb.pack(side=tk.LEFT)
            sb.bind("<Return>", lambda e: self._mark_dirty())
            sb.bind("<FocusOut>", lambda e: self._mark_dirty())

        # —— Seed ——
        ttk.Label(cf, text="Seed", font=("", 10, "bold")).pack(anchor="w", pady=(10, 2))
        row = ttk.Frame(cf)
        row.pack(fill=tk.X, pady=2)
        self.var_seed = tk.IntVar(value=random.randint(0, 999999))
        seed_entry = ttk.Entry(row, textvariable=self.var_seed, width=20)
        seed_entry.pack(side=tk.LEFT, fill=tk.X, expand=True)
        seed_entry.bind("<Return>", lambda e: self._mark_dirty())
        seed_entry.bind("<FocusOut>", lambda e: self._mark_dirty())
        ttk.Button(row, text="🎲", width=3, command=self._random_seed).pack(side=tk.RIGHT, padx=2)
        self.var_seed.trace_add("write", self._mark_dirty)

        # —— Buttons ——
        ttk.Button(cf, text="Save PNG…", command=self._save).pack(fill=tk.X, pady=(14, 2))
        ttk.Button(cf, text="Batch Export All Types…", command=self._batch).pack(fill=tk.X, pady=2)

    def _random_seed(self):
        self.var_seed.set(random.randint(0, 999999))

    def _toggle_custom_mode(self):
        if self.var_custom_mode.get():
            self._custom_frame.pack(fill=tk.X, pady=2)
        else:
            self._custom_frame.pack_forget()
        self._mark_dirty()

    def _pick_border_color(self):
        hex_init = f"#{self._border_color[0]:02x}{self._border_color[1]:02x}{self._border_color[2]:02x}"
        result = colorchooser.askcolor(color=hex_init, title="Border Color",
                                       parent=self.root)
        if result[0] is not None:
            self._border_color = tuple(int(v) for v in result[0])
            hex_new = f"#{self._border_color[0]:02x}{self._border_color[1]:02x}{self._border_color[2]:02x}"
            self._border_swatch.configure(bg=hex_new)
            self._mark_dirty()

    # ═══════════════════════════════════════════════════════════════
    #  Preview
    # ═══════════════════════════════════════════════════════════════

    def _build_preview(self, parent):
        # Zoom toolbar
        toolbar = ttk.Frame(parent)
        toolbar.pack(fill=tk.X, side=tk.TOP)

        ttk.Button(toolbar, text="⊖ Fit", width=5,
                   command=self._zoom_fit).pack(side=tk.LEFT, padx=2)
        ttk.Button(toolbar, text="−", width=3,
                   command=self._zoom_out).pack(side=tk.LEFT)
        self._zoom_label = ttk.Label(toolbar, text="Fit", width=8, anchor="center")
        self._zoom_label.pack(side=tk.LEFT, padx=2)
        ttk.Button(toolbar, text="+", width=3,
                   command=self._zoom_in).pack(side=tk.LEFT)
        ttk.Button(toolbar, text="1:1", width=4,
                   command=self._zoom_100).pack(side=tk.LEFT, padx=2)

        ttk.Separator(toolbar, orient=tk.VERTICAL).pack(side=tk.LEFT, padx=6, fill=tk.Y)

        # Background color swatches
        ttk.Label(toolbar, text="BG:").pack(side=tk.LEFT, padx=2)
        self._bg_canvas = tk.Canvas  # type alias for linting
        BG_PRESETS = [
            ("#2b2b2b", "Dark"),
            ("#000000", "Black"),
            ("#ffffff", "White"),
            ("#4a6741", "Green"),
            ("#3a5f8a", "Blue"),
            ("checker", "Grid"),
        ]
        for color, tooltip in BG_PRESETS:
            if color == "checker":
                btn = tk.Canvas(toolbar, width=20, height=20, highlightthickness=1,
                                highlightbackground="#555")
                # Draw mini checkerboard
                for yy in range(4):
                    for xx in range(4):
                        c = "#666" if (xx + yy) % 2 == 0 else "#999"
                        btn.create_rectangle(xx*5, yy*5, xx*5+5, yy*5+5, fill=c, outline="")
                btn.pack(side=tk.LEFT, padx=1)
                btn.bind("<Button-1>", lambda e, c=color: self._set_bg(c))
            else:
                btn = tk.Canvas(toolbar, width=20, height=20, bg=color,
                                highlightthickness=1, highlightbackground="#555")
                btn.pack(side=tk.LEFT, padx=1)
                btn.bind("<Button-1>", lambda e, c=color: self._set_bg(c))

        ttk.Separator(toolbar, orient=tk.VERTICAL).pack(side=tk.LEFT, padx=6, fill=tk.Y)

        ttk.Label(toolbar, text="Scroll to pan | Wheel to zoom").pack(
            side=tk.LEFT, padx=4)
        self._info_label = ttk.Label(toolbar, text="")
        self._info_label.pack(side=tk.RIGHT, padx=4)

        # Canvas container with scrollbars
        container = ttk.Frame(parent)
        container.pack(fill=tk.BOTH, expand=True, side=tk.BOTTOM)

        self.preview_canvas = tk.Canvas(container, bg="#2b2b2b", highlightthickness=0)
        hbar = ttk.Scrollbar(container, orient=tk.HORIZONTAL, command=self.preview_canvas.xview)
        vbar = ttk.Scrollbar(container, orient=tk.VERTICAL, command=self.preview_canvas.yview)
        self.preview_canvas.configure(xscrollcommand=hbar.set, yscrollcommand=vbar.set)

        self.preview_canvas.grid(row=0, column=0, sticky="nsew")
        vbar.grid(row=0, column=1, sticky="ns")
        hbar.grid(row=1, column=0, sticky="ew")
        container.grid_rowconfigure(0, weight=1)
        container.grid_columnconfigure(0, weight=1)

        # Pan via drag
        self.preview_canvas.bind("<ButtonPress-1>", self._pan_start)
        self.preview_canvas.bind("<B1-Motion>", self._pan_move)
        self.preview_canvas.bind("<ButtonRelease-1>", self._pan_stop)

        # Mouse wheel zoom
        self.preview_canvas.bind("<MouseWheel>", self._on_preview_wheel)
        # Linux-style scroll
        self.preview_canvas.bind("<Button-4>", lambda e: self._zoom_step(0.25))
        self.preview_canvas.bind("<Button-5>", lambda e: self._zoom_step(-0.25))

    def _set_bg(self, color):
        self._bg_color = color
        if color == "checker":
            self.preview_canvas.configure(bg="#888")
        else:
            self.preview_canvas.configure(bg=color)
        self._render_preview()

    # ═══════════════════════════════════════════════════════════════
    #  Zoom
    # ═══════════════════════════════════════════════════════════════

    def _zoom_label_update(self):
        if self._zoom_level == 0:
            self._zoom_label.config(text="Fit")
        else:
            pct = int(self._zoom_level * 100)
            self._zoom_label.config(text=f"{pct}%")

    def _zoom_fit(self):
        self._zoom_level = 0
        self._pan_x = 0
        self._pan_y = 0
        self._zoom_label_update()
        self._render_preview()

    def _zoom_in(self):
        # Move to next higher zoom level
        current = self._get_effective_zoom()
        for z in ZOOM_LEVELS:
            if z > current + 0.001:
                self._zoom_level = z
                self._zoom_label_update()
                self._render_preview()
                return
        self._zoom_level = ZOOM_LEVELS[-1]
        self._zoom_label_update()
        self._render_preview()

    def _zoom_out(self):
        current = self._get_effective_zoom()
        for z in reversed(ZOOM_LEVELS):
            if z < current - 0.001:
                self._zoom_level = z
                self._zoom_label_update()
                self._render_preview()
                return
        self._zoom_level = 0  # fit
        self._zoom_label_update()
        self._render_preview()

    def _zoom_100(self):
        self._zoom_level = 1.0
        self._pan_x = 0
        self._pan_y = 0
        self._zoom_label_update()
        self._render_preview()

    def _zoom_step(self, delta):
        """Relative zoom from mouse wheel."""
        if self._zoom_level == 0:
            current = self._get_effective_zoom()
            # snap to nearest level then step
            nearest = min(ZOOM_LEVELS, key=lambda z: abs(z - current))
            idx = ZOOM_LEVELS.index(nearest)
        else:
            idx = ZOOM_LEVELS.index(self._zoom_level)

        new_idx = max(0, min(len(ZOOM_LEVELS) - 1, idx + (1 if delta > 0 else -1)))
        self._zoom_level = ZOOM_LEVELS[new_idx]
        if self._zoom_level < 0.05:
            self._zoom_level = 0
        self._zoom_label_update()
        self._render_preview()

    def _get_effective_zoom(self):
        """Return the actual zoom scale currently used."""
        if self._zoom_level != 0:
            return self._zoom_level
        if self.preview_img is None:
            return 1.0
        cw = self.preview_canvas.winfo_width()
        ch = self.preview_canvas.winfo_height()
        cw = cw if cw > 50 else 500
        ch = ch if ch > 50 else 400
        iw, ih = self.preview_img.width, self.preview_img.height
        if iw <= 0 or ih <= 0:
            return 1.0
        return min(cw / iw, ch / ih)

    def _on_preview_wheel(self, event):
        if event.state & 0x4:  # Ctrl key → horizontal scroll
            self.preview_canvas.xview_scroll(-1 * (event.delta // 120), "units")
        else:
            self._zoom_step(0.25 if event.delta > 0 else -0.25)

    # ═══════════════════════════════════════════════════════════════
    #  Panning
    # ═══════════════════════════════════════════════════════════════

    def _pan_start(self, event):
        self.preview_canvas.config(cursor="fleur")
        self._drag_start = (event.x, event.y)

    def _pan_move(self, event):
        if self._drag_start is None:
            return
        dx = event.x - self._drag_start[0]
        dy = event.y - self._drag_start[1]
        self.preview_canvas.xview_scroll(-dx, "units")
        self.preview_canvas.yview_scroll(-dy, "units")
        self._drag_start = (event.x, event.y)

    def _pan_stop(self, event):
        self.preview_canvas.config(cursor="")
        self._drag_start = None

    # ═══════════════════════════════════════════════════════════════
    #  Update logic
    # ═══════════════════════════════════════════════════════════════

    def _mark_dirty(self, *args):
        if self._update_id is not None:
            self.root.after_cancel(self._update_id)
        self._update_id = self.root.after(100, self._regenerate)

    def _schedule_update(self):
        self._mark_dirty()

    def _regenerate(self):
        self._update_id = None
        w = self.var_width.get()
        h = self.var_height.get()
        wood = self.var_wood.get()
        elem = self.var_element.get()
        seed = self.var_seed.get()
        angle = self.var_angle.get()
        curve = self.var_curve.get()
        corner = self.var_corner.get()
        grain_amt = self.var_grain.get()

        shadow_en = self.var_shadow_enable.get()
        shadow_off = (self.var_shadow_x.get(), self.var_shadow_y.get()) if shadow_en else None
        shadow_blur = self.var_shadow_blur.get()
        shadow_opacity = self.var_shadow_opacity.get() if shadow_en else 0
        border_w = self.var_border_width.get()
        border_style = self.var_border_style.get()
        border_inset = self.var_border_inset.get()

        img = wtg.create_texture(
            w, h,
            wood_type=wood,
            element=elem,
            seed=seed,
            angle=angle,
            ring_curvature=curve,
            corner_radius=corner,
            grain_amount=grain_amt,
            shadow_offset=shadow_off,
            shadow_blur=shadow_blur,
            shadow_opacity=shadow_opacity,
            border_width=border_w,
            border_style=border_style,
            border_color=self._border_color,
            border_inset=border_inset,
            border_widths=(self.var_cw_top.get(), self.var_cw_right.get(),
                           self.var_cw_bottom.get(), self.var_cw_left.get())
                           if self.var_custom_mode.get() else None,
            corner_radii=(self.var_cr_tl.get(), self.var_cr_tr.get(),
                          self.var_cr_bl.get(), self.var_cr_br.get())
                          if self.var_custom_mode.get() else None,
        )

        self.preview_img = img
        self._render_preview()

        iw, ih = img.width, img.height
        info = f"{w}×{h} → {iw}×{ih}  |  {wood} / {elem}  |  grain={grain_amt:.2f}  |  seed={seed}"
        if shadow_en:
            info += f"  |  shadow({shadow_off[0]},{shadow_off[1]}) blur={shadow_blur} α={shadow_opacity}"
        if border_w > 0:
            info += f"  |  border={border_w}px {border_style}"
        self._info_label.config(text=info)

    def _render_preview(self):
        """Render the preview image onto the canvas using current zoom and pan."""
        if self.preview_img is None:
            return

        img = self.preview_img
        zoom = self._get_effective_zoom()
        iw, ih = img.width, img.height

        if zoom == 1.0:
            display_img = img
        else:
            pw = max(1, int(iw * zoom))
            ph = max(1, int(ih * zoom))
            display_img = img.resize((pw, ph), Image.NEAREST)

        self.preview_tk = ImageTk.PhotoImage(display_img)
        self.preview_canvas.delete("all")

        # Draw checkerboard if selected
        if self._bg_color == "checker":
            self._draw_checkerboard(display_img.width, display_img.height)

        self.preview_canvas.create_image(
            0, 0,
            image=self.preview_tk,
            anchor="nw",
            tags="preview"
        )
        self.preview_canvas.configure(scrollregion=(0, 0, display_img.width, display_img.height))

    def _draw_checkerboard(self, tw, th):
        """Draw a checkerboard pattern to show transparency."""
        cs = max(8, min(tw, th) // 20)  # checker size adapts to image
        cols = tw // cs + 2
        rows = th // cs + 2
        for r in range(rows):
            for c in range(cols):
                color = "#888" if (r + c) % 2 == 0 else "#aaa"
                x1, y1 = c * cs, r * cs
                x2, y2 = x1 + cs, y1 + cs
                self.preview_canvas.create_rectangle(x1, y1, x2, y2, fill=color, outline="")

    def _on_resize(self, event):
        if event.widget is self.root:
            if self._zoom_level == 0:
                self._render_preview()

    # ═══════════════════════════════════════════════════════════════
    #  Save / Batch
    # ═══════════════════════════════════════════════════════════════

    def _save(self):
        if self.preview_img is None:
            return
        path = filedialog.asksaveasfilename(
            defaultextension=".png",
            filetypes=[("PNG Image", "*.png")],
            initialfile=f"wood_{self.var_wood.get()}_{self.var_element.get()}.png",
        )
        if path:
            self.preview_img.save(path)
            messagebox.showinfo("Saved", f"Saved to:\n{path}")

    def _batch(self):
        out_dir = filedialog.askdirectory(title="Select output folder")
        if not out_dir:
            return
        import os
        seed = random.randint(0, 999999)
        elem = self.var_element.get()
        shadow_en = self.var_shadow_enable.get()
        shadow_off = (self.var_shadow_x.get(), self.var_shadow_y.get()) if shadow_en else None
        shadow_blur = self.var_shadow_blur.get()
        shadow_opacity = self.var_shadow_opacity.get() if shadow_en else 0
        border_w = self.var_border_width.get()
        border_style = self.var_border_style.get()
        border_inset = self.var_border_inset.get()
        for wood in wtg.WOOD_TYPES:
            w, h = self.var_width.get(), self.var_height.get()
            img = wtg.create_texture(
                w, h, wood_type=wood, element=elem, seed=seed,
                angle=self.var_angle.get(),
                ring_curvature=self.var_curve.get(),
                corner_radius=self.var_corner.get(),
                grain_amount=self.var_grain.get(),
                shadow_offset=shadow_off,
                shadow_blur=shadow_blur,
                shadow_opacity=shadow_opacity,
                border_width=border_w,
                border_style=border_style,
                border_color=self._border_color,
                border_inset=border_inset,
                border_widths=(self.var_cw_top.get(), self.var_cw_right.get(),
                               self.var_cw_bottom.get(), self.var_cw_left.get())
                               if self.var_custom_mode.get() else None,
                corner_radii=(self.var_cr_tl.get(), self.var_cr_tr.get(),
                              self.var_cr_bl.get(), self.var_cr_br.get())
                              if self.var_custom_mode.get() else None,
            )
            path = os.path.join(out_dir, f"wood_{wood}_{elem}.png")
            img.save(path)
        messagebox.showinfo("Done", f"Exported {len(wtg.WOOD_TYPES)} textures to:\n{out_dir}")


def main():
    root = tk.Tk()
    WoodTextureGUI(root)
    root.mainloop()


if __name__ == "__main__":
    main()

"""
Wooden UI Texture Generator
Generates procedural wood-grain textures for game UI elements.
"""

import argparse
import math
import os
import random
import numpy as np
from PIL import Image, ImageFilter, ImageDraw


# ── Wood type presets ──────────────────────────────────────────────
WOOD_TYPES = {
    "oak": {
        "base": (180, 150, 110),
        "grain_dark": (140, 110, 70),
        "grain_light": (210, 180, 140),
        "grain_intensity": 1.0,
        "ring_spacing": 8,
        "noise_scale": 0.3,
    },
    "walnut": {
        "base": (90, 60, 40),
        "grain_dark": (50, 30, 15),
        "grain_light": (130, 95, 65),
        "grain_intensity": 1.2,
        "ring_spacing": 6,
        "noise_scale": 0.25,
    },
    "mahogany": {
        "base": (160, 80, 50),
        "grain_dark": (120, 50, 25),
        "grain_light": (200, 120, 85),
        "grain_intensity": 1.1,
        "ring_spacing": 7,
        "noise_scale": 0.2,
    },
    "pine": {
        "base": (220, 200, 160),
        "grain_dark": (190, 165, 115),
        "grain_light": (240, 225, 195),
        "grain_intensity": 0.7,
        "ring_spacing": 12,
        "noise_scale": 0.4,
    },
    "cherry": {
        "base": (170, 105, 70),
        "grain_dark": (140, 70, 40),
        "grain_light": (200, 140, 105),
        "grain_intensity": 0.9,
        "ring_spacing": 7,
        "noise_scale": 0.2,
    },
    "ash": {
        "base": (200, 185, 155),
        "grain_dark": (160, 145, 115),
        "grain_light": (225, 210, 185),
        "grain_intensity": 0.8,
        "ring_spacing": 9,
        "noise_scale": 0.35,
    },
}


def value_noise(size: int, scale: float = 0.1, seed: int = 0) -> np.ndarray:
    """Generate value noise grid using interpolated random lattice points."""
    rng = np.random.RandomState(seed)
    grid_size = max(2, int(size * scale) + 1)
    lattice = rng.rand(grid_size, grid_size)
    # Upsample with bilinear interpolation
    xx = np.linspace(0, grid_size - 1, size)
    yy = np.linspace(0, grid_size - 1, size)
    xi = np.floor(xx).astype(int)
    yi = np.floor(yy).astype(int)
    xf = xx - xi
    yf = yy - yi
    xi1 = np.clip(xi + 1, 0, grid_size - 1)
    yi1 = np.clip(yi + 1, 0, grid_size - 1)

    top = (1 - xf[:, None]) * lattice[np.ix_(xi, yi)] + xf[:, None] * lattice[np.ix_(xi1, yi)]
    bot = (1 - xf[:, None]) * lattice[np.ix_(xi, yi1)] + xf[:, None] * lattice[np.ix_(xi1, yi1)]
    return (1 - yf[None, :]) * top + yf[None, :] * bot


def _get_noise(size_h: int, size_w: int, scale: float, noise_seed: int) -> np.ndarray:
    """Build a noise texture padded/tiled to cover the requested dimensions."""
    n = value_noise(max(size_h, size_w, 64), scale=scale, seed=noise_seed)
    while n.shape[0] < size_h:
        n = np.concatenate([n, n], axis=0)
    while n.shape[1] < size_w:
        n = np.concatenate([n, n], axis=1)
    return n[:size_h, :size_w]


def generate_wood_grain(width: int, height: int, wood_type: str, seed: int = 0,
                        angle: float = 0.0, ring_curvature: float = 0.0,
                        grain_amount: float = 1.0) -> np.ndarray:
    """
    Generate a clean wood-grain texture as an RGB numpy array (float, 0–1).
    Grain runs horizontally by default; angle rotates the pattern.

    grain_amount: 0.0 = solid flat color, 1.0 = full grain texture.
    """
    wt = WOOD_TYPES.get(wood_type, WOOD_TYPES["oak"])
    base = (np.array(wt["base"]) / 255.0).reshape(3, 1, 1)
    dark = (np.array(wt["grain_dark"]) / 255.0).reshape(3, 1, 1)
    light = (np.array(wt["grain_light"]) / 255.0).reshape(3, 1, 1)
    ring_spacing = wt["ring_spacing"]
    noise_scale = wt["noise_scale"]

    Y, X = np.ogrid[:height, :width]
    Y_f = Y.astype(np.float64)
    X_f = X.astype(np.float64)

    if angle != 0.0:
        rad = math.radians(angle)
        cos_a, sin_a = math.cos(rad), math.sin(rad)
        Y_rot = Y_f * cos_a - X_f * sin_a
    else:
        Y_rot = Y_f

    # Very subtle natural waviness (±2.5 px max) — clean but not sterile
    n_wave = _get_noise(height, width, noise_scale * 0.2, seed)
    wave_offset = (n_wave - 0.5) * 5.0

    if ring_curvature != 0.0:
        center_x = width / 2.0
        wave_offset += ((X_f - center_x) ** 2) * ring_curvature / width

    rings = Y_rot + wave_offset

    # Two-octave grain — primary + subtle secondary harmonic
    grain = (
        np.sin(rings / ring_spacing * 2 * math.pi) * 0.7
        + np.sin(rings / ring_spacing * 1.7 * 2 * math.pi + 1.0) * 0.3
    )

    # Optional: broad tonal drift across the plank (large-scale subtle variation)
    n_tonal = _get_noise(height, width, noise_scale * 0.08, seed + 42)
    tonal_drift = (n_tonal - 0.5) * 0.25

    grain = grain * grain_amount * wt["grain_intensity"] + tonal_drift

    # Map grain to color: positive → lighter, negative → darker
    grain_clipped = np.clip(grain, -1.0, 1.0)
    contrast = 0.55  # kept moderate so the texture stays subtle
    texture = np.where(
        grain_clipped > 0,
        base + (light - base) * grain_clipped * contrast,
        base + (base - dark) * grain_clipped * contrast,
    )

    return np.clip(texture, 0.0, 1.0).transpose(1, 2, 0)


def apply_bevel(mask: np.ndarray, width: int, height: int,
                bevel_size: float, bevel_type: str = "raised") -> np.ndarray:
    """Generate a bevel light map (0–1) for a rectangular area."""
    bevel_px = max(1, int(bevel_size * min(width, height)))
    light_map = np.full((height, width), 0.5, dtype=np.float32)

    for i in range(bevel_px):
        t = i / bevel_px
        if bevel_type == "raised":
            val = 0.5 + 0.5 * (1 - t)  # top-left bright
        elif bevel_type == "sunken":
            val = 0.5 - 0.5 * (1 - t)
        else:
            val = 0.5 + 0.3 * ((bevel_px - i) / bevel_px - 0.5) * 2

        # Top edge
        if i < height:
            light_map[i, bevel_px:] = light_map[i, bevel_px:].copy()
            light_map[i, i:width - i] = 0.5 + 0.35 * (1 - t)
        # Bottom edge
        if height - 1 - i >= 0:
            light_map[height - 1 - i, bevel_px:-bevel_px or None] = 0.5 - 0.35 * (1 - t)
        # Left edge
        light_map[bevel_px:-bevel_px or None, i] = 0.5 + 0.3 * (1 - t)
        # Right edge
        light_map[bevel_px:-bevel_px or None, width - 1 - i] = 0.5 - 0.3 * (1 - t)

    # Corners
    for y in range(bevel_px):
        for x in range(bevel_px):
            t = max(x, y) / bevel_px
            light_map[y, x] = 0.5 + 0.4 * (1 - t)
        for x in range(max(0, width - bevel_px), width):
            rx = width - 1 - x
            t = max(y, rx) / bevel_px
            light_map[y, x] = 0.5 + 0.2 * (1 - t)
    for y in range(max(0, height - bevel_px), height):
        ry = height - 1 - y
        for x in range(bevel_px):
            t = max(ry, x) / bevel_px
            light_map[y, x] = 0.5 - 0.2 * (1 - t)
        for x in range(max(0, width - bevel_px), width):
            rx = width - 1 - x
            t = max(ry, rx) / bevel_px
            light_map[y, x] = 0.5 - 0.4 * (1 - t)

    return light_map


def draw_drop_shadow(img: Image.Image,
                     offset_x: int = 4, offset_y: int = 4,
                     blur_radius: int = 6, opacity: int = 120,
                     shadow_color: tuple = (0, 0, 0)) -> Image.Image:
    """
    Composite a drop shadow behind the image.
    Returns a new RGBA image sized to contain both the shadow and original.
    """
    if blur_radius <= 0 and offset_x == 0 and offset_y == 0:
        return img.copy()

    pad_x = abs(offset_x) + blur_radius * 2
    pad_y = abs(offset_y) + blur_radius * 2

    out_w = img.width + pad_x * 2
    out_h = img.height + pad_y * 2

    # Shadow layer
    shadow_alpha = Image.new("L", (out_w, out_h), 0)
    shadow_canvas = ImageDraw.Draw(shadow_alpha)
    sx = pad_x + offset_x
    sy = pad_y + offset_y
    shadow_canvas.rounded_rectangle(
        [sx, sy, sx + img.width - 1, sy + img.height - 1],
        radius=getattr(img, "corner_radius", 0),
        fill=opacity,
    )
    if blur_radius > 0:
        shadow_alpha = shadow_alpha.filter(ImageFilter.GaussianBlur(blur_radius))

    shadow_layer = Image.new("RGBA", (out_w, out_h), (*shadow_color, 0))
    shadow_layer.putalpha(shadow_alpha)

    # Compose: shadow first, then image on top
    result = Image.new("RGBA", (out_w, out_h), (0, 0, 0, 0))
    result.paste(shadow_layer, (0, 0), shadow_layer)
    result.paste(img, (pad_x, pad_y), img)

    # Store corner radius for later use
    result.corner_radius = getattr(img, "corner_radius", 0)
    return result


def _lerp_color(a: tuple, b: tuple, t: float) -> tuple:
    """Linear interpolation between two RGB tuples."""
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def _draw_side_strip(canvas, side, bw_self, bw_ortho_a, bw_ortho_b,
                     cr_a, cr_b, iw, ih, inset, color):
    """画一条直边（填充矩形），不覆盖角落区域。"""
    if bw_self <= 0:
        return
    if side == 'top':
        x0 = inset + cr_a
        y0 = inset
        x1 = iw - 1 - inset - cr_b
        y1 = inset + bw_self - 1
    elif side == 'bottom':
        x0 = inset + cr_a
        y0 = ih - 1 - inset - bw_self + 1
        x1 = iw - 1 - inset - cr_b
        y1 = ih - 1 - inset
    elif side == 'left':
        x0 = inset
        y0 = inset + cr_a
        x1 = inset + bw_self - 1
        y1 = ih - 1 - inset - cr_b
    else:  # right
        x0 = iw - 1 - inset - bw_self + 1
        y0 = inset + cr_a
        x1 = iw - 1 - inset
        y1 = ih - 1 - inset - cr_b
    if x0 <= x1 and y0 <= y1:
        canvas.rectangle([x0, y0, x1, y1], fill=color)


def _draw_corner_fill(canvas, corner, bw_a, bw_b, cr,
                      iw, ih, inset, color):
    """画一个圆角区域。当相邻两边宽度不同时，内圈用椭圆扇形平滑过渡。"""
    if cr <= 0 or max(bw_a, bw_b) <= 0:
        return
    # 外圈：四分之一圆，半径 = cr
    # 内圈半轴：水平由垂直边宽度决定(bw_b)，垂直由水平边宽度决定(bw_a)
    inner_rx = max(0, cr - bw_b)  # 水平半轴：左/右边宽度
    inner_ry = max(0, cr - bw_a)  # 垂直半轴：顶/底边宽度
    if corner == 'tl':
        obox = [inset, inset, inset + cr * 2, inset + cr * 2]
        start, end = 180, 270
        ibox = [inset + cr - inner_rx, inset + cr - inner_ry,
                inset + cr + inner_rx, inset + cr + inner_ry]
    elif corner == 'tr':
        obox = [iw - 1 - inset - cr * 2, inset,
                iw - 1 - inset, inset + cr * 2]
        start, end = 270, 360
        ibox = [iw - 1 - inset - cr - inner_rx, inset + cr - inner_ry,
                iw - 1 - inset - cr + inner_rx, inset + cr + inner_ry]
    elif corner == 'bl':
        obox = [inset, ih - 1 - inset - cr * 2,
                inset + cr * 2, ih - 1 - inset]
        start, end = 90, 180
        ibox = [inset + cr - inner_rx, ih - 1 - inset - cr - inner_ry,
                inset + cr + inner_rx, ih - 1 - inset - cr + inner_ry]
    else:  # br
        obox = [iw - 1 - inset - cr * 2, ih - 1 - inset - cr * 2,
                iw - 1 - inset, ih - 1 - inset]
        start, end = 0, 90
        ibox = [iw - 1 - inset - cr - inner_rx, ih - 1 - inset - cr - inner_ry,
                iw - 1 - inset - cr + inner_rx, ih - 1 - inset - cr + inner_ry]
    # 画外圈
    canvas.pieslice(obox, start, end, fill=color)
    # 挖掉内圈（椭圆扇形）
    if inner_rx > 0 and inner_ry > 0:
        canvas.pieslice(ibox, start, end, fill=(0, 0, 0, 0))


def _draw_border_custom(image, border_layer, border_array, opaque_mask,
                        border_style,
                        color_top, color_left, color_right, color_bottom,
                        bw_top, bw_right, bw_bottom, bw_left,
                        distance_to_top, distance_to_left,
                        distance_to_bottom, distance_to_right,
                        distance_to_nearest_edge):
    """自定义模式：像素级反距离加权混合。每条边宽度、每个圆角半径独立。"""
    # 反距离平方加权 → 近边权重极高，远边可忽略。拐角处两条边权重相当 → 自然混合。
    eps_dist = 0.1
    w_t = 1.0 / (distance_to_top * distance_to_top + eps_dist)
    w_l = 1.0 / (distance_to_left * distance_to_left + eps_dist)
    w_b = 1.0 / (distance_to_bottom * distance_to_bottom + eps_dist)
    w_r = 1.0 / (distance_to_right * distance_to_right + eps_dist)
    w_sum = w_t + w_l + w_b + w_r + 1e-9
    weight_top = (w_t / w_sum).astype(np.float32)
    weight_left = (w_l / w_sum).astype(np.float32)
    weight_bottom = (w_b / w_sum).astype(np.float32)
    weight_right = (w_r / w_sum).astype(np.float32)

    # 颜色分配
    ct = np.array(color_top, dtype=np.float32)
    cl = np.array(color_left, dtype=np.float32)
    cr = np.array(color_right, dtype=np.float32)
    cb = np.array(color_bottom, dtype=np.float32)

    if border_style == "raised":
        assigned_top, assigned_left = ct, cl
        assigned_right, assigned_bottom = cr, cb
    elif border_style == "sunken":
        assigned_top, assigned_left = cb, cr
        assigned_right, assigned_bottom = cl, ct
    else:
        # groove / ridge：用加权平均确定每像素的等效边框宽度，归一化深度判内外半层
        bw_effective = (
            weight_top * bw_top + weight_left * bw_left +
            weight_bottom * bw_bottom + weight_right * bw_right)
        normalized_depth = np.where(
            bw_effective > 0, distance_to_nearest_edge / bw_effective, 0.0)
        outer_half = (normalized_depth < 0.5).astype(np.float32)
        inner_half = 1.0 - outer_half
        if border_style == "ridge":
            # 外半 raised(凸) + 内半 sunken(凹)
            assigned_top = (outer_half[..., None] * ct[None, None, :] +
                            inner_half[..., None] * cb[None, None, :])
            assigned_left = (outer_half[..., None] * cl[None, None, :] +
                             inner_half[..., None] * cr[None, None, :])
            assigned_right = (outer_half[..., None] * cr[None, None, :] +
                              inner_half[..., None] * cl[None, None, :])
            assigned_bottom = (outer_half[..., None] * cb[None, None, :] +
                               inner_half[..., None] * ct[None, None, :])
        else:  # groove
            assigned_top = (outer_half[..., None] * cb[None, None, :] +
                            inner_half[..., None] * ct[None, None, :])
            assigned_left = (outer_half[..., None] * cr[None, None, :] +
                             inner_half[..., None] * cl[None, None, :])
            assigned_right = (outer_half[..., None] * cl[None, None, :] +
                              inner_half[..., None] * cr[None, None, :])
            assigned_bottom = (outer_half[..., None] * ct[None, None, :] +
                               inner_half[..., None] * cb[None, None, :])

    # 加权混合
    total_weight = weight_top + weight_left + weight_bottom + weight_right + 1e-9
    if border_style in ("groove", "ridge"):
        blended_color = (
            weight_top[..., None] * assigned_top +
            weight_left[..., None] * assigned_left +
            weight_right[..., None] * assigned_right +
            weight_bottom[..., None] * assigned_bottom
        ) / total_weight[..., None]
    else:
        blended_color = (
            weight_top[..., None] * assigned_top[None, None, :] +
            weight_left[..., None] * assigned_left[None, None, :] +
            weight_right[..., None] * assigned_right[None, None, :] +
            weight_bottom[..., None] * assigned_bottom[None, None, :]
        ) / total_weight[..., None]

    for channel in range(3):
        border_array[opaque_mask, channel] = blended_color[opaque_mask, channel]
    border_array = np.clip(border_array, 0, 255).astype(np.uint8)
    border_layer = Image.fromarray(border_array, mode="RGBA")
    image.paste(border_layer, (0, 0), border_layer)
    return image


def draw_border(image: Image.Image,
                border_width: int = 2,
                border_style: str = "flat",
                border_color: tuple = (60, 40, 20),
                corner_radius: int = 0,
                inset: int = 0,
                border_widths: tuple = None,   # (top, right, bottom, left)  自定义每条边宽度
                corner_radii: tuple = None) -> Image.Image:  # (tl, tr, bl, br)  自定义每个圆角半径
    """
    绘制带四条边独立颜色的边框。

    四边颜色方案（模拟光源从左上角照射）：
      top    — 最亮（顶边受光最强）
      left   — 次亮（左边环境光反射）
      right  — 次暗（右边背光）
      bottom — 最暗（底边阴影最重）

    风格：
      flat   — 四边同色，无立体感
      raised — 凸起按钮：顶>左>右>底
      sunken — 凹陷输入框：底>右>左>顶（与 raised 互换四边颜色）
      groove — V 形刻槽：外半层凹陷 + 内半层凸起
      ridge  — ∧ 形凸脊：外半层凸起 + 内半层凹陷

    自定义模式：传入 border_widths=(顶,右,底,左) 和/或 corner_radii=(TL,TR,BL,BR)
    二者均可缺省，缺省时回退到统一值。
    """
    image_width = image.width
    image_height = image.height

    # ── 解析自定义参数 ──
    # 如果四边宽度完全相同，回退到统一 border_width
    if border_widths is not None and \
       border_widths[0] == border_widths[1] == border_widths[2] == border_widths[3]:
        border_width = border_widths[0]
        border_widths = None
    # 如果四角半径完全相同，回退到统一 corner_radius
    if corner_radii is not None and \
       corner_radii[0] == corner_radii[1] == corner_radii[2] == corner_radii[3]:
        corner_radius = corner_radii[0]
        corner_radii = None
    use_custom = (border_widths is not None) or (corner_radii is not None)
    if border_widths is None:
        bw_top = bw_right = bw_bottom = bw_left = border_width
    else:
        bw_top, bw_right, bw_bottom, bw_left = border_widths
    if corner_radii is None:
        cr_tl = cr_tr = cr_bl = cr_br = corner_radius
    else:
        cr_tl, cr_tr, cr_bl, cr_br = corner_radii

    # 检查是否有任何可见的边框
    if max(bw_top, bw_right, bw_bottom, bw_left) <= 0:
        return image

    half_border_width = border_width / 2.0

    # ═══════════════════════════════════════════════════════════════
    # 预计算四条边的目标颜色
    # ═══════════════════════════════════════════════════════════════
    color_top = _lerp_color(border_color, (255, 255, 255), 0.45)
    color_left = _lerp_color(border_color, (255, 255, 255), 0.20)
    color_right = _lerp_color(border_color, (0, 0, 0), 0.10)
    color_bottom = _lerp_color(border_color, (0, 0, 0), 0.55)

    # ═══════════════════════════════════════════════════════════════
    # 第一步：画出边框像素
    # ═══════════════════════════════════════════════════════════════
    border_layer = Image.new(
        "RGBA", (image_width, image_height), (0, 0, 0, 0))
    canvas = ImageDraw.Draw(border_layer)

    if use_custom:
        # 自定义模式：分别画四条直边（填充矩形）和四个角（填充扇形）
        _draw_side_strip(canvas, 'top', bw_top, bw_left, bw_right,
                         cr_tl, cr_tr, image_width, image_height, inset, border_color)
        _draw_side_strip(canvas, 'bottom', bw_bottom, bw_left, bw_right,
                         cr_bl, cr_br, image_width, image_height, inset, border_color)
        _draw_side_strip(canvas, 'left', bw_left, bw_top, bw_bottom,
                         cr_tl, cr_bl, image_width, image_height, inset, border_color)
        _draw_side_strip(canvas, 'right', bw_right, bw_top, bw_bottom,
                         cr_tr, cr_br, image_width, image_height, inset, border_color)
        _draw_corner_fill(canvas, 'tl', bw_top, bw_left, cr_tl,
                          image_width, image_height, inset, border_color)
        _draw_corner_fill(canvas, 'tr', bw_top, bw_right, cr_tr,
                          image_width, image_height, inset, border_color)
        _draw_corner_fill(canvas, 'bl', bw_bottom, bw_left, cr_bl,
                          image_width, image_height, inset, border_color)
        _draw_corner_fill(canvas, 'br', bw_bottom, bw_right, cr_br,
                          image_width, image_height, inset, border_color)
    else:
        # 默认模式：单条 PIL rounded_rectangle 粗轮廓线
        outer_corner_radius = max(0, corner_radius - inset)
        canvas.rounded_rectangle(
            [inset, inset, image_width - 1 - inset, image_height - 1 - inset],
            radius=outer_corner_radius,
            outline=border_color,
            width=border_width,
        )

    # flat 风格不需要替换颜色，直接合成返回
    if border_style == "flat":
        image.paste(border_layer, (0, 0), border_layer)
        return image

    # ═══════════════════════════════════════════════════════════════
    # 第二步：逐个像素判定"这个像素属于哪条边"，然后赋予对应颜色。
    #
    # 整体思路：
    #   1. 先做硬判定——像素到哪条边最近就属于哪条边。
    #   2. 在四个角落区域内用角度做渐变（atan2），覆盖硬判定的结果。
    #   3. 按风格决定每条边最终用哪个颜色，然后加权混合。
    #
    # 数据结构说明：
    #   distance_to_*   — float32 数组 (H, W)，每个像素到该边的距离
    #   weight_*        — float32 数组 (H, W)，每个像素中该边的颜色权重
    #   opaque_mask     — bool 数组 (H, W)，标记哪些像素属于边框
    # ═══════════════════════════════════════════════════════════════

    border_array = np.array(border_layer, dtype=np.float32)
    opaque_mask = border_array[:, :, 3] > 0

    # 生成全图每个像素的坐标网格 (y, x)
    # mgrid 产生两个 (H, W) 数组，分别包含每像素的行号和列号
    pixel_y, pixel_x = np.mgrid[:image_height, :image_width].astype(np.float32)

    # 计算每个像素到四条图像边缘的像素距离
    # inset 不为 0 时等效于把边框向内缩进了 inset 像素
    distance_to_top = pixel_y - inset
    distance_to_left = pixel_x - inset
    distance_to_bottom = image_height - 1.0 - inset - pixel_y
    distance_to_right = image_width - 1.0 - inset - pixel_x
    distance_to_nearest_edge = np.minimum(
        np.minimum(distance_to_top, distance_to_left),
        np.minimum(distance_to_bottom, distance_to_right))

    # ── 自定义模式：像素级反距离加权，提前返回 ──
    if use_custom:
        return _draw_border_custom(
            image, border_layer, border_array, opaque_mask, border_style,
            color_top, color_left, color_right, color_bottom,
            bw_top, bw_right, bw_bottom, bw_left,
            distance_to_top, distance_to_left, distance_to_bottom, distance_to_right,
            distance_to_nearest_edge)

    # ─────────────────────────────────────────────────────────────
    # 2a. 硬判定：像素属于距离自己最近的那条边。
    #
    # distance_to_nearest_edge = min(四个距离)
    # weight_* = 该距离是否 ≤ 最近距离 + 0.5px
    #
    # 0.5px 容差的作用：
    #   在两条边距离相等或接近（差值 ≤0.5px）时，像素同时属于两条边，
    #   颜色取两条边的平均。这解决了"正方形角点像素应该同时属于
    #   顶边和左边"的问题。
    #
    # 例如：像素 (0, 0) 在正方形边框的左上角。
    #   distance_to_top = 0, distance_to_left = 0
    #   distance_to_nearest_edge = 0
    #   weight_top  = (0 <= 0 + 0.5) = True  = 1.0
    #   weight_left = (0 <= 0 + 0.5) = True  = 1.0
    #   → 各占 50%，颜色 = (top_color + left_color) / 2 ✓
    # ─────────────────────────────────────────────────────────────
    classification_tolerance = 0.5

    weight_top = (
        distance_to_top <= distance_to_nearest_edge + classification_tolerance
    ).astype(np.float32)
    weight_left = (
        distance_to_left <= distance_to_nearest_edge + classification_tolerance
    ).astype(np.float32)
    weight_bottom = (
        distance_to_bottom <= distance_to_nearest_edge + classification_tolerance
    ).astype(np.float32)
    weight_right = (
        distance_to_right <= distance_to_nearest_edge + classification_tolerance
    ).astype(np.float32)

    # ─────────────────────────────────────────────────────────────
    # 2b. 角落渐变：以圆角的四分之一圆圆心为基准，在两条相邻边
    #     颜色之间按角度做线性混合。
    #
    # 每个角落的圆心 = 该四分之一圆的圆心。
    # 圆弧区域：dx² + dy² <= cr²，且距两邻边均 < cr。
    #
    # 四个角落的 atan2 角度范围各不相同，需各自归一化：
    #
    #   左上角  圆心 (cr, cr)
    #           dx = distance_to_left - cr
    #           dy = distance_to_top  - cr
    #           角度范围：-π/2（顶切点）→ -π（左切点，π 需绕回 -π）
    #           frac = (angle + π/2) / (-π/2)   即  (-angle - π/2) / (π/2)
    #
    #   右上角  圆心 (image_width-1-cr, cr)
    #           dx = cr - distance_to_right
    #           dy = distance_to_top - cr
    #           角度范围：-π/2（顶切点）→ 0（右切点）
    #           frac = (angle + π/2) / (π/2)
    #
    #   左下角  圆心 (cr, image_height-1-cr)
    #           dx = distance_to_left - cr
    #           dy = cr - distance_to_bottom
    #           角度范围：0（左切点）→ π/2（底切点）
    #           注意：此处渐变是 底边→左边。底切点 angle = π/2，左切点 angle = 0。
    #           frac 从底到左：frac = 1 - angle/(π/2)
    #
    #   右下角  圆心 (image_width-1-cr, image_height-1-cr)
    #           dx = cr - distance_to_right
    #           dy = cr - distance_to_bottom
    #           角度范围：0（右切点）→ π/2（底切点）
    #           frac = angle / (π/2)
    #
    # cr=0 时仅 (0,0) 一个像素，atan2(0,0)=0，特殊处理为 0.5。
    # ─────────────────────────────────────────────────────────────
    corner_radius_float = float(corner_radius)
    epsilon = 1e-9

    # --- 左上角：顶边 ↔ 左边 ---
    dx_tl = distance_to_left - corner_radius_float
    dy_tl = distance_to_top - corner_radius_float
    angle_tl = np.arctan2(dy_tl, dx_tl)                     # [-π/2, π]
    angle_tl = np.where(angle_tl > math.pi * 0.5,           # π → -π
                        angle_tl - math.pi * 2.0, angle_tl)  # 归到 [-π/2, -π]
    blend_fraction_top_left = (
        -angle_tl - math.pi * 0.5) / (math.pi * 0.5)         # 顶=0，左=1
    blend_fraction_top_left = np.where(
        (abs(dx_tl) < epsilon) & (abs(dy_tl) < epsilon),
        0.5, blend_fraction_top_left)
    # 用正方形区域 (d_top < cr AND d_left < cr) 而非四分之一圆，
    # 因为 PIL 的 rounded_rectangle 抗锯齿像素可能略超出几何圆弧。
    # 正方形区域必然覆盖 PIL 绘制的所有圆角像素。
    # 角度公式保证区域边界处颜色趋近纯边色，与硬判定无缝衔接。
    top_left_corner_mask = (
        (distance_to_top < corner_radius_float) &
        (distance_to_left < corner_radius_float))
    weight_top[top_left_corner_mask] = (
        1.0 - blend_fraction_top_left[top_left_corner_mask])
    weight_left[top_left_corner_mask] = (
        blend_fraction_top_left[top_left_corner_mask])
    weight_bottom[top_left_corner_mask] = 0.0
    weight_right[top_left_corner_mask] = 0.0

    # --- 右上角：顶边 ↔ 右边 ---
    dx_tr = corner_radius_float - distance_to_right
    dy_tr = distance_to_top - corner_radius_float
    angle_tr = np.arctan2(dy_tr, dx_tr)                     # [-π/2, 0]
    blend_fraction_top_right = (
        angle_tr + math.pi * 0.5) / (math.pi * 0.5)          # 顶=0，右=1
    blend_fraction_top_right = np.where(
        (abs(dx_tr) < epsilon) & (abs(dy_tr) < epsilon),
        0.5, blend_fraction_top_right)
    top_right_corner_mask = (
        (distance_to_top < corner_radius_float) &
        (distance_to_right < corner_radius_float))
    weight_top[top_right_corner_mask] = (
        1.0 - blend_fraction_top_right[top_right_corner_mask])
    weight_right[top_right_corner_mask] = (
        blend_fraction_top_right[top_right_corner_mask])
    weight_bottom[top_right_corner_mask] = 0.0
    weight_left[top_right_corner_mask] = 0.0

    # --- 左下角：底边 ↔ 左边 ---
    dx_bl = distance_to_left - corner_radius_float
    dy_bl = corner_radius_float - distance_to_bottom
    angle_bl = np.arctan2(dy_bl, dx_bl)                     # [0, π/2]
    # 底切点 angle=π/2, 左切点 angle=π。frac 从底到左：底=0, 左=1
    blend_fraction_bottom_left = (
        angle_bl - math.pi * 0.5) / (math.pi * 0.5)
    blend_fraction_bottom_left = np.where(
        (abs(dx_bl) < epsilon) & (abs(dy_bl) < epsilon),
        0.5, blend_fraction_bottom_left)
    bottom_left_corner_mask = (
        (distance_to_bottom < corner_radius_float) &
        (distance_to_left < corner_radius_float))
    weight_bottom[bottom_left_corner_mask] = (
        1.0 - blend_fraction_bottom_left[bottom_left_corner_mask])
    weight_left[bottom_left_corner_mask] = (
        blend_fraction_bottom_left[bottom_left_corner_mask])
    weight_top[bottom_left_corner_mask] = 0.0
    weight_right[bottom_left_corner_mask] = 0.0

    # --- 右下角：底边 ↔ 右边 ---
    dx_br = corner_radius_float - distance_to_right
    dy_br = corner_radius_float - distance_to_bottom
    angle_br = np.arctan2(dy_br, dx_br)                     # [0, π/2]
    # 右切点 angle=0, 底切点 angle=π/2。frac 从右到底：右=0, 底=1
    blend_fraction_bottom_right = angle_br / (math.pi * 0.5)
    blend_fraction_bottom_right = np.where(
        (abs(dx_br) < epsilon) & (abs(dy_br) < epsilon),
        0.5, blend_fraction_bottom_right)
    bottom_right_corner_mask = (
        (distance_to_bottom < corner_radius_float) &
        (distance_to_right < corner_radius_float))
    weight_right[bottom_right_corner_mask] = (
        1.0 - blend_fraction_bottom_right[bottom_right_corner_mask])
    weight_bottom[bottom_right_corner_mask] = (
        blend_fraction_bottom_right[bottom_right_corner_mask])
    weight_top[bottom_right_corner_mask] = 0.0
    weight_left[bottom_right_corner_mask] = 0.0

    # ─────────────────────────────────────────────────────────────
    # 2c. 按风格决定每条边的最终颜色，然后用权重混合。
    #
    # 在此之前 weight_* 已就绪：每个像素的四个权重之和 = 1（或接近1）。
    # 现在需要确定每条边的"目标色"。
    #
    # raised / sunken —— 四边颜色固定，不随像素位置变化。
    #   raised:  顶=亮, 左=次亮, 右=次暗, 底=暗
    #   sunken:  把 raised 的四边颜色互换位置（顶↔底, 左↔右）
    #
    # groove / ridge —— 边框分"外半层"和"内半层"。
    #   外半层 = 接近图像边缘的那一半边框宽度内的像素
    #   内半层 = 接近图像内部的那一半
    #   ridge  = 外半 raised + 内半 sunken
    #   groove = 外半 sunken + 内半 raised
    #
    # border_ring_index 就是 distance_to_nearest_edge，
    # 外层像素 index 小（0,1,2...），内层 index 大。
    # ─────────────────────────────────────────────────────────────
    border_ring_index = distance_to_nearest_edge
    border_ring_index = np.clip(border_ring_index, 0, border_width - 1)

    # 四边参考色转为 numpy 数组
    color_top_array = np.array(color_top, dtype=np.float32)
    color_left_array = np.array(color_left, dtype=np.float32)
    color_right_array = np.array(color_right, dtype=np.float32)
    color_bottom_array = np.array(color_bottom, dtype=np.float32)

    if border_style == "raised":
        # 凸起：顶亮 → 左次亮 → 右次暗 → 底暗
        assigned_top_color = color_top_array
        assigned_left_color = color_left_array
        assigned_right_color = color_right_array
        assigned_bottom_color = color_bottom_array

    elif border_style == "sunken":
        # 凹陷：互换四边颜色（顶↔底, 左↔右）
        assigned_top_color = color_bottom_array
        assigned_left_color = color_right_array
        assigned_right_color = color_left_array
        assigned_bottom_color = color_top_array

    elif border_style in ("groove", "ridge"):
        outer_half = border_ring_index < half_border_width
        if border_style == "ridge":
            assigned_top_color = np.where(
                outer_half[..., None],
                color_top_array[None, None, :],
                color_bottom_array[None, None, :])
            assigned_left_color = np.where(
                outer_half[..., None],
                color_left_array[None, None, :],
                color_right_array[None, None, :])
            assigned_right_color = np.where(
                outer_half[..., None],
                color_right_array[None, None, :],
                color_left_array[None, None, :])
            assigned_bottom_color = np.where(
                outer_half[..., None],
                color_bottom_array[None, None, :],
                color_top_array[None, None, :])
        else:  # groove
            assigned_top_color = np.where(
                outer_half[..., None],
                color_bottom_array[None, None, :],
                color_top_array[None, None, :])
            assigned_left_color = np.where(
                outer_half[..., None],
                color_right_array[None, None, :],
                color_left_array[None, None, :])
            assigned_right_color = np.where(
                outer_half[..., None],
                color_left_array[None, None, :],
                color_right_array[None, None, :])
            assigned_bottom_color = np.where(
                outer_half[..., None],
                color_top_array[None, None, :],
                color_bottom_array[None, None, :])

    # ─────────────────────────────────────────────────────────────
    # 加权混合：每个像素的最终颜色 = 四条边颜色按权重加权平均。
    #
    # raised/sunken：assigned_*_color 形状为 (3,)，用 [None,None,:] 扩到 (1,1,3)
    # groove/ridge：assigned_*_color 形状为 (H,W,3)，直接参与运算
    # ─────────────────────────────────────────────────────────────
    total_weight = (
        weight_top + weight_left + weight_bottom + weight_right + 1e-9)
    if border_style in ("groove", "ridge"):
        # assigned_*_color 已经是 (H, W, 3) 数组（默认模式 groove/ridge）
        blended_color = (
            weight_top[..., None] * assigned_top_color +
            weight_left[..., None] * assigned_left_color +
            weight_right[..., None] * assigned_right_color +
            weight_bottom[..., None] * assigned_bottom_color
        ) / total_weight[..., None]
    else:
        # assigned_*_color 是 (3,) 数组，需扩展为 (1, 1, 3)
        blended_color = (
            weight_top[..., None] * assigned_top_color[None, None, :] +
            weight_left[..., None] * assigned_left_color[None, None, :] +
            weight_right[..., None] * assigned_right_color[None, None, :] +
            weight_bottom[..., None] * assigned_bottom_color[None, None, :]
        ) / total_weight[..., None]

    # ─────────────────────────────────────────────────────────────
    # 只改写边框像素（opaque_mask = True）的 RGB 通道，不改 alpha。
    # clip 防止浮点误差导致颜色值超出 [0, 255]。
    # ─────────────────────────────────────────────────────────────
    for channel in range(3):
        border_array[opaque_mask, channel] = blended_color[opaque_mask, channel]
    border_array = np.clip(border_array, 0, 255).astype(np.uint8)
    border_layer = Image.fromarray(border_array, mode="RGBA")

    # 将边框图层合成到原图上。border_layer 的 alpha 通道作为遮罩。
    image.paste(border_layer, (0, 0), border_layer)
    return image


def create_texture(width: int, height: int, wood_type: str = "oak",
                   element: str = "background", seed: int = 0,
                   angle: float = 0.0, ring_curvature: float = 0.0,
                   corner_radius: int = 0,
                   grain_amount: float = 1.0,
                   shadow_offset: tuple = None,
                   shadow_blur: int = 0,
                   shadow_opacity: int = 0,
                   border_width: int = 0,
                   border_style: str = "flat",
                   border_color: tuple = (60, 40, 20),
                   border_inset: int = 0,
                   border_widths: tuple = None,
                   corner_radii: tuple = None) -> Image.Image:
    """
    Create a wood-textured UI element.

    Parameters:
        width, height: pixel dimensions
        wood_type: one of the WOOD_TYPES keys
        element: "background", "button", "panel", "frame"
        seed: random seed for reproducible textures
        angle: grain rotation in degrees
        ring_curvature: how much growth rings curve (0 = straight)
        corner_radius: rounded corner radius in pixels
        grain_amount: 0.0 = solid flat color, 1.0 = full grain
        shadow_offset: (dx, dy) for drop shadow, or None to disable
        shadow_blur: Gaussian blur radius for shadow
        shadow_opacity: shadow alpha 0-255
        border_width: border thickness in pixels (0 = none)
        border_style: "flat", "raised", "sunken", "groove", "ridge"
        border_color: RGB tuple for border base color
        border_inset: pixels from the edge before the border starts
    """
    grain = generate_wood_grain(width, height, wood_type, seed, angle, ring_curvature,
                                grain_amount)

    img_array = (grain * 255).astype(np.uint8)
    img = Image.fromarray(img_array, mode="RGB")

    # Apply element-specific effects
    if element == "button":
        bevel_map = apply_bevel(grain[:, :, 0], width, height, 0.06, "raised")
        highlight = Image.new("RGB", (width, height), (255, 255, 255))
        img = Image.blend(img, highlight, 0.08)
        top_grad = Image.new("L", (width, height // 3), 0)
        for y in range(height // 3):
            val = int(30 * (1 - y / (height // 3)))
            top_grad.paste(val, (0, y, width, y + 1))
        top_light = Image.new("RGB", (width, height), (255, 255, 240))
        img = Image.composite(
            Image.blend(img, top_light, 0.05),
            img,
            top_grad.resize((width, height))
        )

    elif element == "panel":
        border_px = max(2, int(min(width, height) * 0.015))
        draw = ImageDraw.Draw(img)
        for i in range(border_px):
            alpha = int(40 - i * (40 / border_px))
            if alpha > 0:
                draw.rectangle(
                    [i, i, width - 1 - i, height - 1 - i],
                    outline=(0, 0, 0, alpha),
                    width=1
                )

    elif element == "frame":
        border_px = max(4, int(min(width, height) * 0.05))
        draw = ImageDraw.Draw(img)
        draw.rectangle(
            [0, 0, width - 1, height - 1],
            outline=(40, 30, 20),
            width=border_px
        )
        draw.rectangle(
            [border_px, border_px, width - 1 - border_px, height - 1 - border_px],
            outline=(180, 160, 130),
            width=max(1, border_px // 3)
        )

    # Convert to RGBA before border (border drawn on full rectangle)
    img = img.convert("RGBA")

    # Draw configurable border BEFORE mask (border covers full rectangle)
    if border_width > 0:
        img = draw_border(img, border_width, border_style, border_color,
                          corner_radius, border_inset,
                          border_widths=border_widths,
                          corner_radii=corner_radii)

    # Apply rounded corners AFTER border — cuts through both wood and border
    mask = Image.new("L", (width, height), 0)
    mask_draw = ImageDraw.Draw(mask)
    if corner_radii is not None:
        cr_tl, cr_tr, cr_bl, cr_br = corner_radii
        # 先填满整个矩形
        mask_draw.rectangle([0, 0, width - 1, height - 1], fill=255)
        # 对每个角：切掉正方形角，再补回四分之一圆
        corners_spec = [
            (cr_tl, [0, 0, cr_tl, cr_tl],
             [0, 0, cr_tl * 2, cr_tl * 2], 180, 270),
            (cr_tr, [width - 1 - cr_tr, 0, width - 1, cr_tr],
             [width - 1 - cr_tr * 2, 0, width - 1, cr_tr * 2], 270, 360),
            (cr_bl, [0, height - 1 - cr_bl, cr_bl, height - 1],
             [0, height - 1 - cr_bl * 2, cr_bl * 2, height - 1], 90, 180),
            (cr_br, [width - 1 - cr_br, height - 1 - cr_br, width - 1, height - 1],
             [width - 1 - cr_br * 2, height - 1 - cr_br * 2,
              width - 1, height - 1], 0, 90),
        ]
        for cr_val, square_bbox, arc_bbox, start_a, end_a in corners_spec:
            if cr_val <= 0:
                continue
            # 切掉正方形角
            mask_draw.rectangle(square_bbox, fill=0)
            # 补回四分之一圆
            mask_draw.pieslice(arc_bbox, start_a, end_a, fill=255)
        effective_cr = max(cr_tl, cr_tr, cr_bl, cr_br)
    elif corner_radius > 0:
        mask_draw.rounded_rectangle(
            [0, 0, width - 1, height - 1],
            radius=corner_radius, fill=255)
        effective_cr = corner_radius
    else:
        effective_cr = 0
    if effective_cr > 0:
        img.putalpha(mask)

    # Store corner radius for shadow use
    img.corner_radius = effective_cr

    # Composite drop shadow (expands canvas)
    if shadow_offset is not None and shadow_opacity > 0:
        img = draw_drop_shadow(img, shadow_offset[0], shadow_offset[1],
                               shadow_blur, shadow_opacity)

    return img


def main():
    parser = argparse.ArgumentParser(
        description="Generate wooden UI textures for games."
    )
    parser.add_argument("-W", "--width", type=int, default=256,
                        help="Texture width in pixels (default: 256)")
    parser.add_argument("-H", "--height", type=int, default=256,
                        help="Texture height in pixels (default: 256)")
    parser.add_argument("-t", "--type", default="oak",
                        choices=list(WOOD_TYPES.keys()),
                        help="Wood type (default: oak)")
    parser.add_argument("-e", "--element", default="background",
                        choices=["background", "button", "panel", "frame"],
                        help="UI element type (default: background)")
    parser.add_argument("-o", "--output", default=None,
                        help="Output file path (default: auto-generated name)")
    parser.add_argument("-s", "--seed", type=int, default=None,
                        help="Random seed for reproducibility")
    parser.add_argument("-a", "--angle", type=float, default=0.0,
                        help="Grain rotation angle in degrees")
    parser.add_argument("-c", "--curvature", type=float, default=0.0,
                        help="Growth ring curvature (0=straight, 0.02=slight curve)")
    parser.add_argument("-r", "--corner-radius", type=int, default=0,
                        help="Corner radius for rounded corners")
    parser.add_argument("-g", "--grain-amount", type=float, default=1.0,
                        help="Grain visibility 0.0–1.0 (default: 1.0)")
    # Shadow
    parser.add_argument("--shadow-offset-x", type=int, default=0,
                        help="Drop shadow X offset (default: 0, shadow disabled)")
    parser.add_argument("--shadow-offset-y", type=int, default=0,
                        help="Drop shadow Y offset (default: 0)")
    parser.add_argument("--shadow-blur", type=int, default=6,
                        help="Drop shadow blur radius (default: 6)")
    parser.add_argument("--shadow-opacity", type=int, default=0,
                        help="Drop shadow opacity 0-255 (default: 0, shadow disabled)")
    # Border
    parser.add_argument("--border-width", type=int, default=0,
                        help="Border thickness in pixels (default: 0)")
    parser.add_argument("--border-style", default="flat",
                        choices=["flat", "raised", "sunken", "groove", "ridge"],
                        help="Border style (default: flat)")
    parser.add_argument("--border-color", default="60,40,20",
                        help="Border RGB color as R,G,B (default: 60,40,20)")
    parser.add_argument("--border-inset", type=int, default=0,
                        help="Border offset from edge in pixels (default: 0)")
    parser.add_argument("--list-types", action="store_true",
                        help="List available wood types and exit")
    parser.add_argument("--batch", action="store_true",
                        help="Generate all wood types for all element types")

    args = parser.parse_args()

    if args.list_types:
        print("Available wood types:")
        for name, info in WOOD_TYPES.items():
            print(f"  {name}: base=RGB{info['base']}, ring_spacing={info['ring_spacing']}")
        return

    seed = args.seed if args.seed is not None else random.randint(0, 999999)

    # Parse shadow
    shadow_off = (args.shadow_offset_x, args.shadow_offset_y)
    use_shadow = args.shadow_opacity > 0

    # Parse border color
    border_c = tuple(int(x.strip()) for x in args.border_color.split(","))

    if args.batch:
        output_dir = args.output or "output"
        os.makedirs(output_dir, exist_ok=True)
        for wood in WOOD_TYPES:
            for elem in ["background", "button", "panel", "frame"]:
                w, h = 256, 256
                if elem == "button":
                    w, h = 200, 60
                elif elem == "frame":
                    w, h = 256, 32
                elif elem == "panel":
                    w, h = 400, 300

                img = create_texture(
                    w, h, wood_type=wood, element=elem, seed=seed,
                    grain_amount=args.grain_amount,
                    shadow_offset=shadow_off if use_shadow else None,
                    shadow_blur=args.shadow_blur,
                    shadow_opacity=args.shadow_opacity,
                    border_width=args.border_width,
                    border_style=args.border_style,
                    border_color=border_c,
                    border_inset=args.border_inset,
                )
                path = os.path.join(output_dir, f"wood_{wood}_{elem}.png")
                img.save(path)
                print(f"Saved: {path}")
        print(f"\nAll textures saved to: {output_dir}/")
    else:
        img = create_texture(
            args.width, args.height,
            wood_type=args.type,
            element=args.element,
            seed=seed,
            angle=args.angle,
            ring_curvature=args.curvature,
            corner_radius=args.corner_radius,
            grain_amount=args.grain_amount,
            shadow_offset=shadow_off if use_shadow else None,
            shadow_blur=args.shadow_blur,
            shadow_opacity=args.shadow_opacity,
            border_width=args.border_width,
            border_style=args.border_style,
            border_color=border_c,
            border_inset=args.border_inset,
        )

        output = args.output or f"wood_{args.type}_{args.element}_{args.width}x{args.height}.png"
        img.save(output)
        print(f"Saved: {output} ({img.width}x{img.height}, seed={seed})")


if __name__ == "__main__":
    main()

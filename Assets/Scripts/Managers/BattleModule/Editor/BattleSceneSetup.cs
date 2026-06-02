using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 精灵表切片工具 + 场景搭建。
/// </summary>
public static class BattleSceneSetup
{
    private const int FRAME_COLS = 4;
    private const int FRAME_ROWS = 2;

    public static void SetupBattleSceneFull()
    {
        SliceAllSpriteSheets();
        SetupBattleScene();
    }

    public static void SetupBattleScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "BattleScene";

        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0, 0, -10);
        camGo.tag = "MainCamera";

        var bgGo = new GameObject("Background");
        bgGo.transform.position = Vector3.zero;
        bgGo.AddComponent<SpriteRenderer>();

        var fieldGo = new GameObject("BattleField");

        var ctrlGo = new GameObject("BattleController");
        ctrlGo.AddComponent<BattleDriver>();
        ctrlGo.AddComponent<BattleSceneBootstrapper>();

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        var bootstrapper = ctrlGo.GetComponent<BattleSceneBootstrapper>();
        var bgRenderer = bgGo.GetComponent<SpriteRenderer>();

        typeof(BattleSceneBootstrapper)
            .GetField("_backgroundRenderer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(bootstrapper, bgRenderer);
        typeof(BattleSceneBootstrapper)
            .GetField("_unitParent",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(bootstrapper, fieldGo.transform);

        string scenePath = "Assets/Scenes/BattleScene.unity";
        string dir = Path.GetDirectoryName(scenePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        EditorSceneManager.SaveScene(scene, scenePath);

        Debug.Log($"[Setup] 场景已保存: {scenePath}");
    }

    // ==================== 精灵表切片 ====================

    public static void SliceAllSpriteSheets()
    {
        string[] dirs = {
            "Assets/Resources/Art/Sprites/Units",
            "Assets/Resources/Art/Sprites/Skills",
            "Assets/Resources/Art/Sprites/Effects"
        };
        int count = 0;

        foreach (string dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            string[] files = Directory.GetFiles(dir, "*.png");
            foreach (string file in files)
            {
                if (SliceSpriteSheet(file, FRAME_COLS, FRAME_ROWS))
                    count++;
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[Slice] 已处理 {count} 张精灵表 ({FRAME_COLS}x{FRAME_ROWS} 网格)");
    }

    private static bool SliceSpriteSheet(string assetPath, int cols, int rows)
    {
        assetPath = assetPath.Replace("\\", "/");
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return false;

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (tex == null) return false;

        int frameW = tex.width / cols;
        int frameH = tex.height / rows;
        if (frameW == 0 || frameH == 0) return false;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        string baseName = Path.GetFileNameWithoutExtension(assetPath);
        var metas = new SpriteMetaData[cols * rows];
        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                metas[idx] = new SpriteMetaData
                {
                    name = $"{baseName}_{idx}",
                    rect = new Rect(c * frameW, (rows - 1 - r) * frameH, frameW, frameH),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };
                idx++;
            }
        }
        importer.spritesheet = metas;
        importer.SaveAndReimport();
        return true;
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDemo.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键搭建战斗场景 + 精灵表切片 + AnimationCache 构建。
/// </summary>
public static class BattleSceneSetup
{
    private const int FRAME_COLS = 4;
    private const int FRAME_ROWS = 2;

    // ==================== 主菜单项 ====================

    [MenuItem("GameDemo/Setup Battle Scene (Full)")]
    public static void SetupBattleSceneFull()
    {
        SliceAllSpriteSheets();
        BuildAnimationCache();
        SetupBattleScene();
    }

    // ==================== 场景搭建 ====================

    [MenuItem("GameDemo/Step 1: Setup Scene")]
    public static void SetupBattleScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "BattleScene";

        // Camera
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0, 0, -10);
        camGo.tag = "MainCamera";

        // Background
        var bgGo = new GameObject("Background");
        bgGo.transform.position = Vector3.zero;
        bgGo.AddComponent<SpriteRenderer>();

        // Unit Field (父节点，用于容纳动态创建的 UnitView)
        var fieldGo = new GameObject("BattleField");

        // BattleController
        var ctrlGo = new GameObject("BattleController");
        ctrlGo.AddComponent<BattleDriver>();
        ctrlGo.AddComponent<BattleSceneBootstrapper>();

        // Canvas
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

        // Config
        var config = CreateDefaultConfig();

        // Bind references
        var bootstrapper = ctrlGo.GetComponent<BattleSceneBootstrapper>();
        var bgRenderer = bgGo.GetComponent<SpriteRenderer>();
        var cache = LoadOrCreateCache();

        SetPrivateField(bootstrapper, "_config", config);
        SetPrivateField(bootstrapper, "_backgroundRenderer", bgRenderer);
        SetPrivateField(bootstrapper, "_animationCache", cache);
        SetPrivateField(bootstrapper, "_unitParent", fieldGo.transform);

        // Save
        string scenePath = "Assets/Scenes/BattleScene.unity";
        EnsureDirectory(scenePath);
        EditorSceneManager.SaveScene(scene, scenePath);

        Debug.Log($"[Setup] 场景已保存: {scenePath}\n" +
                  "  下一步：选择 Config 表填入背景图，然后点 Play");
        Selection.activeObject = config;
    }

    // ==================== 精灵表切片 ====================

    [MenuItem("GameDemo/Step 2: Slice Sprite Sheets")]
    public static void SliceAllSpriteSheets()
    {
        string[] dirs = {
            "Assets/Resources/Art/Sprites/Units",
            "Assets/Resources/Art/Sprites/Skills"
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

    // ==================== AnimationCache 构建 ====================

    [MenuItem("GameDemo/Step 3: Build Animation Cache")]
    public static void BuildAnimationCache()
    {
        var cache = LoadOrCreateCache();
        var entries = new List<AnimationCache.Entry>();

        string[] dirs = {
            "Assets/Resources/Art/Sprites/Units",
            "Assets/Resources/Art/Sprites/Skills"
        };

        foreach (string dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            string[] files = Directory.GetFiles(dir, "*.png");
            foreach (string file in files)
            {
                string assetPath = file.Replace("\\", "/");
                string baseName = Path.GetFileNameWithoutExtension(assetPath);

                // 转换为 Resources 路径：去掉 "Assets/Resources/" 前缀和扩展名
                string resourcesPath = assetPath
                    .Replace("Assets/Resources/", "")
                    .Replace(".png", "");

                entries.Add(new AnimationCache.Entry
                {
                    Key = baseName,
                    Path = resourcesPath
                });
            }
        }

        cache.SetEntries(entries.ToArray());
        EditorUtility.SetDirty(cache);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Cache] AnimationCache 已更新，共 {entries.Count} 个条目（路径模式）");
    }

    // ==================== 辅助 ====================

    private static AnimationCache LoadOrCreateCache()
    {
        const string cachePath = "Assets/ScriptableObjects/Battle/AnimationCache.asset";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationCache>(cachePath);
        if (existing != null) return existing;

        EnsureDirectory(cachePath);
        var cache = ScriptableObject.CreateInstance<AnimationCache>();
        AssetDatabase.CreateAsset(cache, cachePath);
        AssetDatabase.SaveAssets();
        return cache;
    }

    private static BattleSceneConfig CreateDefaultConfig()
    {
        var config = ScriptableObject.CreateInstance<BattleSceneConfig>();
        config.BackgroundPath = "Art/Backgrounds/battle_grassland";

        config.PlayerUnits = new BattleUnitConfig[]
        {
            new()
            {
                Id = "p_warrior", DisplayName = "战士",
                Attack = 80, Defense = 30, HP = 500, Speed = 60, Mana = 100,
                Row = 1, Col = 0, InitialCost = 100,
                Skills = new SkillConfig[] {
                    new() { Id = "attack", DisplayName = "攻击",
                        SkillType = "SingleAttack", TargetType = "SingleEnemy",
                        PerformanceFxId = "fx_slash" }
                }
            },
            new()
            {
                Id = "p_mage", DisplayName = "法师",
                Attack = 120, Defense = 15, HP = 300, Speed = 40, Mana = 200,
                Row = 0, Col = 1, InitialCost = 120,
                Skills = new SkillConfig[] {
                    new() { Id = "poison", DisplayName = "毒刃",
                        SkillType = "SingleAttack", TargetType = "SingleEnemy",
                        PerformanceFxId = "fx_poison" }
                }
            },
            new()
            {
                Id = "p_priest", DisplayName = "牧师",
                Attack = 50, Defense = 20, HP = 350, Speed = 50, Mana = 150,
                Row = 2, Col = 1, InitialCost = 110,
                Skills = new SkillConfig[] {
                    new() { Id = "stun", DisplayName = "眩晕",
                        SkillType = "SingleAttack", TargetType = "SingleEnemy",
                        PerformanceFxId = "fx_stun" }
                }
            },
        };

        config.EnemyUnits = new BattleUnitConfig[]
        {
            new()
            {
                Id = "e_goblin", DisplayName = "哥布林",
                Attack = 60, Defense = 10, HP = 200, Speed = 70, Mana = 50,
                Row = 1, Col = 1, InitialCost = 100,
                Skills = new SkillConfig[] {
                    new() { Id = "attack", DisplayName = "攻击",
                        SkillType = "SingleAttack", TargetType = "SingleEnemy",
                        PerformanceFxId = "fx_slash" }
                }
            },
            new()
            {
                Id = "e_archer", DisplayName = "哥布林射手",
                Attack = 70, Defense = 8, HP = 180, Speed = 80, Mana = 50,
                Row = 0, Col = 2, InitialCost = 100,
                Skills = new SkillConfig[] {
                    new() { Id = "attack", DisplayName = "攻击",
                        SkillType = "SingleAttack", TargetType = "SingleEnemy",
                        PerformanceFxId = "fx_arrow" }
                }
            },
            new()
            {
                Id = "e_troll", DisplayName = "巨魔",
                Attack = 100, Defense = 40, HP = 600, Speed = 30, Mana = 80,
                Row = 1, Col = 2, InitialCost = 130,
                Skills = new SkillConfig[] {
                    new() { Id = "attack", DisplayName = "攻击",
                        SkillType = "SingleAttack", TargetType = "SingleEnemy",
                        PerformanceFxId = "fx_slash" }
                }
            },
        };

        string path = "Assets/ScriptableObjects/Battle/DefaultBattleConfig.asset";
        EnsureDirectory(path);
        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        return config;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(target, value);
    }

    private static void EnsureDirectory(string assetPath)
    {
        string dir = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
}

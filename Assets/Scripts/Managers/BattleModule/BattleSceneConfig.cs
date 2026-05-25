using GameDemo.Battle;
using UnityEngine;

/// <summary>
/// 战斗场景配置表（ScriptableObject）。
/// 右键 Project 窗口 → Create → Battle → Scene Config 创建。
/// </summary>
[CreateAssetMenu(menuName = "Battle/Scene Config", fileName = "BattleSceneConfig")]
public class BattleSceneConfig : ScriptableObject
{
    [Header("场景")]
    [Tooltip("背景图 Resources 路径，如 Art/Backgrounds/battle_grassland")]
    public string BackgroundPath = "Art/Backgrounds/battle_grassland";

    [Header("我方配置")]
    public BattleUnitConfig[] PlayerUnits;

    [Header("敌方配置")]
    public BattleUnitConfig[] EnemyUnits;
}

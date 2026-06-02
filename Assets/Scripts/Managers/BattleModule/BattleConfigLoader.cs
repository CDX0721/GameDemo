using System.Collections.Generic;
using UnityEngine;

namespace GameDemo.Battle
{
    public static class BattleConfigLoader
    {
        public static (BattleFieldDef field, List<UnitPlacementDef> playerUnits, Dictionary<string, BattleUnitDef> units) Load(string fieldKey)
        {
            var fieldsAsset = AssetManager.Instance.Load<TextAsset>("Configs/Battle/BattleFields");
            if (fieldsAsset == null)
            {
                Debug.LogError("[BattleConfigLoader] BattleFields.json not found in Resources/Configs/Battle/");
                return (null, null, null);
            }
            var fieldsRoot = JsonUtility.FromJson<BattleFieldsRoot>(fieldsAsset.text);
            var field = fieldsRoot.TestBattleFiled;

            var playerAsset = AssetManager.Instance.Load<TextAsset>("Configs/Battle/PlayerFormation");
            List<UnitPlacementDef> playerUnits = null;
            if (playerAsset != null)
            {
                var playerRoot = JsonUtility.FromJson<PlayerFormationRoot>(playerAsset.text);
                playerUnits = playerRoot.PlayerUnits;
            }
            if (playerUnits == null)
            {
                Debug.LogError("[BattleConfigLoader] PlayerFormation.json not found or empty.");
                return (field, null, null);
            }

            var unitsAsset = AssetManager.Instance.Load<TextAsset>("Configs/Battle/BattleUnits");
            if (unitsAsset == null)
            {
                Debug.LogError("[BattleConfigLoader] BattleUnits.json not found in Resources/Configs/Battle/");
                return (field, playerUnits, null);
            }
            var unitsRoot = JsonUtility.FromJson<BattleUnitsRoot>(unitsAsset.text);
            var units = unitsRoot.BattleUnits.ToDict();

            return (field, playerUnits, units);
        }
    }
}

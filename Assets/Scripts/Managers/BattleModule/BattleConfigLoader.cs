using System.Collections.Generic;
using UnityEngine;

namespace GameDemo.Battle
{
    public static class BattleConfigLoader
    {
        public static (BattleFieldDef field, Dictionary<string, BattleUnitDef> units) Load(string fieldKey)
        {
            var fieldsAsset = Resources.Load<TextAsset>("Configs/Battle/BattleFields");
            if (fieldsAsset == null)
            {
                Debug.LogError("[BattleConfigLoader] BattleFields.json not found in Resources/Configs/Battle/");
                return (null, null);
            }
            var fieldsRoot = JsonUtility.FromJson<BattleFieldsRoot>(fieldsAsset.text);
            var field = fieldsRoot.TestBattleFiled;

            var unitsAsset = Resources.Load<TextAsset>("Configs/Battle/BattleUnits");
            if (unitsAsset == null)
            {
                Debug.LogError("[BattleConfigLoader] BattleUnits.json not found in Resources/Configs/Battle/");
                return (field, null);
            }
            var unitsRoot = JsonUtility.FromJson<BattleUnitsRoot>(unitsAsset.text);
            var units = unitsRoot.BattleUnits.ToDict();

            return (field, units);
        }
    }
}

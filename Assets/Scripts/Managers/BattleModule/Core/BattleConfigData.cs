using System;
using System.Collections.Generic;

namespace GameDemo.Battle
{
    // ==================== 背景布局设置（按背景ID索引） ====================

    [Serializable]
    public class BattleBackgroundSettingsEntry
    {
        public string img;
        public PixelPoint PlayerCenterPixel;
        public PixelPoint EnemyCenterPixel;
        public float RowSpacingPixel;
        public float ColSpacingPixel;
    }

    [Serializable]
    public class PixelPoint
    {
        public float x;
        public float y;
    }

    // ==================== 战斗字段配置 ====================

    [Serializable]
    public class BattleFieldsRoot
    {
        public BattleFieldDef TestBattleFiled;
    }

    [Serializable]
    public class BattleFieldDef
    {
        public string BackGround;
        public string BGM;
        public List<UnitPlacementDef> EnemyUnits;
    }

    [Serializable]
    public class PlayerFormationRoot
    {
        public List<UnitPlacementDef> PlayerUnits;
    }

    // ==================== BGM 设置 ====================

    [Serializable]
    public class BGMSettingsEntry
    {
        public string music;
        public string loop_in;
        public string loop_out;
        public string volume_offset;
        public int frame_rate;
    }

    [Serializable]
    public class UnitPlacementDef
    {
        public string id;
        public int row;
        public int col;
        public float initialCost;
        public List<string> attachSkills;
    }

    [Serializable]
    public class BattleUnitsRoot
    {
        public BattleUnitDict BattleUnits;
    }

    [Serializable]
    public class BattleUnitDict
    {
        public BattleUnitDef OrangeDog;
        public BattleUnitDef Cirno;
        public BattleUnitDef Flandre;
        public BattleUnitDef Sans;

        public Dictionary<string, BattleUnitDef> ToDict()
        {
            var d = new Dictionary<string, BattleUnitDef>();
            void Add(string k, BattleUnitDef v)
            {
                if (v != null) { v.Id = k; d[k] = v; }
            }
            Add("OrangeDog", OrangeDog);
            Add("Cirno", Cirno);
            Add("Flandre", Flandre);
            Add("Sans", Sans);
            return d;
        }
    }

    [Serializable]
    public class BattleUnitDef
    {
        public string Id;
        public string DisplayName;
        public float Attack;
        public float Defense;
        public float HP;
        public float Mana;
        public float Speed;
        public List<string> InnateSills;
        public string IdleAnimation;
        public string AttackAnimation;
        public string ControlType;
    }
}

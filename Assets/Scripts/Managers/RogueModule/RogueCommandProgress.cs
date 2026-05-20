using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GameDemo.Rogue
{
    [Serializable]
    public sealed class RogueCommandLevelRule
    {
        public int level;
        public int xpRequired;
        public int initialHopeBonus;
        public int initialHpBonus;
        public int initialIngotsBonus;
        public float shopDiscount;
        public List<string> unlockContent = new List<string>();
    }

    [Serializable]
    public sealed class RogueCommandProgressState
    {
        public int level = 1;
        public int xp;
        public List<string> unlockedContent = new List<string>();
    }

    [Serializable]
    public sealed class RogueCommandProgressSnapshot
    {
        public RogueCommandProgressState state = new RogueCommandProgressState();
    }

    public static class RogueCommandProgressService
    {
        static readonly List<RogueCommandLevelRule> _rules = new List<RogueCommandLevelRule>
        {
            new RogueCommandLevelRule { level = 1, xpRequired = 0, initialHopeBonus = 0, initialHpBonus = 0, initialIngotsBonus = 0, shopDiscount = 0f },
            new RogueCommandLevelRule { level = 2, xpRequired = 100, initialHopeBonus = 1, initialHpBonus = 0, initialIngotsBonus = 0, shopDiscount = 0.05f, unlockContent = new List<string> { "f2_operator_pool" } },
            new RogueCommandLevelRule { level = 3, xpRequired = 150, initialHopeBonus = 0, initialHpBonus = 0, initialIngotsBonus = 3, shopDiscount = 0f, unlockContent = new List<string> { "extra_event_option" } },
            new RogueCommandLevelRule { level = 4, xpRequired = 200, initialHopeBonus = 0, initialHpBonus = 1, initialIngotsBonus = 0, shopDiscount = 0f, unlockContent = new List<string> { "all_jobs_pool", "free_refresh_1" } },
            new RogueCommandLevelRule { level = 5, xpRequired = 250, initialHopeBonus = 0, initialHpBonus = 0, initialIngotsBonus = 0, shopDiscount = 0f, unlockContent = new List<string> { "hidden_floor_common" } },
            new RogueCommandLevelRule { level = 6, xpRequired = 300, initialHopeBonus = 3, initialHpBonus = 2, initialIngotsBonus = 0, shopDiscount = 0.1f, unlockContent = new List<string> { "legend_pity_minus_3", "start_curio_plus_1" } },
        };

        static RogueCommandProgressState _state;
        static string _savePath;

        public static RogueCommandProgressState State => EnsureLoaded();

        public static void Configure(string savePath)
        {
            _savePath = savePath;
        }

        public static void AddExperience(int xp)
        {
            RogueCommandProgressState state = EnsureLoaded();
            if (xp <= 0)
            {
                return;
            }

            state.xp += xp;
            RecalculateLevel(state);
            Save();
        }

        public static RogueCommandLevelRule GetCurrentRule()
        {
            RogueCommandProgressState state = EnsureLoaded();
            return GetRule(state.level);
        }

        public static int GetInitialHopeBonus() => GetCurrentRule().initialHopeBonus;
        public static int GetInitialHpBonus() => GetCurrentRule().initialHpBonus;
        public static int GetInitialIngotsBonus() => GetCurrentRule().initialIngotsBonus;
        public static float GetShopDiscount() => GetCurrentRule().shopDiscount;

        public static bool HasUnlocked(string contentId)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                return false;
            }

            return EnsureLoaded().unlockedContent.Contains(contentId);
        }

        public static void CommitRun(int xpGained)
        {
            AddExperience(xpGained);
        }

        static RogueCommandLevelRule GetRule(int level)
        {
            RogueCommandLevelRule rule = _rules[0];
            for (int i = 0; i < _rules.Count; i++)
            {
                if (_rules[i].level <= level)
                {
                    rule = _rules[i];
                }
            }
            return rule;
        }

        static void RecalculateLevel(RogueCommandProgressState state)
        {
            int level = 1;
            for (int i = 0; i < _rules.Count; i++)
            {
                if (state.xp >= _rules[i].xpRequired)
                {
                    level = _rules[i].level;
                }
            }

            state.level = level;
            state.unlockedContent.Clear();
            for (int i = 0; i < _rules.Count; i++)
            {
                if (_rules[i].level <= state.level && _rules[i].unlockContent != null)
                {
                    for (int j = 0; j < _rules[i].unlockContent.Count; j++)
                    {
                        string unlock = _rules[i].unlockContent[j];
                        if (!state.unlockedContent.Contains(unlock))
                        {
                            state.unlockedContent.Add(unlock);
                        }
                    }
                }
            }
        }

        static RogueCommandProgressState EnsureLoaded()
        {
            if (_state != null)
            {
                return _state;
            }

            if (!string.IsNullOrWhiteSpace(_savePath) && File.Exists(_savePath))
            {
                string json = File.ReadAllText(_savePath);
                RogueCommandProgressSnapshot snapshot = JsonUtility.FromJson<RogueCommandProgressSnapshot>(json);
                _state = snapshot != null && snapshot.state != null ? snapshot.state : new RogueCommandProgressState();
            }
            else
            {
                _state = new RogueCommandProgressState();
            }

            RecalculateLevel(_state);
            return _state;
        }

        static void Save()
        {
            if (string.IsNullOrWhiteSpace(_savePath))
            {
                _savePath = Path.Combine(Application.persistentDataPath, "rogue_command_progress.json");
            }

            var snapshot = new RogueCommandProgressSnapshot { state = EnsureLoaded() };
            string json = JsonUtility.ToJson(snapshot, true);
            Directory.CreateDirectory(Path.GetDirectoryName(_savePath) ?? ".");
            File.WriteAllText(_savePath, json);
        }
    }
}

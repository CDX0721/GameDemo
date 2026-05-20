using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace GameDemo.Rogue
{
    [Serializable]
    public sealed class RogueSquadUnitPayload
    {
        public string op_id;
        public int promote_lvl;
        public int skill_idx;
        public int hope_cost;
        public int potential;
    }

    [Serializable]
    public sealed class RogueModifierPayload
    {
        public string id;
        public float value;
    }

    [Serializable]
    public sealed class RogueGlobalStatePayload
    {
        public int current_hp;
        public int hope_remaining;
        public int ingots;
        public List<string> active_curios = new List<string>();
        public List<string> flags = new List<string>();
        public List<RogueModifierPayload> modifiers = new List<RogueModifierPayload>();
    }

    [Serializable]
    public sealed class RogueBattleEntryRequest
    {
        public string run_id;
        public int floor_index;
        public string node_id;
        public int map_seed;
        public List<RogueSquadUnitPayload> squad = new List<RogueSquadUnitPayload>();
        public RogueGlobalStatePayload global_state = new RogueGlobalStatePayload();
        public string difficulty = "normal";
        public string client_checksum;
    }

    [Serializable]
    public sealed class RogueSurvivingUnitPayload
    {
        public string op_id;
        public float hp_pct;
        public int sp;
    }

    [Serializable]
    public sealed class RogueBattleRewardsPayload
    {
        public int hope_drop;
        public int ingot_drop;
        public List<string> curio_granted = new List<string>();
        public int exp_drop;
    }

    [Serializable]
    public sealed class RogueStateDeltaPayload
    {
        public int hp_change;
        public int hope_change;
        public int ingots_change;
        public List<string> curios_added = new List<string>();
        public List<string> flags_set = new List<string>();
    }

    [Serializable]
    public sealed class RogueBattleResultResponse
    {
        public string result;
        public int battle_duration;
        public List<RogueSurvivingUnitPayload> surviving_units = new List<RogueSurvivingUnitPayload>();
        public RogueBattleRewardsPayload rewards = new RogueBattleRewardsPayload();
        public RogueStateDeltaPayload state_delta = new RogueStateDeltaPayload();
        public string server_checksum;
    }

    public static class RogueProtocolChecksum
    {
        public static string BuildClientChecksum(RogueBattleEntryRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var sb = new StringBuilder();
            sb.Append(request.run_id).Append('|')
              .Append(request.node_id).Append('|')
              .Append(request.map_seed).Append('|')
              .Append(request.global_state.current_hp).Append('|')
              .Append(request.global_state.hope_remaining).Append('|')
              .Append(request.global_state.ingots).Append('|');

            for (int i = 0; i < request.squad.Count; i++)
            {
                RogueSquadUnitPayload u = request.squad[i];
                sb.Append(u.op_id).Append(':')
                  .Append(u.promote_lvl).Append(':')
                  .Append(u.skill_idx).Append(':')
                  .Append(u.hope_cost).Append(':')
                  .Append(u.potential).Append(';');
            }

            for (int i = 0; i < request.global_state.active_curios.Count; i++)
            {
                sb.Append(request.global_state.active_curios[i]).Append(';');
            }

            for (int i = 0; i < request.global_state.modifiers.Count; i++)
            {
                RogueModifierPayload modifier = request.global_state.modifiers[i];
                sb.Append(modifier.id).Append(':').Append(modifier.value.ToString("R")).Append(';');
            }

            return Sha256(sb.ToString());
        }

        public static string BuildServerChecksum(RogueBattleResultResponse response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            var sb = new StringBuilder();
            sb.Append(response.result).Append('|')
              .Append(response.battle_duration).Append('|')
              .Append(response.rewards.hope_drop).Append('|')
              .Append(response.rewards.ingot_drop).Append('|')
              .Append(response.rewards.exp_drop).Append('|')
              .Append(response.state_delta.hp_change).Append('|')
              .Append(response.state_delta.hope_change).Append('|')
              .Append(response.state_delta.ingots_change).Append('|');

            for (int i = 0; i < response.state_delta.curios_added.Count; i++)
            {
                sb.Append(response.state_delta.curios_added[i]).Append(';');
            }
            for (int i = 0; i < response.state_delta.flags_set.Count; i++)
            {
                sb.Append(response.state_delta.flags_set[i]).Append(';');
            }

            return Sha256(sb.ToString());
        }

        public static bool VerifyServerChecksum(RogueBattleResultResponse response)
        {
            string expected = BuildServerChecksum(response);
            return string.Equals(expected, response.server_checksum, StringComparison.OrdinalIgnoreCase);
        }

        static string Sha256(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}

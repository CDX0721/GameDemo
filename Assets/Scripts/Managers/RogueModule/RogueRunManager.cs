using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace GameDemo.Rogue
{
    public sealed class RogueRunManager
    {
        static readonly Lazy<RogueRunManager> _instance =
            new Lazy<RogueRunManager>(() => new RogueRunManager(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static RogueRunManager Instance => _instance.Value;

        readonly Dictionary<string, RogueNode> _nodeIndex = new Dictionary<string, RogueNode>();
        readonly HashSet<string> _visited = new HashSet<string>();
        readonly Dictionary<string, RogueShopState> _shopStates = new Dictionary<string, RogueShopState>();
        readonly Dictionary<string, RogueEventDefinition> _eventDefinitions = new Dictionary<string, RogueEventDefinition>();
        readonly List<RogueShopItem> _shopPool = new List<RogueShopItem>();

        RogueRunConfig _config;
        RogueMap _map;
        RogueRunState _state;
        string _savePath;
        System.Random _rng;

        public event Action<RogueRunState> OnRunStarted;
        public event Action<RogueNode> OnNodeEntered;
        public event Action<RogueNode, RogueNodeResult> OnNodeResolved;
        public event Action<RogueRunState> OnRunEnded;

        public RogueRunConfig Config => _config;
        public RogueMap Map => _map;
        public RogueRunState State => _state;
        public bool IsRunning => _state != null && _state.IsRunning;

        RogueRunManager() { }

        public void ConfigurePersistence(string savePath)
        {
            _savePath = savePath;
            if (!string.IsNullOrWhiteSpace(savePath))
            {
                string progressPath = Path.Combine(
                    Path.GetDirectoryName(savePath) ?? string.Empty,
                    "rogue_command_progress.json");
                RogueCommandProgressService.Configure(progressPath);
            }
        }

        public void ConfigureEvents(IReadOnlyList<RogueEventDefinition> definitions)
        {
            _eventDefinitions.Clear();
            if (definitions == null) return;
            for (int i = 0; i < definitions.Count; i++)
            {
                RogueEventDefinition d = definitions[i];
                if (d == null || string.IsNullOrWhiteSpace(d.eventId)) continue;
                _eventDefinitions[d.eventId] = d;
            }
        }

        public void ConfigureShopPool(IReadOnlyList<RogueShopItem> shopPool)
        {
            _shopPool.Clear();
            if (shopPool == null) return;
            for (int i = 0; i < shopPool.Count; i++)
            {
                RogueShopItem item = shopPool[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.itemId))
                {
                    _shopPool.Add(item);
                }
            }
        }

        public RogueRunState StartNewRun(RogueRunConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            int seed = config.seed != 0 ? config.seed : Environment.TickCount;
            _rng = new System.Random(seed);
            _map = RogueMapGenerator.Generate(config, _rng);
            _config = config;
            _shopStates.Clear();

            int hopeBonus = RogueCommandProgressService.GetInitialHopeBonus();
            int hpBonus = RogueCommandProgressService.GetInitialHpBonus();
            int ingotBonus = RogueCommandProgressService.GetInitialIngotsBonus();

            _state = new RogueRunState
            {
                runId = Guid.NewGuid().ToString("N"),
                version = config.runVersion,
                seed = seed,
                status = RogueRunStatus.InProgress,
                currentFloor = 0,
                currentNodeId = _map.startNodeId,
                hope = config.startingHope + hopeBonus,
                ingots = config.startingIngots + ingotBonus,
                health = config.startingHealth + hpBonus,
                maxHealth = config.startingMaxHealth + hpBonus,
                keys = 0,
            };

            BuildIndex();
            _visited.Clear();
            _visited.Add(_state.currentNodeId);
            _state.visitedNodeIds.Clear();
            _state.visitedNodeIds.Add(_state.currentNodeId);
            _state.ClampHealth();

            AutoSave();
            OnRunStarted?.Invoke(_state);
            return _state;
        }

        public bool TryResumeFromLastSave(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(_savePath))
            {
                error = "save path is not configured";
                return false;
            }

            if (!RogueRunPersistence.TryLoad(_savePath, out RogueRunSnapshot snapshot, out error))
            {
                return false;
            }

            LoadSnapshot(snapshot);
            return true;
        }

        public IReadOnlyList<RogueNode> GetAvailableNextNodes()
        {
            if (!IsRunning)
            {
                return Array.Empty<RogueNode>();
            }

            RogueNode current = GetCurrentNode();
            if (current == null)
            {
                return Array.Empty<RogueNode>();
            }

            var next = new List<RogueNode>();
            for (int i = 0; i < current.nextIds.Count; i++)
            {
                string nodeId = current.nextIds[i];
                if (_visited.Contains(nodeId))
                {
                    continue;
                }

                if (_nodeIndex.TryGetValue(nodeId, out RogueNode node))
                {
                    if (IsNodeVisible(node))
                    {
                        next.Add(node);
                    }
                }
            }

            return next;
        }

        public RogueNode EnterNode(string nodeId)
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("Rogue run is not active.");
            }

            if (string.IsNullOrWhiteSpace(nodeId))
            {
                throw new ArgumentException("Node id is null or empty.", nameof(nodeId));
            }

            RogueNode current = GetCurrentNode();
            if (current == null)
            {
                throw new InvalidOperationException("Current node is missing.");
            }

            if (_visited.Contains(nodeId))
            {
                throw new InvalidOperationException($"Node \"{nodeId}\" was already visited.");
            }

            if (!current.nextIds.Contains(nodeId))
            {
                throw new InvalidOperationException($"Node \"{nodeId}\" is not reachable from current node.");
            }

            if (!_nodeIndex.TryGetValue(nodeId, out RogueNode node))
            {
                throw new InvalidOperationException($"Node \"{nodeId}\" does not exist.");
            }
            if (!IsNodeVisible(node))
            {
                throw new InvalidOperationException($"Node \"{nodeId}\" is hidden.");
            }

            _state.currentNodeId = nodeId;
            _state.currentFloor = node.floor;
            _visited.Add(nodeId);
            _state.visitedNodeIds.Add(nodeId);

            EnsureNodeState(node);
            AutoSave();
            OnNodeEntered?.Invoke(node);
            return node;
        }

        public RogueBattleEntryRequest BuildBattleEntryRequest(
            string difficulty,
            IReadOnlyList<RogueSquadUnitPayload> squad,
            IReadOnlyList<RogueModifierPayload> modifiers = null)
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("Rogue run is not active.");
            }

            RogueNode node = GetCurrentNode();
            if (node == null)
            {
                throw new InvalidOperationException("Current node is missing.");
            }
            if (node.type != RogueNodeType.Battle &&
                node.type != RogueNodeType.Elite &&
                node.type != RogueNodeType.MidBoss &&
                node.type != RogueNodeType.Boss)
            {
                throw new InvalidOperationException($"Node type {node.type} is not a battle node.");
            }

            var request = new RogueBattleEntryRequest
            {
                run_id = _state.runId,
                floor_index = _state.currentFloor,
                node_id = node.id,
                map_seed = _state.seed,
                difficulty = string.IsNullOrWhiteSpace(difficulty) ? "normal" : difficulty,
                global_state = new RogueGlobalStatePayload
                {
                    current_hp = _state.health,
                    hope_remaining = _state.hope,
                    ingots = _state.ingots,
                    active_curios = new List<string>(_state.curios),
                    flags = new List<string>(_state.flags),
                }
            };

            if (squad != null)
            {
                for (int i = 0; i < squad.Count; i++)
                {
                    if (squad[i] != null)
                    {
                        request.squad.Add(squad[i]);
                    }
                }
            }

            if (modifiers != null)
            {
                for (int i = 0; i < modifiers.Count; i++)
                {
                    if (modifiers[i] != null)
                    {
                        request.global_state.modifiers.Add(modifiers[i]);
                    }
                }
            }

            request.client_checksum = RogueProtocolChecksum.BuildClientChecksum(request);
            AutoSave();
            return request;
        }

        public void ApplyBattleResult(RogueBattleResultResponse response, bool validateChecksum = true)
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("Rogue run is not active.");
            }
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }
            if (validateChecksum && !RogueProtocolChecksum.VerifyServerChecksum(response))
            {
                throw new InvalidOperationException("Server checksum validation failed.");
            }

            var result = new RogueNodeResult
            {
                success = string.Equals(response.result, "win", StringComparison.OrdinalIgnoreCase),
                healthDelta = response.state_delta.hp_change,
                hopeDelta = response.state_delta.hope_change + response.rewards.hope_drop,
                ingotsDelta = response.state_delta.ingots_change + response.rewards.ingot_drop,
                commandExpDelta = response.rewards.exp_drop
            };

            for (int i = 0; i < response.state_delta.curios_added.Count; i++)
            {
                string curio = response.state_delta.curios_added[i];
                if (!string.IsNullOrWhiteSpace(curio))
                {
                    result.curiosAdded.Add(curio);
                    result.rewards.Add(new RogueReward
                    {
                        type = RogueRewardType.Relic,
                        id = curio,
                        amount = 1
                    });
                }
            }

            for (int i = 0; i < response.rewards.curio_granted.Count; i++)
            {
                string curio = response.rewards.curio_granted[i];
                if (!string.IsNullOrWhiteSpace(curio))
                {
                    result.curiosAdded.Add(curio);
                    result.rewards.Add(new RogueReward
                    {
                        type = RogueRewardType.Relic,
                        id = curio,
                        amount = 1
                    });
                }
            }

            if (result.rewards.Count == 0 &&
                (GetCurrentNode()?.type == RogueNodeType.Battle ||
                 GetCurrentNode()?.type == RogueNodeType.Elite ||
                 GetCurrentNode()?.type == RogueNodeType.MidBoss ||
                 GetCurrentNode()?.type == RogueNodeType.Boss))
            {
                result.rewards.Add(RogueRewardRoller.RollBattleReward(_state, GetCurrentNode()));
            }

            for (int i = 0; i < response.state_delta.flags_set.Count; i++)
            {
                string flag = response.state_delta.flags_set[i];
                if (!string.IsNullOrWhiteSpace(flag))
                {
                    result.flagsSet.Add(flag);
                }
            }

            if (string.Equals(response.result, "retreat", StringComparison.OrdinalIgnoreCase))
            {
                result.flagsSet.Add("retreat_used");
            }

            ResolveCurrentNode(result);
        }

        public RogueShopState GetCurrentShopState()
        {
            RogueNode node = GetCurrentNode();
            if (node == null || node.type != RogueNodeType.Shop)
            {
                return null;
            }
            EnsureNodeState(node);
            return _shopStates.TryGetValue(node.id, out RogueShopState shop) ? shop : null;
        }

        public bool TryRefreshCurrentShop()
        {
            RogueNode node = GetCurrentNode();
            if (node == null || node.type != RogueNodeType.Shop)
            {
                return false;
            }
            EnsureNodeState(node);
            if (!_shopStates.TryGetValue(node.id, out RogueShopState shop))
            {
                return false;
            }
            if (shop.refreshCount >= 3)
            {
                return false;
            }

            bool ok = RogueShopService.TryRefresh(_state, shop, _shopPool, _rng, 3, 5);
            if (ok)
            {
                _state.totalRefreshCount++;
                AutoSave();
            }
            return ok;
        }

        public bool TryPurchaseCurrentShop(string slotId)
        {
            RogueShopState shop = GetCurrentShopState();
            if (shop == null)
            {
                return false;
            }
            bool ok = RogueShopService.TryPurchase(_state, shop, slotId);
            if (ok)
            {
                AutoSave();
            }
            return ok;
        }

        public RogueNodeResult ResolveCurrentEvent(string optionId)
        {
            RogueNode node = GetCurrentNode();
            if (node == null || node.type != RogueNodeType.Event)
            {
                throw new InvalidOperationException("Current node is not an event.");
            }

            if (!_eventDefinitions.TryGetValue(node.contentId, out RogueEventDefinition definition))
            {
                throw new InvalidOperationException($"Event definition not found: {node.contentId}");
            }

            RogueNodeResult result = RogueEventService.ResolveOption(_state, definition, optionId, _rng);
            ResolveCurrentNode(result);
            return result;
        }

        public void ResolveCurrentNode(RogueNodeResult result)
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("Rogue run is not active.");
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            RogueNode node = GetCurrentNode();
            if (node == null)
            {
                throw new InvalidOperationException("Current node is missing.");
            }

            _state.hope += result.hopeDelta;
            _state.ingots += result.ingotsDelta;
            _state.health += result.healthDelta;
            _state.maxHealth += result.maxHealthDelta;
            _state.keys += result.keysDelta;
            _state.commandExp += result.commandExpDelta;

            if (result.flagsSet != null)
            {
                for (int i = 0; i < result.flagsSet.Count; i++)
                {
                    string flag = result.flagsSet[i];
                    if (!string.IsNullOrWhiteSpace(flag) && !_state.flags.Contains(flag))
                    {
                        _state.flags.Add(flag);
                    }
                }
            }

            if (result.curiosAdded != null)
            {
                for (int i = 0; i < result.curiosAdded.Count; i++)
                {
                    string curio = result.curiosAdded[i];
                    if (!string.IsNullOrWhiteSpace(curio) && !_state.curios.Contains(curio))
                    {
                        _state.curios.Add(curio);
                    }
                }
            }

            if (result.rewards != null)
            {
                for (int i = 0; i < result.rewards.Count; i++)
                {
                    RogueRewardApplier.Apply(_state, result.rewards[i]);
                }
            }

            _state.ClampHealth();
            OnNodeResolved?.Invoke(node, result);

            TryUnlockHiddenFloor(node, result);

            if (!result.success || _state.health <= 0)
            {
                EndRun(false);
                return;
            }

            if (node.type == RogueNodeType.Boss)
            {
                if (!HasVisibleNextNodes(node))
                {
                    EndRun(true);
                }
                return;
            }

            AutoSave();
        }

        public RogueNode GetCurrentNode()
        {
            if (_state == null || string.IsNullOrWhiteSpace(_state.currentNodeId))
            {
                return null;
            }

            return _nodeIndex.TryGetValue(_state.currentNodeId, out RogueNode node) ? node : null;
        }

        public RogueRunSnapshot CreateSnapshot()
        {
            var shops = new List<RogueShopState>();
            foreach (KeyValuePair<string, RogueShopState> pair in _shopStates)
            {
                if (pair.Value != null)
                {
                    shops.Add(pair.Value);
                }
            }
            return new RogueRunSnapshot
            {
                config = _config,
                map = _map,
                state = _state,
                shopStates = shops
            };
        }

        public void LoadSnapshot(RogueRunSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            _config = snapshot.config;
            _map = snapshot.map;
            _state = snapshot.state;
            _rng = new System.Random(_state != null ? _state.seed : Environment.TickCount);

            BuildIndex();
            _visited.Clear();
            _shopStates.Clear();
            if (_state != null && _state.visitedNodeIds != null)
            {
                for (int i = 0; i < _state.visitedNodeIds.Count; i++)
                {
                    string id = _state.visitedNodeIds[i];
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        _visited.Add(id);
                    }
                }
            }

            if (snapshot.shopStates != null)
            {
                for (int i = 0; i < snapshot.shopStates.Count; i++)
                {
                    RogueShopState shop = snapshot.shopStates[i];
                    if (shop != null && !string.IsNullOrWhiteSpace(shop.nodeId))
                    {
                        _shopStates[shop.nodeId] = shop;
                    }
                }
            }
        }

        void EnsureNodeState(RogueNode node)
        {
            if (node == null)
            {
                return;
            }

            if (node.type == RogueNodeType.Shop && !_shopStates.ContainsKey(node.id))
            {
                if (_shopPool.Count == 0)
                {
                    BuildDefaultShopPool();
                }
                _shopStates[node.id] = RogueShopService.BuildInventory(node.id, node.floor, _shopPool, 3, 5, _rng);
            }
        }

        void TryUnlockHiddenFloor(RogueNode node, RogueNodeResult result)
        {
            if (node == null || result == null)
            {
                return;
            }

            if (node.floor != 3 || node.type != RogueNodeType.MidBoss)
            {
                return;
            }

            if (_state.flags.Contains("hidden_floor_unlocked"))
            {
                return;
            }

            if (result.healthDelta < 0)
            {
                return;
            }

            if (_state.flags.Contains("retreat_used"))
            {
                return;
            }

            if (CountSecretCurios() < 3)
            {
                return;
            }

            _state.flags.Add("hidden_floor_unlocked");
        }

        bool HasVisibleNextNodes(RogueNode node)
        {
            if (node == null)
            {
                return false;
            }

            for (int i = 0; i < node.nextIds.Count; i++)
            {
                if (_nodeIndex.TryGetValue(node.nextIds[i], out RogueNode next) && IsNodeVisible(next))
                {
                    return true;
                }
            }

            return false;
        }

        bool IsNodeVisible(RogueNode node)
        {
            return node != null && (!node.hidden || _state.flags.Contains("hidden_floor_unlocked"));
        }

        int CountSecretCurios()
        {
            int count = 0;
            for (int i = 0; i < _state.curios.Count; i++)
            {
                if (IsSecretCurioId(_state.curios[i]))
                {
                    count++;
                }
            }
            return count;
        }

        static bool IsSecretCurioId(string curioId)
        {
            if (string.IsNullOrWhiteSpace(curioId))
            {
                return false;
            }

            string lower = curioId.ToLowerInvariant();
            return lower.Contains("secret") || lower.Contains("hidden") || lower.Contains("curio_隐秘");
        }

        void BuildDefaultShopPool()
        {
            _shopPool.Add(new RogueShopItem
            {
                itemId = "shop_heal_1",
                category = "consumable",
                basePrice = 20,
                reward = new RogueReward { type = RogueRewardType.Heal, amount = 1, id = "heal_1" }
            });
            _shopPool.Add(new RogueShopItem
            {
                itemId = "shop_hope_2",
                category = "upgrade",
                basePrice = 25,
                reward = new RogueReward { type = RogueRewardType.Currency, amount = 2, id = "hope_2" }
            });
            _shopPool.Add(new RogueShopItem
            {
                itemId = "shop_ingot_10",
                category = "utility",
                basePrice = 15,
                reward = new RogueReward { type = RogueRewardType.Gold, amount = 10, id = "ingot_10" }
            });
            _shopPool.Add(new RogueShopItem
            {
                itemId = "shop_relic_random",
                category = "curio",
                basePrice = 35,
                reward = new RogueReward { type = RogueRewardType.Relic, amount = 1, id = "curio_random_1" }
            });
            _shopPool.Add(new RogueShopItem
            {
                itemId = "shop_max_hp_1",
                category = "upgrade",
                basePrice = 30,
                reward = new RogueReward { type = RogueRewardType.MaxHealth, amount = 1, id = "max_hp_1" }
            });
        }

        void EndRun(bool success)
        {
            if (_state == null)
            {
                return;
            }

            _state.status = success ? RogueRunStatus.Success : RogueRunStatus.Failed;
            RogueCommandProgressService.CommitRun(_state.commandExp);
            AutoSave();
            OnRunEnded?.Invoke(_state);
        }

        void AutoSave()
        {
            if (string.IsNullOrWhiteSpace(_savePath))
            {
                return;
            }

            try
            {
                RogueRunPersistence.Save(_savePath, CreateSnapshot(), "1.0.0");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RogueRunManager] AutoSave failed: {ex.Message}");
            }
        }

        void BuildIndex()
        {
            _nodeIndex.Clear();
            if (_map == null || _map.nodes == null)
            {
                return;
            }

            for (int i = 0; i < _map.nodes.Count; i++)
            {
                RogueNode node = _map.nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.id))
                {
                    continue;
                }

                _nodeIndex[node.id] = node;
            }
        }
    }
}

using System.Collections.Generic;

namespace GameDemo.Battle
{
    /// <summary>
    /// 行动队列。维护对局中所有单位的行动代价，按 ActionValue 升序排列，值最小的位于队首。
    /// 内部使用 List 实现随机访问。
    /// </summary>
    public class ActionQueue
    {
        private readonly List<BattleUnitInstance> _queue = new List<BattleUnitInstance>();

        public int Count => _queue.Count;

        public BattleUnitInstance? Current => _queue.Count > 0 ? _queue[0] : null;

        public BattleUnitInstance? this[int index] =>
            index >= 0 && index < _queue.Count ? _queue[index] : null;

        /// <summary>
        /// 从双方阵形重建队列，收集所有存活单位的实例。
        /// </summary>
        public void Rebuild(Formation playerFormation, Formation enemyFormation)
        {
            _queue.Clear();
            CollectAliveUnits(playerFormation);
            CollectAliveUnits(enemyFormation);
            Sort();
        }

        /// <summary>
        /// 将队首单位的 ActionValue 作为时间增量 t，使队列中所有单位的 RemainingCost 减少 t * Speed，
        /// 队首单位 RemainingCost 归零。然后重新排序。
        /// </summary>
        public void AdvanceTime()
        {
            if (_queue.Count == 0) return;
            BattleUnitInstance head = _queue[0];
            float t = head.ActionValue;
            if (t <= 0f) return;
            for (int i = 0; i < _queue.Count; i++)
            {
                _queue[i].RemainingCost -= t * _queue[i].CurrentSpeed;
                if (_queue[i].RemainingCost < 0f) _queue[i].RemainingCost = 0f;
            }
            Sort();
        }

        public bool Remove(BattleUnitInstance unit) => _queue.Remove(unit);

        public void Reshuffle() => Sort();

        public int IndexOf(BattleUnitInstance unit) => _queue.IndexOf(unit);

        public bool Contains(BattleUnitInstance unit) => _queue.Contains(unit);

        private void CollectAliveUnits(Formation formation)
        {
            foreach (BattleUnitInstance unit in formation.Units)
                if (unit.IsAlive)
                    _queue.Add(unit);
        }

        private void Sort()
        {
            _queue.Sort((a, b) => a.ActionValue.CompareTo(b.ActionValue));
        }
    }
}

using System;

namespace GameDemo.Battle
{
    /// <summary>
    /// 对局状态机，管理 BattleState 的切换与查询。
    /// </summary>
    public class BattleStateMachine
    {
        public BattleState CurrentState { get; private set; } = BattleState.BattleStart;

        public event Action<BattleState, BattleState> OnStateChanged;

        public bool IsBattleStart => CurrentState == BattleState.BattleStart;
        public bool IsPreAction => CurrentState == BattleState.PreAction;
        public bool IsWaitingAction => CurrentState == BattleState.WaitingAction;
        public bool IsActing => CurrentState == BattleState.Acting;
        public bool IsPostAction => CurrentState == BattleState.PostAction;
        public bool IsBattleEnd => CurrentState == BattleState.BattleEnd;
        public bool IsPaused => CurrentState == BattleState.Paused;

        /// <summary>
        /// 直接设置状态，触发 OnStateChanged 事件。
        /// </summary>
        public void SetState(BattleState newState)
        {
            if (newState == CurrentState) return;

            BattleState previous = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(previous, newState);
        }
    }
}

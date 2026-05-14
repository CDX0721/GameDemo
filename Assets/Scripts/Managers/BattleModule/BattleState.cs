namespace GameDemo.Battle
{
    /// <summary>
    /// 对局状态枚举。
    /// </summary>
    public enum BattleState
    {
        /// <summary>游戏开始 / 战斗初始化</summary>
        BattleStart,
        /// <summary>单位行动前</summary>
        PreAction,
        /// <summary>单位等待行动</summary>
        WaitingAction,
        /// <summary>单位行动中</summary>
        Acting,
        /// <summary>单位行动后</summary>
        PostAction,
        /// <summary>游戏结束</summary>
        BattleEnd,
        /// <summary>游戏暂停</summary>
        Paused
    }
}

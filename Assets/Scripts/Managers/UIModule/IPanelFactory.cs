using System;

namespace GameDemo.UI
{
    /// <summary>
    /// 面板实例化策略接口。UIManager 在注册表中找不到目标面板时调用此接口创建。
    /// 实现类负责从 Resources、Addressables 或其他来源加载并实例化面板。
    /// </summary>
    public interface IPanelFactory
    {
        /// <summary>
        /// 创建或加载指定类型的面板实例。返回 null 表示此工厂无法处理该类型。
        /// </summary>
        /// <param name="panelType">面板类型，通常是继承 UIPanel 的具体类</param>
        UIPanel CreatePanel(Type panelType);
    }
}

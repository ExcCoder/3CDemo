namespace CCCProject.Core
{
    /// <summary>
    /// 模块接口。
    /// 模块是**普通 C# 类**，不是 MonoBehaviour：它的创建、启动、关闭全部由
    /// <see cref="ModuleCenter"/> 统一管理，不依赖 Unity 的 Awake / OnDestroy / Update。
    /// 这样"模块的生命周期"只有一处说了算，换宿主（场景、测试、服务器）也不用改模块本身。
    /// </summary>
    public interface IGameModule
    {
        /// <summary>初始化：注册进 <see cref="ModuleCenter"/> 时调用一次。</summary>
        void OnLaunch();

        /// <summary>关闭：从 <see cref="ModuleCenter"/> 注销或整体清空时调用一次，用于释放资源。</summary>
        void OnShutdown();
    }
}

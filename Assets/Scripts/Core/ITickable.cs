namespace CCCProject.Core
{
    /// <summary>
    /// 需要每帧更新的模块实现本接口，由 <see cref="ModuleCenter.Tick"/> 按**注册顺序**驱动。
    /// </summary>
    /// <remarks>
    /// 有了它，帧序就是显式的：先注册、先更新。
    /// 以前各模块各自挂 <c>Update</c>，谁先跑取决于 Unity 的脚本执行顺序（要靠
    /// <c>[DefaultExecutionOrder]</c> 兜底），输入读早了、读晚了都会出问题。
    /// </remarks>
    public interface ITickable
    {
        /// <summary>每帧调用一次。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        void Tick(float deltaTime);
    }
}

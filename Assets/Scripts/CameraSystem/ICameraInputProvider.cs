using Cinemachine;
using UnityEngine;

namespace CCCProject.CameraSystem
{
    /// <summary>
    /// 视角输入适配器接口：相机只认这个接口，不认具体实现。
    /// </summary>
    /// <remarks>
    /// 实现类**必须是挂在 vcam 上的 MonoBehaviour**：Cinemachine 2.8.9 的
    /// <c>CinemachineVirtualCameraBase.GetInputAxisProvider()</c> 是按 vcam 上的组件扫描查找的。
    /// 因此的是"引擎适配器"，不属于 <c>IGameModule</c>，也不注册进 <c>ModuleCenter</c>——
    /// 它的开关由 <c>CameraModule</c> 通过接口控制。
    /// </remarks>
    public interface ICameraInputProvider : AxisState.IInputAxisProvider
    {
        /// <summary>视角输入开关（过场 / 菜单时关闭）。</summary>
        bool InputEnabled { get; set; }
    }
}

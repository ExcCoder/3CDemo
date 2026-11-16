using Cinemachine;
using UnityEngine;

namespace CCCProject.CameraSystem
{
    /// <summary>
    /// 自由视角输入适配器：实现 Cinemachine 的 <see cref="AxisState.IInputAxisProvider"/>，
    /// 把旧 Input Manager 的鼠标轴喂给 FreeLook，并在这里集中处理灵敏度、Y 轴反转和输入开关。
    /// 以后换成 Input System 或自定义 InputModule 时，只替换本组件，不动 CameraModule 和相机结构。
    /// </summary>
    /// <remarks>
    /// 这是**引擎适配器**，不是模块：Cinemachine 2.8.9 要求它必须是挂在 vcam 上的 MonoBehaviour
    /// （<c>CinemachineVirtualCameraBase.GetInputAxisProvider()</c> 按组件扫描），
    /// 因此不实现 <c>IGameModule</c>、也不注册进 <c>ModuleCenter</c>。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CameraInputAxisProvider : MonoBehaviour, ICameraInputProvider
    {
        [Header("视角输入")]
        [SerializeField, Tooltip("水平灵敏度倍率。")]
        private float _sensitivityX = 1f;

        [SerializeField, Tooltip("垂直灵敏度倍率。")]
        private float _sensitivityY = 1f;

        [SerializeField, Tooltip("反转垂直视角。")]
        private bool _invertY;

        [SerializeField, Tooltip("是否允许读取视角输入；过场 / 菜单时由 CameraModule 关闭。")]
        private bool _inputEnabled = true;

        /// <summary>视角输入开关。</summary>
        public bool InputEnabled
        {
            get => _inputEnabled;
            set => _inputEnabled = value;
        }

        /// <summary>读取指定轴输入（0 = X，1 = Y，2 = Z）。</summary>
        /// <param name="axis">轴索引。</param>
        /// <returns>该轴的输入值；输入关闭时返回 0。</returns>
        public float GetAxisValue(int axis)
        {
            if (!_inputEnabled)
                return 0f;

            switch (axis)
            {
                case 0:
                    return Input.GetAxis("Mouse X") * _sensitivityX;
                case 1:
                    return Input.GetAxis("Mouse Y") * _sensitivityY * (_invertY ? -1f : 1f);
                default:
                    return 0f;
            }
        }
    }
}

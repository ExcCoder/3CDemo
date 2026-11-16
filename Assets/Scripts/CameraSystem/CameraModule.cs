using System;
using CCCProject.Core;
using UnityEngine;

namespace CCCProject.CameraSystem
{
    /// <summary>
    /// 相机门面：对游戏逻辑只暴露"绑定目标 / 模式 / 输入开关 / 参数 / 事件"，
    /// 具体表现交给 <see cref="ICameraRig"/> 的实现（当前是 Cinemachine），
    /// 公共 API 不出现 Cinemachine 类型，便于以后用同一套接口接入手写相机做方案对比。
    /// </summary>
    /// <remarks>
    /// 纯 C# 类：生命周期由 <see cref="ModuleCenter"/> 管理，依赖由组合根（<c>GameLauncher</c>）注入。
    /// </remarks>
    public sealed class CameraModule : IGameModule
    {
        private readonly ICameraRig _rig;
        private readonly CameraTarget _initialTarget;

        private CameraSettings _settings;
        private CameraMode _mode = CameraMode.FreeLook;

        /// <summary>当前相机模式。</summary>
        public CameraMode Mode => _mode;

        /// <summary>当前绑定的相机目标，未绑定时为 null。</summary>
        public CameraTarget Target { get; private set; }

        /// <summary>
        /// 表现层是否仍可用：退出 Play 时 MonoBehaviour 已被销毁，
        /// 此时调用它的任何方法都会抛 MissingReferenceException。
        /// </summary>
        private bool RigAvailable
        {
            get
            {
                if (_rig == null)
                {
                    return false;
                }

                // ICameraRig 是接口，直接 == null 走的是引用比较；
                // 转成 UnityEngine.Object 才能用到 Unity 重载的 ==，识别已销毁对象。
                if (_rig is UnityEngine.Object unityObject && unityObject == null)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// 实际输出相机的 Transform，即"玩家看到的视角"，角色以此为移动基准；
        /// 输出相机未就绪时为 null。
        /// </summary>
        public Transform ViewTransform => RigAvailable ? _rig.OutputTransform : null;

        /// <summary>当前生效的相机参数资产。</summary>
        public CameraSettings Settings => _settings;

        /// <summary>模式变化后触发。</summary>
        public event Action<CameraMode> ModeChanged;

        /// <summary>绑定目标变化后触发（解绑时为 null）。</summary>
        public event Action<CameraTarget> TargetChanged;

        /// <summary>构造相机门面。</summary>
        /// <param name="rig">相机表现层（引擎适配器），门面只认这个接口。</param>
        /// <param name="settings">相机手感参数资产；为空时沿用场景里的旧数值。</param>
        /// <param name="initialTarget">启动时自动绑定的相机目标。</param>
        public CameraModule(ICameraRig rig, CameraSettings settings, CameraTarget initialTarget)
        {
            _rig = rig;
            _settings = settings;
            _initialTarget = initialTarget;
        }

        /// <summary>启动流程：应用参数、绑定初始目标并进入默认模式。</summary>
        public void OnLaunch()
        {
            if (!RigAvailable)
            {
                Debug.LogError("[CameraModule] 没有相机表现层（ICameraRig），相机不会工作。");
                return;
            }

            ApplySettings(_settings);

            if (_initialTarget != null)
            {
                BindTarget(_initialTarget);
            }
            else
            {
                Debug.LogWarning("[CameraModule] 启动时没有初始相机目标，请稍后调用 BindTarget。");
            }

            SetMode(CameraMode.FreeLook);
        }

        /// <summary>关闭流程：关闭视角输入并解绑目标。</summary>
        public void OnShutdown()
        {
            if (RigAvailable)
            {
                _rig.SetInputEnabled(false);
            }

            BindTarget(null);
        }

        /// <summary>绑定相机跟随目标；传 null 表示解绑。</summary>
        /// <param name="target">角色身上的相机目标。</param>
        public void BindTarget(CameraTarget target)
        {
            Target = target;

            if (target != null && _settings != null)
            {
                // 锚点高度也归参数资产管：换模型、调身高不用改预制体。
                target.ConfigureAnchors(_settings.FollowAnchorHeight, _settings.LookAnchorHeight);
            }

            if (RigAvailable)
            {
                _rig.BindTarget(target);
            }

            TargetChanged?.Invoke(target);
        }

        /// <summary>切换相机模式。</summary>
        /// <param name="mode">目标模式。</param>
        public void SetMode(CameraMode mode)
        {
            switch (mode)
            {
                case CameraMode.FreeLook:
                    break;

                default:
                    Debug.LogWarning($"[CameraModule] 尚未实现的相机模式：{mode}。");
                    return;
            }

            _mode = mode;
            ModeChanged?.Invoke(mode);
        }

        /// <summary>开关视角输入，供过场 / 菜单 / 死亡等状态调用。</summary>
        /// <param name="enabled">是否允许读取视角输入。</param>
        public void SetCameraInputEnabled(bool enabled)
        {
            if (!RigAvailable)
            {
                Debug.LogWarning("[CameraModule] 没有相机表现层，无法切换视角输入。");
                return;
            }

            _rig.SetInputEnabled(enabled);
        }

        /// <summary>换一份参数资产并立刻生效（调参时不用重进 Play）。</summary>
        /// <param name="settings">新的相机参数资产。</param>
        public void ApplySettings(CameraSettings settings)
        {
            _settings = settings;

            if (!RigAvailable)
            {
                return;
            }

            _rig.Apply(settings);

            if (Target != null && settings != null)
            {
                Target.ConfigureAnchors(settings.FollowAnchorHeight, settings.LookAnchorHeight);
            }
        }

        /// <summary>按比例拉近 / 推远相机臂（1 为标称距离），留给瞄准与锁定视角。</summary>
        /// <param name="scale">缩放系数。</param>
        public void SetArmDistanceScale(float scale)
        {
            if (RigAvailable)
            {
                _rig.SetArmDistanceScale(scale);
            }
        }
    }
}

using UnityEngine;

namespace CCCProject.CameraSystem
{
    /// <summary>
    /// 相机表现层的抽象：门面 <see cref="CameraModule"/> 只通过本接口驱动相机，
    /// 因此门面的公共 API 里不会出现 Cinemachine 类型。
    /// </summary>
    /// <remarks>
    /// 实现是引擎适配器（<see cref="CameraRigBinding"/>，必须挂在 vcam 上），
    /// 以后要换一套手写相机做方案对比，只需再实现一份本接口，门面一个字都不用改。
    /// </remarks>
    public interface ICameraRig
    {
        /// <summary>
        /// 实际输出相机的 Transform，即"玩家看到的视角"；角色以此为移动基准。
        /// 输出相机未就绪时为 null。
        /// </summary>
        Transform OutputTransform { get; }

        /// <summary>绑定跟随目标；传 null 表示解绑。</summary>
        /// <param name="target">角色身上的相机目标。</param>
        void BindTarget(CameraTarget target);

        /// <summary>把参数资产应用到表现层（臂长、FOV、碰撞避让）。</summary>
        /// <param name="settings">相机参数资产。</param>
        void Apply(CameraSettings settings);

        /// <summary>相机臂距离缩放：1 为资产标称距离，小于 1 拉近（瞄准 / 锁定会用到）。</summary>
        /// <param name="scale">缩放系数，最小 0.05。</param>
        void SetArmDistanceScale(float scale);

        /// <summary>开关视角输入，供过场 / 菜单 / 死亡等状态调用。</summary>
        /// <param name="enabled">是否允许读取视角输入。</param>
        void SetInputEnabled(bool enabled);
    }
}

using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 角色运动契约：只暴露“运动事实”与“移动锁”，不暴露内部求解与物理实现。
    /// 状态机、动画、音效等上层只依赖本接口，不认识具体运动实现。
    /// </summary>
    public interface ICharacterMotor
    {
        /// <summary>当前速度（含垂直分量）。</summary>
        Vector3 Velocity { get; }

        /// <summary>当前水平速度大小。</summary>
        float PlanarSpeed { get; }

        /// <summary>是否贴地。</summary>
        bool IsGrounded { get; }

        /// <summary>是否离地（跳跃滞空中）。走下坡不算离地，只有起跳之后才算。</summary>
        bool IsAirborne { get; }

        /// <summary>移动锁：锁定后不再响应移动输入，只做减速与重力。</summary>
        bool MovementLocked { get; set; }

        /// <summary>相机朝向的水平投影，作为移动方向的基准。</summary>
        Vector3 ViewForward { get; }

        /// <summary>是否处于落地恢复期：落地动画播完前禁止位移，但可以被翻滚打断。</summary>
        bool LandingRecovery { get; }

        /// <summary>取消落地恢复期。翻滚等动作打断落地后摇时调用。</summary>
        void CancelLandingRecovery();

        /// <summary>请求起跳。地面状态下于下一次 tick 生效；空中请求会被丢弃（不做二段跳）。</summary>
        void RequestJump();

        /// <summary>
        /// 交由脚本驱动的水平位移（翻滚等动作使用）。
        /// </summary>
        /// <param name="direction">位移方向（世界坐标，水平面内）。</param>
        /// <param name="distance">本帧要前进的距离（米）。</param>
        void SetScriptedPlanarMotion(Vector3 direction, float distance);

        /// <summary>动作是否正在占用移动与转向。</summary>
        bool ActionLocked { get; }

        /// <summary>
        /// 占用动作控制权（带所有者标记）。
        /// </summary>
        /// <remarks>
        /// 锁必须带所有者，否则"攻击被翻滚打断"时会出问题：
        /// 攻击结束时无条件释放，翻滚进行中玩家却已经能移动。
        /// 带所有者后只有持有者能释放，交接时互不误开。
        /// </remarks>
        /// <param name="owner">占用者（一般传 this）。</param>
        void AcquireActionLock(object owner);

        /// <summary>
        /// 释放动作控制权；当前所有者不是 <paramref name="owner"/> 时不做任何事。
        /// </summary>
        /// <param name="owner">释放者（一般传 this）。</param>
        void ReleaseActionLock(object owner);
    }
}
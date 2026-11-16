using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 角色移动手感参数（走/跑速度、加减速、转向、重力、贴地）。
    /// 手感数值集中在此资产里，调参不动代码；各角色可引用同一份或各持一份。
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterMovementConfig", menuName = "CCCProject/Character Movement Config")]
    public sealed class CharacterMovementConfig : ScriptableObject
    {
        [Header("速度")]
        [Tooltip("走路速度（米/秒）。")]
        [SerializeField] private float _walkSpeed = 1.6f;

        [Tooltip("奔跑速度（米/秒）。")]
        [SerializeField] private float _runSpeed = 5.2f;

        [Header("加减速")]
        [Tooltip("起动加速度（米/秒²）。越大越干脆。")]
        [SerializeField] private float _acceleration = 22f;

        [Tooltip("停步减速度（米/秒²）。比重加速大，手感更利落。")]
        [SerializeField] private float _deceleration = 30f;

        [Header("转向")]
        [Tooltip("转身速度上限（度/秒）。限制上限制造“动作承诺”感。")]
        [SerializeField] private float _turnSpeed = 720f;

        [Header("重力与贴地")]
        [Tooltip("重力加速度（米/秒²），负值向下。")]
        [SerializeField] private float _gravity = -24f;

        [Tooltip("贴地速度（米/秒），负值。避免走下坡时弹跳。")]
        [SerializeField] private float _groundStick = -2f;

        [Header("跳跃")]
        [Tooltip("起跳竖直速度（米/秒）。越大跳得越高；与 Gravity 一起决定滞空时长。")]
        [SerializeField] private float _jumpVelocity = 6.5f;

        [Header("落地恢复")]
        [Tooltip("1 倍播放时完整落地动作的目标时长（含退出混合）。装配脚本按此值调整状态速度，" +
                 "保留完整下蹲与站起；运行时实际解锁以 Animator 返回移动状态为准。")]
        [SerializeField] private float _landingRecoveryTime = 1.4f;

        /// <summary>走路速度（米/秒）。</summary>
        public float WalkSpeed => _walkSpeed;

        /// <summary>奔跑速度（米/秒）。</summary>
        public float RunSpeed => _runSpeed;

        /// <summary>起动加速度（米/秒²）。</summary>
        public float Acceleration => _acceleration;

        /// <summary>停步减速度（米/秒²）。</summary>
        public float Deceleration => _deceleration;

        /// <summary>转身速度上限（度/秒）。</summary>
        public float TurnSpeed => _turnSpeed;

        /// <summary>重力加速度（米/秒²）。</summary>
        public float Gravity => _gravity;

        /// <summary>贴地速度（米/秒）。</summary>
        public float GroundStick => _groundStick;

        /// <summary>起跳竖直速度（米/秒）。</summary>
        public float JumpVelocity => _jumpVelocity;

        /// <summary>完整落地动作的标定时长（秒、1 倍速）；运行期不作为解锁计时器。</summary>
        public float LandingRecoveryTime => _landingRecoveryTime;
    }
}

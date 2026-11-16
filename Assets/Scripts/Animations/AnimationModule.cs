using CCCProject.Character;
using CCCProject.Core;
using UnityEngine;

namespace CCCProject.Animations
{
    /// <summary>
    /// 动画表现层模块：把角色的**运动事实**翻译成 Animator 参数，驱动原生 Animator 状态机。
    /// </summary>
    /// <remarks>
    /// 纯 C# 类：生命周期由 <see cref="ModuleCenter"/> 管理，每帧由 <see cref="ITickable"/> 驱动，
    /// 依赖（Animator / 角色运动 / 参数）由组合根构造注入。
    /// <para>
    /// 本模块**只做表现**，不做决策：它不知道什么是"动作"、什么是"连招"，
    /// 只负责把 <see cref="ICharacterMotor"/> 的事实翻译成状态机认识的参数：
    /// <list type="bullet">
    /// <item><description><c>Speed</c>（float，米/秒）—— 驱动待机/走/跑的混合树；</description></item>
    /// <item><description><c>Grounded</c>（bool）—— 落地判定，决定是否回落到待机层；</description></item>
    /// <item><description><c>Jump</c>（trigger）—— 起跳瞬间的边沿触发。</description></item>
    /// </list>
    /// 状态之间怎么连（transition）是 <c>.controller</c> 资产的事，代码不干预。
    /// </para>
    /// </remarks>
    public sealed class AnimationModule : IGameModule, ITickable
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        private readonly Animator _animator;
        private readonly ICharacterMotor _motor;
        private readonly AnimationSettings _settings;

        private bool _wasAirborne;
        private int _landingSequence = -1;
        private bool _sawLandingState;
        private static readonly int LandingStateHash = Animator.StringToHash("Base Layer.JumpLand");
        private static readonly int LocomotionStateHash = Animator.StringToHash("Base Layer.Locomotion");

        /// <summary>构造动画表现层模块。</summary>
        /// <param name="animator">角色的 Animator（挂在模型根节点上）。</param>
        /// <param name="motor">角色运动事实来源。</param>
        /// <param name="settings">播放倍率参数。</param>
        public AnimationModule(Animator animator, ICharacterMotor motor, AnimationSettings settings)
        {
            _animator = animator;
            _motor = motor;
            _settings = settings;
        }

        /// <summary>启动：校验依赖。</summary>
        public void OnLaunch()
        {
            if (_animator == null)
            {
                Debug.LogError("[AnimationModule] 未指定 Animator，角色不会有动画。");
                return;
            }

            if (_motor == null)
            {
                Debug.LogError("[AnimationModule] 缺少角色运动依赖，请检查模块注册顺序。");
                return;
            }

            if (_settings == null)
            {
                Debug.LogError("[AnimationModule] 未指定 AnimationSettings，步频倍率将退化为 1。");
            }
        }

        /// <summary>关闭：复位播放倍率。</summary>
        public void OnShutdown()
        {
            if (_animator != null)
            {
                _animator.speed = 1f;
            }

            _wasAirborne = false;
            _landingSequence = -1;
            _sawLandingState = false;
        }

        /// <summary>查询指定落地动作是否实际完成，并已稳定返回 Locomotion。</summary>
        /// <param name="sequence">运动层分配的落地序号，防止沿用上次动作的完成状态。</param>
        /// <returns>已进入过 JumpLand，且当前完整退出混合并返回 Locomotion。</returns>
        public bool IsLandingFinished(int sequence)
        {
            if (_landingSequence != sequence)
            {
                _landingSequence = sequence;
                _sawLandingState = false;
            }

            if (_animator == null || !_animator.isActiveAndEnabled)
            {
                return true; // 表现宿主失效时不永久占用移动控制权。
            }

            var current = _animator.GetCurrentAnimatorStateInfo(0);
            bool transitioning = _animator.IsInTransition(0);
            if (current.fullPathHash == LandingStateHash
                || (transitioning && _animator.GetNextAnimatorStateInfo(0).fullPathHash == LandingStateHash))
            {
                _sawLandingState = true;
            }

            return _sawLandingState && !transitioning && current.fullPathHash == LocomotionStateHash;
        }

        /// <summary>每帧：运动事实 → Animator 参数。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            if (_animator == null || _motor == null)
            {
                return;
            }

            float speed = _motor.PlanarSpeed;
            _animator.SetFloat(SpeedHash, speed);

            // Grounded 表示的是在地面上，不是 CharacterController 报贴地：
            // 起跳那一帧控制器还没真正离地，光看 IsGrounded 会让状态在 Jump / Locomotion 之间抖一下，
            // 所以用 IsAirborne 兜住。
            bool grounded = _motor.IsGrounded && !_motor.IsAirborne;
            _animator.SetBool(GroundedHash, grounded);

            // 起跳边沿：离地的那一帧给一次触发，让状态机切进跳跃状态。
            bool airborne = _motor.IsAirborne;
            if (airborne && !_wasAirborne)
            {
                _animator.SetTrigger(JumpHash);
            }

            _wasAirborne = airborne;

            // 全局播放倍率：步频对齐由混合树子动作的 timeScale 完成（标定值在 Controller 资产里），
            // 这里只给一个整体倍率，不参与"走 / 跑"的节奏控制。
            _animator.speed = _settings != null ? _settings.PlaybackScale : 1f;
        }
    }
}

using CCCProject.Core;
using CCCProject.Inputs;
using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 翻滚动作：接受输入、锁定方向、按动画进度驱动位移、结束时归还控制权。
    /// </summary>
    /// <remarks>
    /// 纯 C# 类：生命周期由 <see cref="ModuleCenter"/> 管理，每帧由 <see cref="ITickable"/> 驱动，
    /// 依赖由组合根构造注入。
    /// <para>
    /// 位移由本模块解算后交给运动层执行（<see cref="ICharacterMotor.SetScriptedPlanarMotion"/>），
    /// 动画只负责姿态。这样翻滚撞到墙时会被碰撞自然截断，
    /// 也不会出现"动画走一遍、角色又走一遍"的双倍位移。
    /// </para>
    /// <para>
    /// 位移进度取自动画的 <c>normalizedTime</c>，而不是自己累加时间：
    /// 这样位移曲线与画面天然同步，不受帧率与混合过渡时长影响。
    /// </para>
    /// </remarks>
    public sealed class DodgeModule : IGameModule, ITickable
    {
        private const string RollStateName = "Roll";

        private static readonly int RollHash = Animator.StringToHash("Roll");

        private readonly Animator _animator;
        private readonly DodgeRelay _relay;
        private readonly DodgeSettings _settings;
        private readonly ICharacterMotor _motor;
        private readonly InputModule _input;
        private readonly IDodgeCancelable _cancelSource;

        private Vector3 _direction;
        private bool _active;

        /// <summary>当前是否处于翻滚中。</summary>
        public bool IsDodging => _active;

        /// <summary>翻滚方向（世界坐标，水平面内）。</summary>
        public Vector3 Direction => _direction;

        /// <summary>翻滚已前进的距离（米）。</summary>
        public float TravelledDistance { get; private set; }

        /// <summary>翻滚触发的次数。</summary>
        public int DodgeCount { get; private set; }

        /// <summary>构造翻滚模块。</summary>
        /// <param name="animator">角色 Animator。</param>
        /// <param name="relay">动画状态退出的转发器。</param>
        /// <param name="settings">翻滚手感参数资产。</param>
        /// <param name="motor">角色运动事实来源。</param>
        /// <param name="input">语义输入。</param>
        /// <param name="cancelSource">可被翻滚取消的动作源（攻击）；为空则没有取消通道。</param>
        public DodgeModule(
            Animator animator,
            DodgeRelay relay,
            DodgeSettings settings,
            ICharacterMotor motor,
            InputModule input,
            IDodgeCancelable cancelSource)
        {
            _animator = animator;
            _relay = relay;
            _settings = settings;
            _motor = motor;
            _input = input;
            _cancelSource = cancelSource;
        }

        /// <summary>启动：绑定转发器。</summary>
        public void OnLaunch()
        {
            if (_relay != null)
            {
                _relay.Module = this;
            }
        }

        /// <summary>关闭：结束翻滚并解除绑定。</summary>
        public void OnShutdown()
        {
            Cancel();
            if (_relay != null)
            {
                _relay.Module = null;
            }
        }

        /// <summary>每帧：读取翻滚输入，推进位移。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            if (_animator == null || !_animator.isActiveAndEnabled)
            {
                Cancel();
                return;
            }

            if (!_active)
            {
                if (_input != null && _input.DodgePressed)
                {
                    TryDodge();
                }

                return;
            }

            Advance();
        }

        /// <summary>
        /// 尝试翻滚。禁止条件：不在地面、动画正在过渡、当前不在可起手状态。
        /// </summary>
        /// <remarks>
        /// 三种起手来源：
        /// ① 普通移动状态：直接翻滚；
        /// ② 落地恢复期（JumpLand）：翻滚是"打断落地后摇"的手段，起手时取消落地恢复；
        /// ③ 正在攻击：只有对方开出了取消窗口才接受，否则这一段攻击是不可取消的承诺。
        /// </remarks>
        /// <returns>是否成功开始翻滚。</returns>
        public bool TryDodge()
        {
            if (_animator == null || !_animator.isActiveAndEnabled)
            {
                return false;
            }

            if (_active || _motor == null || _motor.IsAirborne || !_motor.IsGrounded)
            {
                return false;
            }

            if (_animator.IsInTransition(0))
            {
                return false;
            }

            var state = _animator.GetCurrentAnimatorStateInfo(0);
            bool fromLocomotion = state.IsName("Locomotion");
            bool fromLanding = state.IsName("JumpLand");
            bool cancelling = _cancelSource != null && _cancelSource.IsActionActive;

            if (cancelling)
            {
                // 攻击进行中：只有取消窗口打开才接受打断。
                if (!_cancelSource.DodgeCancelOpen)
                {
                    return false;
                }
            }
            else if (!fromLocomotion && !fromLanding)
            {
                return false;
            }

            _direction = ResolveDirection();
            if (_direction.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            _direction.Normalize();
            _active = true;
            TravelledDistance = 0f;
            DodgeCount++;

            // 先取得控制权，再让被打断的动作交出它自己那一份。
            // 锁带所有者标记：攻击随后释放时不会把翻滚持有的锁一起放开。
            _motor.AcquireActionLock(this);

            if (cancelling)
            {
                _cancelSource.AcceptDodgeCancel();
            }
            else if (fromLanding)
            {
                // 翻滚打断落地后摇：取消落地恢复，让翻滚结束后角色能直接移动，
                // 而不是先站在原地等剩余的后摇时间。
                _motor.CancelLandingRecovery();
            }

            _animator.ResetTrigger("Jump");
            _animator.SetTrigger(RollHash);
            return true;
        }

        /// <summary>动画状态退出时调用（被打断或播完）。</summary>
        public void OnStateExited()
        {
            EndDodge();
        }

        /// <summary>结束翻滚：清空脚本位移，归还控制权。</summary>
        public void Cancel()
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            TravelledDistance = 0f;

            if (_motor != null)
            {
                _motor.SetScriptedPlanarMotion(Vector3.zero, 0f);
                _motor.ReleaseActionLock(this);
            }
        }

        /// <summary>推进翻滚位移。</summary>
        private void Advance()
        {
            float progress = GetAnimationProgress();
            if (progress < 0f)
            {
                // 还没进入 Roll 状态（可能仍在混合，或已被其他状态取代），不推进位移。
                return;
            }

            float target = EvaluateDistance(progress);
            float step = target - TravelledDistance;
            TravelledDistance = target;

            if (step > 0f)
            {
                // 位移由运动层执行；这里只给出"本帧想前进多远"。
                _motor.SetScriptedPlanarMotion(_direction, step);
            }
            else
            {
                _motor.SetScriptedPlanarMotion(Vector3.zero, 0f);
            }

            if (progress >= 1f)
            {
                EndDodge();
            }
        }

        /// <summary>取动画当前的归一化进度；还没进入 Roll 状态时返回 -1。</summary>
        private float GetAnimationProgress()
        {
            var current = _animator.GetCurrentAnimatorStateInfo(0);
            if (current.IsName(RollStateName))
            {
                return Mathf.Clamp01(current.normalizedTime);
            }

            if (_animator.IsInTransition(0))
            {
                var next = _animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(RollStateName))
                {
                    return Mathf.Clamp01(next.normalizedTime);
                }
            }

            return -1f;
        }

        /// <summary>按曲线求进度对应的已前进距离（米）。</summary>
        /// <param name="progress">归一化进度 0~1。</param>
        private float EvaluateDistance(float progress)
        {
            if (_settings == null)
            {
                return 0f;
            }

            float ratio = _settings.DistanceProfile != null
                ? _settings.DistanceProfile.Evaluate(progress)
                : progress;

            return Mathf.Clamp01(ratio) * _settings.TotalDistance;
        }

        /// <summary>决定翻滚方向：优先用移动输入（相机相对），否则沿角色当前朝向前翻。</summary>
        private Vector3 ResolveDirection()
        {
            if (_settings != null && _settings.UseInputDirection && _input != null && _input.HasMoveInput)
            {
                Vector2 move = _input.Move;
                Vector3 forward = _motor != null ? _motor.ViewForward : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                Vector3 dir = forward * move.y + right * move.x;
                dir.y = 0f;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    return dir.normalized;
                }
            }

            if (_animator != null)
            {
                Vector3 facing = _animator.transform.forward;
                facing.y = 0f;
                if (facing.sqrMagnitude > 1e-6f)
                {
                    return facing.normalized;
                }
            }

            return Vector3.forward;
        }

        private void EndDodge()
        {
            Cancel();
        }
    }
}

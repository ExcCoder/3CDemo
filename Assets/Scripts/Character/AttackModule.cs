using CCCProject.Core;
using CCCProject.Inputs;
using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 地面连段攻击：Jab → Cross。
    /// 负责输入接受、连段窗口、单槽时效缓冲、动作控制权交接与命中结算。
    /// </summary>
    /// <remarks>
    /// 纯 C# 类：生命周期由 <see cref="ModuleCenter"/> 管理，每帧由 <see cref="ITickable"/> 驱动，
    /// 依赖（输入 / 角色运动 / Animator / 事件转发器）由组合根构造注入。
    /// <para>
    /// 关键约定：<b>动画状态退出不等于一次动作结束</b>。
    /// Jab 退出到 Cross 时动作仍在继续，因此不能在该退出点释放控制权。
    /// 状态退出回调只负责把事实交给本模块，由本模块判断是"接下一段"还是"结束整串动作"。
    /// </para>
    /// </remarks>
    public sealed class AttackModule : IGameModule, ITickable, IDodgeCancelable
    {
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int AttackNextHash = Animator.StringToHash("AttackNext");

        /// <summary>第一段：Jab。</summary>
        private const int StepJab = 0;

        /// <summary>第二段：Cross，也是本轮连段的最后一段。</summary>
        private const int StepCross = 1;

        /// <summary>状态一致性检查的宽限期（秒）：刚触发后允许 Animator 还没完成过渡。</summary>
        private const float StateGracePeriod = 0.35f;

        private readonly InputModule _input;
        private readonly PlayerModule _motor;
        private readonly Animator _animator;
        private readonly AttackRelay _relay;
        private readonly Transform _owner;

        private int _step;
        private bool _hitThisStep;
        private bool _comboWindowOpen;
        private bool _chainRequested;
        private float _bufferExpiry;
        private string _expectedState;
        private float _expectedSince;
        private bool _seenExpectedState;
        private bool _chainCounted;
        private bool _pendingStateExit;
        private bool _dodgeWindowOpen;

        /// <summary>当前动作阶段。</summary>
        public string Phase { get; private set; } = "Ready";

        /// <summary>当前段索引：0 = Jab，1 = Cross。</summary>
        public int Step => _step;

        /// <summary>已接受的动作次数（Jab 与 Cross 分别计数）。</summary>
        public int AttackCount { get; private set; }

        /// <summary>成功命中目标的次数。</summary>
        public int HitCount { get; private set; }

        /// <summary>成功完成的连段次数。</summary>
        public int ChainCount { get; private set; }

        /// <summary>当前是否有尚未过期的缓冲请求。</summary>
        public bool HasBufferedInput => _bufferExpiry > Time.time;

        /// <summary>动作是否正在进行中（还没回到可自由行动的状态）。</summary>
        public bool IsActionActive => Phase != "Ready";

        /// <summary>当前是否处于允许翻滚打断的取消窗口。</summary>
        public bool DodgeCancelOpen => _dodgeWindowOpen;

        /// <summary>构造连段攻击模块。</summary>
        /// <param name="input">语义输入。</param>
        /// <param name="motor">角色运动事实与控制权接口。</param>
        /// <param name="animator">角色 Animator。</param>
        /// <param name="relay">动画事件转发器。</param>
        /// <param name="owner">角色根节点，用于把命中检测偏移转成世界坐标。</param>
        public AttackModule(
            InputModule input,
            PlayerModule motor,
            Animator animator,
            AttackRelay relay,
            Transform owner)
        {
            _input = input;
            _motor = motor;
            _animator = animator;
            _relay = relay;
            _owner = owner;
        }

        /// <summary>启动：绑定动画事件接收器。</summary>
        public void OnLaunch()
        {
            if (_relay != null)
            {
                _relay.Module = this;
            }
        }

        /// <summary>关闭：清理动作状态并解除绑定。</summary>
        public void OnShutdown()
        {
            Cancel();
            if (_relay != null)
            {
                _relay.Module = null;
            }
        }

        /// <summary>每帧：读取攻击输入、维护缓冲时效、检查连段衔接与状态一致性。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            if (_animator == null || !_animator.isActiveAndEnabled)
            {
                Cancel();
                return;
            }

            if (_input != null && _input.AttackPressed)
            {
                TryAttack();
            }

            if (_pendingStateExit)
            {
                ResolveStateExit();
            }

            UpdateComboRequest();
            CheckStateConsistency();
            ObserveSecondSegment();
        }

        /// <summary>
        /// 尝试攻击。空闲时立即开始第一段；动作进行中则把请求写入单槽缓冲。
        /// </summary>
        /// <returns>是否真的开始了一段新动作（写入缓冲时返回 false）。</returns>
        public bool TryAttack()
        {
            if (_animator == null || !_animator.isActiveAndEnabled)
            {
                return false;
            }

            if (Phase != "Ready")
            {
                // 动作进行中：只有第一段允许接后续，最后一段的请求直接丢弃。
                if (_step == StepJab)
                {
                    BufferRequest();
                }

                return false;
            }

            if (!CanStartAction())
            {
                return false;
            }

            StartStep(StepJab);
            return true;
        }

        /// <summary>动画命中事件：结算本段命中，并打开连段窗口。</summary>
        public void Strike()
        {
            if (Phase == "Ready" || _hitThisStep)
            {
                return;
            }

            _hitThisStep = true;
            Phase = "Active";

            // 命中之后进入连段窗口：此时按下攻击才算数，太早的请求会因缓冲过期被丢弃。
            if (_step == StepJab)
            {
                _comboWindowOpen = true;
            }

            Vector3 center = _owner.TransformPoint(_relay.HitOffset);
            var targets = new System.Collections.Generic.HashSet<TrainingDummy>();
            foreach (var collider in Physics.OverlapSphere(
                         center, _relay.HitRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var dummy = collider.GetComponentInParent<TrainingDummy>();
                if (dummy == null || !targets.Add(dummy))
                {
                    continue;
                }

                Vector3 start = _owner.position + Vector3.up;
                Vector3 aim = collider.ClosestPoint(start);
                if (Physics.Linecast(
                        start, aim,
                        LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                dummy.ReceiveHit();
                HitCount++;
            }
        }

        /// <summary>动画收招事件：进入收招阶段，保持动作约束。</summary>
        public void Recover()
        {
            if (Phase != "Ready")
            {
                Phase = "Recovery";
            }
        }

        /// <summary>
        /// 动画事件：打开翻滚取消窗口。
        /// </summary>
        /// <remarks>
        /// 窗口由动画事件给出，不在代码里写死时间：
        /// 取消窗口属于"动作承诺"的一部分，必须跟着动作节奏走。
        /// 窗口打开前按翻滚无效（这一段动作已经承诺出去了），
        /// 打开后按翻滚才会打断当前攻击。
        /// </remarks>
        public void OpenDodgeWindow()
        {
            if (Phase != "Ready")
            {
                _dodgeWindowOpen = true;
            }
        }

        /// <summary>
        /// 接受翻滚打断：放弃当前攻击，把控制权交给翻滚。
        /// </summary>
        /// <remarks>
        /// 由翻滚在确认取消窗口打开后调用，且翻滚已经先取得动作锁，
        /// 因此这里释放自己的锁不会误开翻滚持有的那一份（锁带所有者）。
        /// 连段缓冲一并清掉：翻滚结束后不应该再自动接出第二拳。
        /// </remarks>
        public void AcceptDodgeCancel()
        {
            _dodgeWindowOpen = false;
            Cancel();
        }

        /// <summary>
        /// 攻击动画状态退出时回调。
        /// </summary>
        /// <remarks>
        /// 这里只记录"有状态退出了"，不立刻下结论。
        /// 回调触发的那一刻，Animator 的过渡信息可能还没建立完整，
        /// 读不到"下一段要去哪"，因此判断推迟到下一次 <see cref="Tick"/>。
        /// </remarks>
        public void OnStateExited()
        {
            _pendingStateExit = true;
        }

        /// <summary>
        /// 处理上一次状态退出：是段间交接，还是整串动作结束。
        /// </summary>
        private void ResolveStateExit()
        {
            // 交接的判据：Animator 当前就在第二段，或者正过渡到第二段。
            bool onSecondSegment = _animator.GetCurrentAnimatorStateInfo(0).IsName("AttackCross");
            bool headingToSecondSegment = _animator.IsInTransition(0)
                && _animator.GetNextAnimatorStateInfo(0).IsName("AttackCross");

            if (_chainRequested && (onSecondSegment || headingToSecondSegment))
            {
                _chainRequested = false;
                _pendingStateExit = false;
                _step = StepCross;
                _hitThisStep = false;
                _comboWindowOpen = false;
                _expectedState = "AttackCross";
                _expectedSince = Time.time;
                _seenExpectedState = false;
                Phase = "Startup";
                return;
            }

            // 没接上第二段：整串动作结束，归还控制权。
            _pendingStateExit = false;
            Cancel();
        }



        /// <summary>结束整串动作：清空缓冲、释放动作约束、复位触发器。</summary>
        public void Cancel()
        {
            Phase = "Ready";
            _step = StepJab;
            _hitThisStep = false;
            _comboWindowOpen = false;
            _chainRequested = false;
            _bufferExpiry = 0f;
            _seenExpectedState = false;
            _dodgeWindowOpen = false;
            _motor.ReleaseActionLock(this);

            if (_animator != null)
            {
                _animator.ResetTrigger(AttackHash);
                _animator.ResetTrigger(AttackNextHash);
            }
        }

        /// <summary>
        /// 观测第二段是否真的开始播放。
        /// </summary>
        /// <remarks>
        /// 连段计数不能挂在状态回调上：Animator 在一次过渡里会同时发出
        /// "上一段退出"和"下一段进入"，两者的先后顺序不保证。
        /// 改成在每帧主动观测"当前是否真的在播 AttackCross"，结果只取决于事实。
        /// </remarks>
        private void ObserveSecondSegment()
        {
            if (_step != StepCross || _chainCounted || _animator.IsInTransition(0))
            {
                return;
            }

            if (_animator.GetCurrentAnimatorStateInfo(0).IsName("AttackCross"))
            {
                _chainCounted = true;
                ChainCount++;
            }
        }
        /// <summary>是否满足开始一段新动作的条件。</summary>
        private bool CanStartAction()
        {
            if (_motor.MovementLocked || _motor.IsAirborne || !_motor.IsGrounded)
            {
                return false;
            }

            // 动作锁被别的动作占着（翻滚进行中）时不能起手攻击。
            if (_motor.ActionLocked)
            {
                return false;
            }

            if (_animator.IsInTransition(0))
            {
                return false;
            }

            return _animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");
        }

        /// <summary>开始一段动作：占用控制权、复位本段命中记录、通知 Animator。</summary>
        /// <param name="step">段索引。</param>
        private void StartStep(int step)
        {
            _step = step;
            _hitThisStep = false;
            _comboWindowOpen = false;
            _expectedState = step == StepJab ? "AttackJab" : "AttackCross";
            _expectedSince = Time.time;
            _seenExpectedState = false;
            _chainCounted = false;
            _dodgeWindowOpen = false;
            Phase = "Startup";
            AttackCount++;
            _motor.AcquireActionLock(this);
            _animator.ResetTrigger("Jump");
            _animator.SetTrigger(AttackHash);
        }

        /// <summary>把本次攻击请求写入单槽缓冲，并记录到期时间。</summary>
        private void BufferRequest()
        {
            _bufferExpiry = Time.time + _relay.ComboBufferLifetime;
        }

        /// <summary>连段窗口打开且有有效缓冲时，请求衔接下一段。</summary>
        private void UpdateComboRequest()
        {
            if (Phase == "Ready" || _chainRequested || !_comboWindowOpen)
            {
                return;
            }

            if (_step != StepJab || !HasBufferedInput)
            {
                return;
            }

            // 消费缓冲：同一次请求不会被使用两次。
            _bufferExpiry = 0f;
            _chainRequested = true;
            _animator.SetTrigger(AttackNextHash);
        }

        /// <summary>
        /// 状态一致性检查：确认 Animator 确实处在预期的攻击状态。
        /// </summary>
        /// <remarks>
        /// 需要留一段宽限期：刚触发时 Animator 还没开始过渡，当前状态仍是上一段；
        /// 若立刻判定不一致，会把刚发出的攻击误判成"被打断"而取消。
        /// 宽限期结束仍未进入目标状态，才认为动作真的丢了。
        /// </remarks>
        private void CheckStateConsistency()
        {
            if (Phase == "Ready" || _animator.IsInTransition(0))
            {
                return;
            }

            if (_animator.GetCurrentAnimatorStateInfo(0).IsName(_expectedState))
            {
                _seenExpectedState = true;
                return;
            }

            if (_seenExpectedState)
            {
                // 已经进过目标状态，之后又离开了：这是被打断。
                Cancel();
                return;
            }

            if (Time.time - _expectedSince > StateGracePeriod)
            {
                Cancel();
            }
        }
    }
}

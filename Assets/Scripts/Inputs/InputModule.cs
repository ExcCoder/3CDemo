using CCCProject.Core;
using UnityEngine;

namespace CCCProject.Inputs
{
    /// <summary>
    /// 输入模块：把旧 Input Manager 的原始轴收敛成语义输入（移动向量、走/跑修饰）。
    /// 只做"读输入 → 输出语义"，不引用角色与相机；以后换输入方案只改本类。
    /// </summary>
    /// <remarks>
    /// 纯 C# 类：生命周期由 <see cref="ModuleCenter"/> 管理，每帧由 <see cref="ITickable"/> 驱动，
    /// 因此"输入先于角色更新"是注册顺序保证的，不依赖脚本执行顺序。
    /// <para>
    /// 命名空间用 <c>CCCProject.Inputs</c> 而不是 <c>CCCProject.Input</c>：后者会与 <c>UnityEngine.Input</c> 冲突，
    /// 导致本命名空间内写 <c>Input.GetAxis</c> 时被解析成命名空间而编译失败。
    /// </para>
    /// </remarks>
    public sealed class InputModule : IGameModule, ITickable
    {
        private readonly string _horizontalAxis;
        private readonly string _verticalAxis;
        private readonly string _walkButton;
        private readonly string _jumpButton;
        private readonly string _dodgeButton;
        private readonly float _deadZone;

        /// <summary>移动输入（已施加死区，长度上限 1）。x 为右、y 为前。</summary>
        public Vector2 Move { get; private set; }

        /// <summary>是否处于"走"状态（按住修饰键，模拟摇杆半推）。</summary>
        public bool IsWalking { get; private set; }

        /// <summary>跳跃键本帧刚按下（边沿触发）；空中按下会在角色落地时被丢弃。</summary>
        public bool JumpPressed { get; private set; }

        /// <summary>鼠标左键的本帧按下边沿。</summary>
        public bool AttackPressed { get; private set; }

        /// <summary>翻滚键的本帧按下边沿（默认右鼠标键）。</summary>
        public bool DodgePressed { get; private set; }

        /// <summary>移动输入是否有效（超过死区）。</summary>
        public bool HasMoveInput => Move.sqrMagnitude > 0f;

        /// <summary>构造输入层。</summary>
        /// <param name="horizontalAxis">水平移动轴名。</param>
        /// <param name="verticalAxis">垂直移动轴名。</param>
        /// <param name="walkButton">走 / 跑修饰键（留空则没有修饰键）。</param>
        /// <param name="jumpButton">跳跃键（旧 Input Manager 的 "Jump" 轴，默认空格；留空则不能跳）。</param>
        /// <param name="dodgeButton">翻滚键（旧 Input Manager 的 "Fire2" 轴，默认右鼠标；留空则不能翻滚）。</param>
        /// <param name="deadZone">摇杆死区。</param>
        public InputModule(
            string horizontalAxis = "Horizontal",
            string verticalAxis = "Vertical",
            string walkButton = "Fire3",
            string jumpButton = "Jump",
            string dodgeButton = "Fire2",
            float deadZone = 0.15f)
        {
            _horizontalAxis = horizontalAxis;
            _verticalAxis = verticalAxis;
            _walkButton = walkButton;
            _jumpButton = jumpButton;
            _dodgeButton = dodgeButton;
            _deadZone = deadZone;
        }

        /// <summary>启动：输入层无需初始化。</summary>
        public void OnLaunch()
        {
        }

        /// <summary>关闭：清空输入，避免下次启动残留。</summary>
        public void OnShutdown()
        {
            Move = Vector2.zero;
            IsWalking = false;
            JumpPressed = false;
            AttackPressed = false;
            DodgePressed = false;
        }

        /// <summary>每帧读一次输入并输出语义值。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            Vector2 raw = new Vector2(
                UnityEngine.Input.GetAxisRaw(_horizontalAxis),
                UnityEngine.Input.GetAxisRaw(_verticalAxis));

            if (raw.sqrMagnitude > 1f)
            {
                raw.Normalize();
            }

            if (raw.sqrMagnitude < _deadZone * _deadZone)
            {
                raw = Vector2.zero;
            }

            IsWalking = !string.IsNullOrEmpty(_walkButton) && UnityEngine.Input.GetButton(_walkButton);
            JumpPressed = !string.IsNullOrEmpty(_jumpButton) && UnityEngine.Input.GetButtonDown(_jumpButton);
            Move = raw;
            AttackPressed = UnityEngine.Input.GetMouseButtonDown(0);
            DodgePressed = !string.IsNullOrEmpty(_dodgeButton) && UnityEngine.Input.GetButtonDown(_dodgeButton);
        }
    }
}

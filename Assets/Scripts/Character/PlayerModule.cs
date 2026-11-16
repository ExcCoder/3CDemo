using CCCProject.CameraSystem;
using CCCProject.Core;
using CCCProject.Inputs;
using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 角色移动模块（运动学基线）：语义输入 → 期望速度/朝向 → <see cref="CharacterController"/> 位移。
    /// 只向下读取 <see cref="InputModule"/> 与 <see cref="CameraModule"/>，不引用相机实现、不认识状态机；
    /// 以后攻击/翻滚只需要设置 <see cref="MovementLocked"/>。
    /// </summary>
    /// <remarks>
    /// 纯 C# 类：生命周期由 <see cref="ModuleCenter"/> 管理，每帧由 <see cref="ITickable"/> 驱动，
    /// 依赖（控制器 / 参数 / 输入 / 相机）由组合根在构造时注入。
    /// </remarks>
    public sealed class PlayerModule : IGameModule, ITickable, ICharacterMotor
    {
        private readonly CharacterController _controller;
        private readonly CharacterMovementConfig _config;
        private readonly InputModule _input;
        private readonly CameraModule _camera;

        private Vector3 _planarVelocity;
        private float _verticalVelocity;
        private bool _airborne;
        private bool _jumpRequested;
        private bool _hasScriptedMotion;
        private Vector3 _scriptedDirection;
        private float _scriptedDistance;
        private bool _landingRecovery;
        private int _landingSequence;
        private System.Func<int, bool> _landingCompletionProbe;

        /// <summary>当前速度（含垂直分量）。</summary>
        public Vector3 Velocity => _planarVelocity + Vector3.up * _verticalVelocity;

        /// <summary>当前水平速度大小。</summary>
        public float PlanarSpeed => _planarVelocity.magnitude;

        /// <summary>相机朝向的水平投影，作为移动方向的基准。</summary>
        public Vector3 ViewForward => GetViewForward();

        /// <summary>是否贴地。</summary>
        public bool IsGrounded => _controller != null && _controller.isGrounded;

        /// <summary>是否离地（跳跃滞空中）。走下坡不算离地，只有起跳之后才算。</summary>
        public bool IsAirborne => _airborne;

        /// <summary>移动锁：锁定后不再响应移动输入，只做减速与重力。</summary>
        public bool MovementLocked { get; set; }

        /// <summary>是否处于落地恢复期：落地后摇期间禁止位移，但翻滚可以打断。</summary>
        public bool LandingRecovery => _landingRecovery;

        private object _actionOwner;

        /// <summary>单次动作占用移动与转向；与外部移动锁分开释放。</summary>
        public bool ActionLocked => _actionOwner != null;

        /// <summary>
        /// 占用动作控制权：清空水平速度与跳跃请求，并把锁记到 <paramref name="owner"/> 名下。
        /// </summary>
        /// <remarks>
        /// 锁带所有者是"动作交接"的前提：攻击被翻滚打断时，
        /// 攻击结束时释放自己的那份锁，不会把翻滚正在持有的锁一起放开。
        /// </remarks>
        /// <param name="owner">占用者（一般传 this）。</param>
        public void AcquireActionLock(object owner)
        {
            if (owner == null)
            {
                return;
            }

            _actionOwner = owner;
            _jumpRequested = false;
            _planarVelocity = Vector3.zero;
        }

        /// <summary>
        /// 释放动作控制权：只有当前所有者能释放，别人调用是空操作。
        /// </summary>
        /// <param name="owner">释放者（一般传 this）。</param>
        public void ReleaseActionLock(object owner)
        {
            if (!ReferenceEquals(_actionOwner, owner))
            {
                return;
            }

            _actionOwner = null;
        }

        /// <summary>
        /// 取消落地恢复期：翻滚等动作可以打断落地后摇。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="AcquireActionLock"/> 分开：落地恢复是"地面状态"，
        /// 翻滚是"动作占用"。两者独立释放，互不误开。
        /// </remarks>
        public void CancelLandingRecovery()
        {
            _landingRecovery = false;
            unchecked { _landingSequence++; }
        }

        /// <summary>由组合根绑定落地动画完成查询；运动层不依赖 Animator 类型。</summary>
        /// <param name="completionProbe">当前落地序号是否已播完并稳定返回移动状态。</param>
        public void BindLandingCompletion(System.Func<int, bool> completionProbe)
        {
            _landingCompletionProbe = completionProbe;
        }

        /// <summary>构造角色移动模块。</summary>
        /// <param name="controller">角色的 CharacterController。</param>
        /// <param name="config">移动手感参数资产。</param>
        /// <param name="input">语义输入。</param>
        /// <param name="camera">相机模块（提供相机相对移动的基准朝向）。</param>
        public PlayerModule(
            CharacterController controller,
            CharacterMovementConfig config,
            InputModule input,
            CameraModule camera)
        {
            _controller = controller;
            _config = config;
            _input = input;
            _camera = camera;
        }

        /// <summary>启动：校验依赖。</summary>
        public void OnLaunch()
        {
            if (_controller == null)
            {
                Debug.LogError("[PlayerModule] 未指定 CharacterController，角色不会移动。");
                return;
            }

            if (_config == null)
            {
                Debug.LogError("[PlayerModule] 未指定 CharacterMovementConfig，角色不会移动。");
                return;
            }

            if (_input == null || _camera == null)
            {
                Debug.LogError("[PlayerModule] 缺少 InputModule 或 CameraModule 依赖，请检查模块注册顺序。");
            }
        }

        /// <summary>关闭：停止移动并清空速度。</summary>
        public void OnShutdown()
        {
            _planarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _airborne = false;
            _jumpRequested = false;
            _hasScriptedMotion = false;
            _scriptedDistance = 0f;
            CancelLandingRecovery();
            _landingCompletionProbe = null;
        }

        /// <summary>每帧：读意图 → 求解 → 应用位移。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            if (_controller == null || _config == null || _input == null || _camera == null)
            {
                return;
            }

            // 读上一帧 Animator 已完成的事实，再计算本帧输入位移。
            // 完整退出 JumpLand（包括混合）才放行；快慢播放不再与计时锁脱节。
            if (_landingRecovery && _landingCompletionProbe != null
                && _landingCompletionProbe(_landingSequence))
            {
                _landingRecovery = false;
            }

            // 翻滚（脚本位移）期间不接受跳跃：动作期间不能让角色离地。
            if (!ActionLocked && !_hasScriptedMotion && !LandingRecovery
                && _input != null && _input.JumpPressed)
            {
                RequestJump();
            }


            SolvePlanarVelocity(deltaTime);
            SolveRotation(deltaTime);
            SolveVertical(deltaTime);

            Vector3 displacement = (_planarVelocity + Vector3.up * _verticalVelocity) * deltaTime;

            // 脚本位移（翻滚）与普通位移叠加，一起交给 CharacterController，
            // 这样翻滚撞墙时会被碰撞截断，而不是穿墙过去。
            if (_hasScriptedMotion)
            {
                displacement += _scriptedDirection * _scriptedDistance;
                _scriptedDistance = 0f;
                _hasScriptedMotion = false;
            }

            CollisionFlags collisions = _controller.Move(displacement);
            if (_airborne && _verticalVelocity <= 0f && (collisions & CollisionFlags.Below) != 0)
            {
                // 本次 Move 才是本帧接触事实；同帧归零，后续动画 Tick 不再多等一帧。
                _airborne = false;
                _verticalVelocity = _config.GroundStick;
                _jumpRequested = false;
                BeginLandingRecovery();
            }
        }

        /// <summary>施加一次水平冲量（击退、推挤等由局部系统调用）。</summary>
        public void AddPlanarImpulse(Vector3 impulse)
        {
            _planarVelocity += new Vector3(impulse.x, 0f, impulse.z);
        }

        /// <summary>
        /// 交由脚本驱动的水平位移（翻滚用）：本帧沿指定方向前进指定距离。
        /// </summary>
        /// <remarks>
        /// 与输入驱动的移动互斥：本帧只要设置了脚本位移，就不再读取移动输入，
        /// 避免"翻滚同时被输入拽走"。位移在 <see cref="Tick"/> 里与其他位移一起累加，
        /// 因此仍受 <see cref="CharacterController"/> 的碰撞约束——撞墙会自然截断。
        /// </remarks>
        /// <param name="direction">位移方向（世界坐标，水平面内）。</param>
        /// <param name="distance">本帧要前进的距离（米）。</param>
        public void SetScriptedPlanarMotion(Vector3 direction, float distance)
        {
            if (distance <= 0f)
            {
                _hasScriptedMotion = false;
                _scriptedDistance = 0f;
                return;
            }

            _hasScriptedMotion = true;
            _jumpRequested = false;
            direction.y = 0f;
            _scriptedDirection = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            _scriptedDistance = distance;
        }

        /// <summary>请求起跳：在地面时于下一次 tick 生效；空中请求会被丢弃（不做二段跳）。</summary>
        public void RequestJump()
        {
            _jumpRequested = true;
        }

        /// <summary>ReadIntent + Solve：算出期望的水平速度。</summary>
        private void SolvePlanarVelocity(float deltaTime)
        {
            if (_hasScriptedMotion)
            {
                // 脚本位移接管本帧：不再读取移动输入，也不参与加速减速。
                _planarVelocity = Vector3.zero;
                return;
            }

            if (LandingRecovery)
            {
                // 落地恢复期：钉在原地，不响应移动输入，也不做减速过渡。
                // 直接归零而不是缓慢减速，是为了避免落地瞬间残留速度造成滑行。
                _planarVelocity = Vector3.zero;
                return;
            }

            Vector2 move = (MovementLocked || ActionLocked) ? Vector2.zero : _input.Move;

            Vector3 desired = Vector3.zero;
            if (move.sqrMagnitude > 0f)
            {
                desired = GetViewForward() * move.y + GetViewRight() * move.x;
                if (desired.sqrMagnitude > 0f)
                {
                    desired.Normalize();
                }
            }

            float throttle = Mathf.Clamp01(move.magnitude);
            float targetSpeed = _input.IsWalking
                ? _config.WalkSpeed
                : Mathf.Lerp(_config.WalkSpeed, _config.RunSpeed, throttle);

            Vector3 targetVelocity = desired * targetSpeed;
            float rate = targetVelocity.magnitude < _planarVelocity.magnitude
                ? _config.Deceleration
                : _config.Acceleration;

            _planarVelocity = Vector3.MoveTowards(_planarVelocity, targetVelocity, rate * deltaTime);
        }

        /// <summary>Solve：转身朝向移动方向，有转速上限。</summary>
        private void SolveRotation(float deltaTime)
        {
            if (_hasScriptedMotion)
            {
                // 翻滚期间朝脚本方向转，用转向速度上限保持"动作承诺"的观感。
                Quaternion scriptedLook = Quaternion.LookRotation(_scriptedDirection, Vector3.up);
                _controller.transform.rotation = Quaternion.RotateTowards(
                    _controller.transform.rotation, scriptedLook, _config.TurnSpeed * deltaTime);
                return;
            }

            if (LandingRecovery || ActionLocked || _planarVelocity.sqrMagnitude < 0.01f)
            {
                return;
            }

            Transform owner = _controller.transform;
            Quaternion look = Quaternion.LookRotation(_planarVelocity.normalized, Vector3.up);
            owner.rotation = Quaternion.RotateTowards(owner.rotation, look, _config.TurnSpeed * deltaTime);
        }

        /// <summary>
        /// 开始落地恢复期：清掉残留水平速度，锁定位移直到后摇结束。
        /// </summary>
        /// <remarks>
        /// 落地瞬间如果保留水平速度，角色会在播放落地动画的同时继续平移，
        /// 看起来像"滑过去"。这里直接清零并锁住位移，恢复期结束后再交还移动控制。
        /// </remarks>
        private void BeginLandingRecovery()
        {
            _landingRecovery = true;
            unchecked { _landingSequence++; }
            _planarVelocity = Vector3.zero;
        }
        /// <summary>Apply：跳跃与竖直运动（起跳 / 滞空 / 落地贴地）。</summary>
        private void SolveVertical(float deltaTime)
        {
            bool grounded = _controller.isGrounded;

            if (grounded && !_airborne)
            {
                // 在地面：先看有没有起跳请求，没有就贴地。
                if (_jumpRequested && !MovementLocked && !ActionLocked && !LandingRecovery)
                {
                    _jumpRequested = false;
                    _airborne = true;
                    _verticalVelocity = _config.JumpVelocity;
                    return;
                }

                _verticalVelocity = _config.GroundStick;
                return;
            }


            // 空中：丢弃跳跃请求（不做二段跳），只受重力。
            _jumpRequested = false;
            _verticalVelocity += _config.Gravity * deltaTime;
        }

        /// <summary>相机朝向的水平投影，作为移动基准。</summary>
        private Vector3 GetViewForward()
        {
            Transform view = _camera != null ? _camera.ViewTransform : null;
            Vector3 forward = view != null ? view.forward : _controller.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = _controller.transform.forward;
                forward.y = 0f;

                if (forward.sqrMagnitude < 0.0001f)
                {
                    forward = Vector3.forward;
                }
            }

            return forward.normalized;
        }

        private Vector3 GetViewRight()
        {
            return Vector3.Cross(Vector3.up, GetViewForward());
        }
    }
}

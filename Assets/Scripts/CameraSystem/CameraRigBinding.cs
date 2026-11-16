using Cinemachine;
using UnityEngine;

namespace CCCProject.CameraSystem
{
    /// <summary>
    /// 相机引擎适配器：把 <see cref="CameraSettings"/> 翻译成 Cinemachine 组件上的具体数值，
    /// 并把碰撞避让装到三条 orbit rig 上。必须挂在 FreeLook 虚拟相机所在物体上。
    /// </summary>
    /// <remarks>
    /// 为什么碰撞避让要在 rig 上装三份：<c>CinemachineCollider</c> 只在
    /// <c>CinemachineCore.Stage.Body</c> 生效，而 <c>CinemachineFreeLook</c> 自己只在
    /// <c>Finalize</c> 阶段回调扩展；真正跑完整管线的，是它每帧驱动的三条子 rig。
    /// 装在外层 FreeLook 上的 Collider 会静默失效。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineFreeLook))]
    public sealed class CameraRigBinding : MonoBehaviour, ICameraRig
    {
        private const int RigCount = 3;

        [SerializeField, Tooltip("主相机上的 CinemachineBrain：门面用它取实际输出相机。")]
        private CinemachineBrain _brain;

        private readonly CinemachineCollider[] _colliders = new CinemachineCollider[RigCount];
        private readonly CameraSphereSolver[] _solvers = new CameraSphereSolver[RigCount];

        private CinemachineFreeLook _freeLook;
        private CameraInputAxisProvider _inputProvider;
        private CameraSettings _settings;
        private float _armScale = 1f;

        /// <summary>实际输出相机的 Transform；Brain 或输出相机未就绪时为 null。</summary>
        public Transform OutputTransform
        {
            get
            {
                if (_brain == null || _brain.OutputCamera == null)
                {
                    return null;
                }

                return _brain.OutputCamera.transform;
            }
        }

        private void Awake()
        {
            _freeLook = GetComponent<CinemachineFreeLook>();
            _inputProvider = GetComponent<CameraInputAxisProvider>();
            EnsureCollisionComponents();
        }

        /// <summary>绑定跟随目标：Follow 与 LookAt 分别指向角色的两个锚点。</summary>
        /// <param name="target">角色身上的相机目标；null 表示解绑。</param>
        public void BindTarget(CameraTarget target)
        {
            if (_freeLook == null)
            {
                _freeLook = GetComponent<CinemachineFreeLook>();
            }

            if (_freeLook == null)
            {
                return;
            }

            _freeLook.Follow = target != null ? target.FollowAnchor : null;
            _freeLook.LookAt = target != null ? target.LookAnchor : null;
        }

        /// <summary>把参数资产写进 FreeLook 的三条 orbit、镜头与三份碰撞避让。</summary>
        /// <param name="settings">相机参数资产。</param>
        public void Apply(CameraSettings settings)
        {
            if (settings == null)
            {
                Debug.LogWarning("[CameraRigBinding] 没有相机参数资产，继续沿用场景里的旧参数。");
                return;
            }

            if (_freeLook == null)
            {
                _freeLook = GetComponent<CinemachineFreeLook>();
            }

            if (_freeLook == null)
            {
                Debug.LogError("[CameraRigBinding] 同物体上没有 CinemachineFreeLook，相机参数无法应用。");
                return;
            }

            _settings = settings;
            _armScale = 1f;

            ApplyArms();
            ApplyLens();
            ApplyCollision();
            ApplySphereSolver();
        }

        /// <summary>按比例拉近 / 推远相机臂（1 为标称距离）。</summary>
        /// <param name="scale">缩放系数，最小 0.05。</param>
        public void SetArmDistanceScale(float scale)
        {
            _armScale = Mathf.Max(0.05f, scale);
            ApplyArms();
        }

        /// <summary>开关视角输入。</summary>
        /// <param name="enabled">是否允许读取视角输入。</param>
        public void SetInputEnabled(bool enabled)
        {
            if (_inputProvider == null)
            {
                _inputProvider = GetComponent<CameraInputAxisProvider>();
            }

            if (_inputProvider != null)
            {
                _inputProvider.InputEnabled = enabled;
            }
        }

        /// <summary>三条 orbit：索引 0 上、1 中、2 下（与 FreeLook 的 m_Orbits 顺序一致）。</summary>
        private void ApplyArms()
        {
            if (_settings == null || _freeLook == null)
            {
                return;
            }

            CinemachineFreeLook.Orbit[] orbits = _freeLook.m_Orbits;
            if (orbits == null || orbits.Length < RigCount)
            {
                Debug.LogError("[CameraRigBinding] FreeLook 的 m_Orbits 不是三条，臂长参数无法应用。");
                return;
            }

            WriteArm(orbits, 0, _settings.TopArm);
            WriteArm(orbits, 1, _settings.MiddleArm);
            WriteArm(orbits, 2, _settings.BottomArm);

            // FreeLook 每帧比对缓存与 m_Orbits 决定是否重算样条，直接改数组元素即可生效。
            _freeLook.m_Orbits = orbits;
        }

        private void WriteArm(CinemachineFreeLook.Orbit[] orbits, int index, CameraSettings.Arm arm)
        {
            orbits[index].m_Height = arm.Height;
            orbits[index].m_Radius = arm.Radius * _armScale;
        }

        private void ApplyLens()
        {
            if (_settings == null || _freeLook == null)
            {
                return;
            }

            _freeLook.m_Lens.FieldOfView = _settings.FieldOfView;
        }

        /// <summary>把碰撞参数写到三条 rig 的 CinemachineCollider 上（没有就装一个）。</summary>
        private void ApplyCollision()
        {
            if (_settings == null)
            {
                return;
            }

            EnsureCollisionComponents();

            int mask = _settings.CollisionMask;
            if (_settings.CollisionEnabled && mask == 0)
            {
                Debug.LogWarning(
                    "[CameraRigBinding] 遮挡层掩码为 0：检查 CameraSettings 的层名拼写，" +
                    "以及 ProjectSettings/TagManager 里是否建了这些层。相机不会做碰撞避让。");
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                CinemachineCollider collider = _colliders[i];
                if (collider == null)
                {
                    continue;
                }

                collider.m_AvoidObstacles = _settings.CollisionEnabled && !_settings.UseOwnSolver;
                collider.m_CollideAgainst = mask;
                if (_settings.CollisionStrategy == CameraCollisionStrategy.PullIn)
                {
                    collider.m_Strategy = CinemachineCollider.ResolutionStrategy.PullCameraForward;
                }
                else
                {
                    collider.m_Strategy = CinemachineCollider.ResolutionStrategy.PreserveCameraHeight;
                    
                }
                collider.m_CameraRadius = _settings.CameraRadius;
                collider.m_MinimumDistanceFromTarget = _settings.MinimumDistanceFromTarget;
                collider.m_Damping = _settings.ReturnDamping;
                collider.m_DampingWhenOccluded = _settings.OcclusionDamping;
                collider.m_MinimumOcclusionTime = _settings.MinimumOcclusionTime;
                collider.m_SmoothingTime = _settings.OcclusionSmoothing;
                collider.m_IgnoreTag = string.Empty;
                collider.m_TransparentLayers = 0;
            }
        }

        /// <summary>按设置启用 / 关闭自研球扫求解器，并把参数灌进去（关掉时掩码置 0，扩展直接早退）。</summary>
        private void ApplySphereSolver()
        {
            if (_settings == null || _freeLook == null)
            {
                return;
            }

            int mask = _settings.CollisionEnabled && _settings.UseOwnSolver ? _settings.CollisionMask : 0;
            for (int i = 0; i < RigCount; i++)
            {
                CinemachineVirtualCamera rig = _freeLook.GetRig(i);
                if (rig == null)
                {
                    continue;
                }

                CameraSphereSolver solver = rig.GetComponent<CameraSphereSolver>();
                if (solver == null)
                {
                    solver = rig.gameObject.AddComponent<CameraSphereSolver>();
                }

                _solvers[i] = solver;
                solver.Configure(
                    _settings.CameraRadius,
                    _settings.MinimumDistanceFromTarget,
                    _settings.SolverIterations,
                    _settings.PullOutSpeed,
                    _settings.PullInSpeed,
                    mask);
            }
        }
        /// <summary>确保三条 rig 上各有一个 CinemachineCollider，并缓存引用。</summary>
        private void EnsureCollisionComponents()
        {
            if (_freeLook == null)
            {
                _freeLook = GetComponent<CinemachineFreeLook>();
            }

            if (_freeLook == null)
            {
                return;
            }

            for (int i = 0; i < RigCount; i++)
            {
                if (_colliders[i] != null)
                {
                    continue;
                }

                CinemachineVirtualCamera rig = _freeLook.GetRig(i);
                if (rig == null)
                {
                    Debug.LogWarning($"[CameraRigBinding] 找不到第 {i} 条 rig，碰撞避让只装了 {i} 份。");
                    continue;
                }

                CinemachineCollider collider = rig.GetComponent<CinemachineCollider>();
                if (collider == null)
                {
                    collider = rig.gameObject.AddComponent<CinemachineCollider>();
                }

                _colliders[i] = collider;
            }
        }
    }
}

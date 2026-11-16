using UnityEngine;

namespace CCCProject.CameraSystem
{
    /// <summary>
    /// 相机的全部手感参数：目标锚点高度、三条环绕臂、镜头 FOV、碰撞避让。
    /// </summary>
    /// <remarks>
    /// 这里是"相机手感"的唯一调参入口：场景里的 vcam 资产只负责装配，
    /// 具体数值由 <see cref="CameraRigBinding"/> 在启动时写进 Cinemachine。
    /// 门面 <see cref="CameraModule"/> 只持有本资产，不认识 Cinemachine。
    /// </remarks>
    [CreateAssetMenu(menuName = "CCCProject/Camera Settings", fileName = "CameraSettings")]
    public sealed class CameraSettings : ScriptableObject
    {
        /// <summary>一条环绕臂：相对跟随锚点的高度与水平半径（米）。</summary>
        [System.Serializable]
        public struct Arm
        {
            [SerializeField, Tooltip("臂的高度：相机停在锚点上方多少米。")]
            private float _height;

            [SerializeField, Tooltip("臂的水平半径：相机离锚点多远。")]
            private float _radius;

            /// <summary>相机相对锚点的高度（米）。</summary>
            public float Height => _height;

            /// <summary>相机相对锚点的水平距离（米）。</summary>
            public float Radius => _radius;

            /// <summary>构造一条环绕臂。</summary>
            /// <param name="height">高度（米）。</param>
            /// <param name="radius">水平半径（米）。</param>
            public Arm(float height, float radius)
            {
                _height = height;
                _radius = radius;
            }
        }

        [Header("目标锚点（相对角色根节点，米）")]
        [SerializeField, Tooltip("跟随锚点高度：相机围绕它做环绕。")]
        private float _followAnchorHeight = 1.3f;

        [SerializeField, Tooltip("看向锚点高度：镜头中心对准它。")]
        private float _lookAnchorHeight = 1.6f;

        [Header("环绕臂：上 / 中 / 下")]
        [SerializeField, Tooltip("上臂：俯视时相机的位置。")]
        private Arm _topArm = new Arm(2.6f, 3.0f);

        [SerializeField, Tooltip("中臂：初始视角（Y 轴 0.5）停在这里。")]
        private Arm _middleArm = new Arm(1.7f, 3.8f);

        [SerializeField, Tooltip("下臂：仰视时相机的位置。")]
        private Arm _bottomArm = new Arm(0.9f, 3.0f);

        [Header("镜头")]
        [SerializeField, Range(20f, 100f), Tooltip("垂直视角（度）。")]
        private float _fieldOfView = 55f;

        [Header("碰撞避让")]
        [SerializeField, Tooltip("关闭后相机不再躲遮挡物，用来对比避让前后的手感。")]
        private bool _collisionEnabled = true;

        [SerializeField, Tooltip("算作遮挡物的层名（只放环境，不要放角色，否则相机被自己的胶囊顶飞）。")]
        private string[] _collisionLayerNames = { "Environment" };

        [SerializeField, Tooltip("被挡住时怎么解：拉近（朝向跟手）还是贴墙滑（保距离但朝向跟不上）。默认拉近。")]
        private CameraCollisionStrategy _collisionStrategy = CameraCollisionStrategy.PullIn;

        [SerializeField, Tooltip("相机球半径（米）：贴墙时留出的余量。太大会在贴墙时把相机横向顶开，太小会蹭近裁剪面。")]
        private float _cameraRadius = 0.2f;

        [SerializeField, Tooltip("相机离目标最近距离（米）。注意这同时是 Collider 的盲区：射线从该距离起算，" +
                                 "玩家贴墙距离小于它时不会触发避让（会直接穿墙）。角色胶囊半径约 0.35，取 0.25。")]
        private float _minimumDistanceFromTarget = 0.25f;

        [SerializeField, Min(0f), Tooltip("被挡住时拉近的阻尼（秒）：越大越软，0 为硬贴。")]
        private float _occlusionDamping = 0.08f;

        [SerializeField, Min(0f), Tooltip("遮挡消失后弹回的阻尼（秒）：越大回位越肉。贴墙离墙时会走这条曲线。")]
        private float _returnDamping = 0.08f;

        [SerializeField, Min(0f), Tooltip("遮挡迟滞（秒）：短于它的新遮挡直接忽略。压住墙角两面墙来回切换造成的抖动。")]
        private float _minimumOcclusionTime = 0.1f;

        [SerializeField, Min(0f), Tooltip("遮挡解除后的保持时间（秒）：最近点至少保持这么久，防止贴墙边缘一进一出地弹。")]
        private float _occlusionSmoothing = 0.1f;
        [SerializeField, Tooltip("遮挡求解实现：自研球扫求解器（推荐，浅角度不擦墙）还是 Cinemachine 自带的射线拉近（对照项）。")]
        private bool _useOwnSolver = true;

        [SerializeField, Min(1), Tooltip("自研求解器每帧最多分离次数：凹角里推开 A 面又撞上 B 面时需要多轮。")]
        private int _solverIterations = 3;
        [SerializeField, Min(0f), Tooltip("拉近最大速度（米/秒）：0 表示不限速（撞进遮挡物时立即贴住，避免穿墙）。")]
        private float _pullInSpeed = 20f;

        [SerializeField, Min(0f), Tooltip("回弹最大速度（米/秒）：遮挡解除后臂长恢复的上限。太大会抽一下，0 表示不限速。")]
        private float _pullOutSpeed = 4f;

        /// <summary>跟随锚点高度（米）。</summary>
        public float FollowAnchorHeight => _followAnchorHeight;

        /// <summary>看向锚点高度（米）。</summary>
        public float LookAnchorHeight => _lookAnchorHeight;

        /// <summary>上臂。</summary>
        public Arm TopArm => _topArm;

        /// <summary>中臂。</summary>
        public Arm MiddleArm => _middleArm;

        /// <summary>下臂。</summary>
        public Arm BottomArm => _bottomArm;

        /// <summary>垂直视角（度）。</summary>
        public float FieldOfView => _fieldOfView;

        /// <summary>是否启用碰撞避让。</summary>
        public bool CollisionEnabled => _collisionEnabled;

        /// <summary>被遮挡时的解决方式。</summary>
        public CameraCollisionStrategy CollisionStrategy => _collisionStrategy;

        /// <summary>相机球半径（米）。</summary>
        public float CameraRadius => _cameraRadius;

        /// <summary>相机离目标最近距离（米）。</summary>
        public float MinimumDistanceFromTarget => _minimumDistanceFromTarget;

        /// <summary>被挡住时的拉近阻尼（秒）。</summary>
        public float OcclusionDamping => _occlusionDamping;

        /// <summary>遮挡消失后的回弹阻尼（秒）。</summary>
        public float ReturnDamping => _returnDamping;

        /// <summary>是否使用自研球扫求解器（false 时用 Cinemachine 自带的射线拉近）。</summary>
        public bool UseOwnSolver => _useOwnSolver;

        /// <summary>自研求解器每帧最多分离次数。</summary>
        public int SolverIterations => _solverIterations;
        /// <summary>拉近最大速度（米/秒）。</summary>
        public float PullInSpeed => _pullInSpeed;

        /// <summary>回弹最大速度（米/秒），0 表示不限速。</summary>
        public float PullOutSpeed => _pullOutSpeed;

        /// <summary>遮挡迟滞（秒）：短于它的新遮挡被忽略。</summary>
        public float MinimumOcclusionTime => _minimumOcclusionTime;

        /// <summary>遮挡解除后的保持时间（秒）。</summary>
        public float OcclusionSmoothing => _occlusionSmoothing;

        /// <summary>
        /// 遮挡物层掩码：由层名解析成 Unity 的 LayerMask。
        /// 层名写错或层没建时返回 0（相机不会避让），启动日志会提示。
        /// </summary>
        public int CollisionMask
        {
            get
            {
                if (_collisionLayerNames == null || _collisionLayerNames.Length == 0)
                {
                    return 0;
                }

                return LayerMask.GetMask(_collisionLayerNames);
            }
        }
    }
}

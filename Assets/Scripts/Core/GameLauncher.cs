using CCCProject.Animations;
using CCCProject.CameraSystem;
using CCCProject.Character;
using CCCProject.Inputs;
using UnityEngine;

namespace CCCProject.Core
{
    /// <summary>
    /// 组合根：启动时按固定顺序构造并注册全部模块。
    /// </summary>
    /// <remarks>
    /// 职责只有两件：
    /// <list type="bullet">
    /// <item><description>启动时按固定顺序构造并注册全部模块（注册代码就写在 <see cref="Start"/> 里，一段线性流程）；</description></item>
    /// <item><description>宿主销毁时让模块中心清空模块（<see cref="ModuleCenter.Clear"/>）。</description></item>
    /// </list>
    /// **每帧驱动不在这里**：模块中心是 MonoBehaviour 单例，帧泵由它自己的 Update 负责。
    /// 这里**不保存模块实例**：模块构造完就只存在于 <see cref="ModuleCenter"/>。
    /// 本类只负责"接线"——把场景里的对象递给模块的构造函数；
    /// 之所以要有人接线，是因为模块是普通 C# 类，自己找不到场景对象。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GameLauncher : MonoBehaviour
    {
        [Header("相机")]
        [SerializeField, Tooltip("相机表现层适配器（挂在 FreeLook 虚拟相机上）。")]
        private CameraRigBinding _cameraRig;

        [SerializeField, Tooltip("相机手感参数资产（臂长 / FOV / 碰撞避让）。")]
        private CameraSettings _cameraSettings;

        [Header("出生点")]
        [SerializeField, Tooltip("角色出生点（地编脚本）；启动时在这里生成角色。")]
        private SwapPoint _spawnPoint;

        [Header("角色")]
        [SerializeField, Tooltip("角色手感参数资产。")]
        private CharacterMovementConfig _movementConfig;

        [Header("输入")]
        [SerializeField, Tooltip("水平移动轴名。")]
        private string _horizontalAxis = "Horizontal";

        [SerializeField, Tooltip("垂直移动轴名。")]
        private string _verticalAxis = "Vertical";

        [SerializeField, Tooltip("走 / 跑修饰键（旧 Input Manager 的 Fire3 = 左 Shift）。")]
        private string _walkButton = "Fire3";

        [SerializeField, Tooltip("跳跃键（旧 Input Manager 的 Jump 轴 = 空格）。")]
        private string _jumpButton = "Jump";

        [SerializeField, Tooltip("翻滚键（旧 Input Manager 的 Fire2 轴 = 右鼠标）。")]
        private string _dodgeButton = "Fire2";

        [SerializeField, Range(0f, 0.5f), Tooltip("摇杆死区。")]
        private float _deadZone = 0.15f;

        [Header("动画")]
        [SerializeField, Tooltip("动画表现层参数（步频基准 / 播放倍率）。")]
        private AnimationSettings _animationSettings;

        [Header("角色淡出")]
        [SerializeField, Tooltip("近距离淡出参数（相机贴到角色身上时隐藏角色）。")]
        private CharacterFadeSettings _characterFadeSettings;

        [Header("翻滚")]
        [SerializeField, Tooltip("翻滚手感参数（位移曲线 / 总距离 / 方向策略）。")]
        private DodgeSettings _dodgeSettings;

        [Header("其它")]
        [SerializeField, Tooltip("启动时是否锁定鼠标（鼠标居中）。")]
        private bool _lockPointerOnLaunch = true;

        /// <summary>模块是否已注册完成。</summary>
        public bool IsRunning { get; private set; }

        /// <summary>模块注册完成时触发。</summary>
        public event System.Action LaunchCompleted;

        private ModuleCenter _center;

        private void Awake()
        {
            // 模块中心是懒加载单例：第一次访问即自动创建，场景里不用手放这个组件。
            _center = ModuleCenter.Instance;
        }

        private void Start()
        {
            if (_center == null)
            {
                Debug.LogError("[GameLauncher] 模块中心不可用，模块注册中止。");
                return;
            }

            // 0) 角色：先从出生点生成出来。
            //    相机模块的初始目标就是角色身上的相机锚点，所以必须先生成再装配。
            GameObject player = SpawnPlayer();
            if (player == null)
            {
                Debug.LogError("[GameLauncher] 角色生成失败，模块注册中止。");
                return;
            }

            var playerController = player.GetComponent<CharacterController>();
            var cameraTarget = player.GetComponent<CameraTarget>();

            // 模块注册就是启动流程本身：一段线性的、看得见的顺序。
            // 这段顺序同时也是每帧更新顺序 —— 输入必须排在角色前面。
            // 依赖用构造函数注入，不在运行时到处查找。

            // 1) 相机：角色要用它做"相机相对移动"，所以先于角色启动。
            //    组合根只认门面 + 引擎适配器接口，不出现任何 Cinemachine 类型；
            //    虚拟相机 / Brain / 输入适配器的接线都在 CameraRigBinding 里面。
            var cameraModule = new CameraModule(_cameraRig, _cameraSettings, cameraTarget);
            _center.Register(cameraModule);

            // 2) 输入：语义输入层，读旧 Input Manager。
            var inputModule = new InputModule(
                _horizontalAxis,
                _verticalAxis,
                _walkButton,
                _jumpButton,
                _dodgeButton,
                _deadZone);
            _center.Register(inputModule);

            // 3) 指针捕获：鼠标居中，与相机实现无关。
            _center.Register(new PointerLockModule(_lockPointerOnLaunch));

            // 4) 角色：依赖上面的输入与相机。
            var playerModule = new PlayerModule(
                playerController,
                _movementConfig,
                inputModule,
                cameraModule);
            var actionAnimator = player.GetComponentInChildren<Animator>(true);

            // 动作模块注册在运动模块之前：本帧的动作约束与脚本位移要先写进运动层，
            // 运动层才能在同一 tick 里把它们算进同一次位移。
            // 攻击模块要作为"可被翻滚取消的动作源"交给翻滚，
            // 所以先构造攻击模块，再构造翻滚模块；注册的先后仍按下面的顺序。
            var attackRelay = actionAnimator != null ? actionAnimator.GetComponent<AttackRelay>() : null;
            var attackModule = attackRelay != null
                ? new AttackModule(inputModule, playerModule, actionAnimator, attackRelay, player.transform)
                : null;

            var dodgeRelay = actionAnimator != null ? actionAnimator.GetComponent<DodgeRelay>() : null;
            var dodgeModule = dodgeRelay != null
                ? new DodgeModule(
                    actionAnimator, dodgeRelay, _dodgeSettings, playerModule, inputModule, attackModule)
                : null;

            // 注册顺序 = 每帧更新顺序：翻滚先判输入，攻击再判输入，
            // 两者都早于运动模块，这样本帧的动作约束与脚本位移才能算进同一次位移。
            if (dodgeModule != null)
                _center.Register(dodgeModule);

            if (attackModule != null)
                _center.Register(attackModule);

            _center.Register(playerModule);

            // 5) 动画表现层：读角色运动事实驱动 Animator。
            //    Animator 挂在模型子节点上（FBX 自带，Controller 在预制体里指定），
            //    角色是运行时生成的，所以这里从生成结果里取。
            var playerAnimator = player.GetComponentInChildren<Animator>(true);
            if (playerAnimator == null)
            {
                Debug.LogError("[GameLauncher] 角色身上找不到 Animator，动画不会播放。");
            }

            var animationModule = new AnimationModule(playerAnimator, playerModule, _animationSettings);
            playerModule.BindLandingCompletion(animationModule.IsLandingFinished);
            _center.Register(animationModule);

            // 6) 角色淡出：相机贴到角色身上时把角色隐掉，避免近裁剪面切开模型。
            //    只读相机位置与看向锚点，属于角色表现层，不参与相机求解。
            var playerRenderers = player.GetComponentsInChildren<Renderer>(true);
            _center.Register(new CharacterFadeModule(
                cameraModule,
                cameraTarget != null ? cameraTarget.LookAnchor : null,
                playerRenderers,
                _characterFadeSettings));

            IsRunning = true;
            Debug.Log($"[GameLauncher] 模块注册完成，共 {_center.Count} 个模块。");
            LaunchCompleted?.Invoke();
        }

        /// <summary>在出生点生成角色；出生点没配或生成失败时返回 null。</summary>
        private GameObject SpawnPlayer()
        {
            if (_spawnPoint == null)
            {
                Debug.LogError("[GameLauncher] 没有指定出生点（SwapPoint），角色无法生成。");
                return null;
            }

            return _spawnPoint.Spawn();
        }

        private void OnDestroy()
        {
            // 宿主没了，模块也该收尾：逆序 OnShutdown。
            if (_center != null)
                _center.Clear();

            IsRunning = false;
        }
    }
}

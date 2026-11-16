using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 挂在 Animator 同物体：转发动画事件、提供命中检测参数与连段缓冲时长。
    /// </summary>
    /// <remarks>
    /// 引擎适配器。Animation Event 只能调用 Animator 所在对象上的组件方法，
    /// 而动作逻辑是普通 C# 类，因此由本组件把事件转交给 <see cref="AttackModule"/>。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AttackRelay : MonoBehaviour
    {
        [Header("命中检测")]
        [SerializeField, Tooltip("相对角色根节点的命中球中心。")]
        private Vector3 _hitOffset = new Vector3(0f, 1.1f, 0.8f);

        [SerializeField, Min(0.01f), Tooltip("命中球半径（米）。")]
        private float _hitRadius = 0.45f;

        [Header("连段")]
        [SerializeField, Min(0f), Tooltip("攻击请求在缓冲槽中的存活时间（秒）：太早按下会被丢弃，太长会让误触也接上。")]
        private float _comboBufferLifetime = 0.2f;

        /// <summary>由组合根绑定的动作模块。</summary>
        public AttackModule Module { get; set; }

        /// <summary>相对角色根节点的命中球中心。</summary>
        public Vector3 HitOffset => _hitOffset;

        /// <summary>命中球半径（米）。</summary>
        public float HitRadius => _hitRadius;

        /// <summary>攻击请求在缓冲槽中的存活时间（秒）。</summary>
        public float ComboBufferLifetime => _comboBufferLifetime;

        /// <summary>攻击动画的命中采样事件。</summary>
        public void AttackStrike()
        {
            Module?.Strike();
        }

        /// <summary>攻击动画的收招事件。</summary>
        public void AttackRecovery()
        {
            Module?.Recover();
        }

        /// <summary>攻击动画的"可翻滚取消"事件：打开取消窗口。</summary>
        public void AttackDodgeWindow()
        {
            Module?.OpenDodgeWindow();
        }

        private void OnDisable()
        {
            Module?.Cancel();
        }

        private void OnGUI()
        {
            if (Module == null)
            {
                return;
            }

            GUI.Box(new Rect(16, 16, 420, 116), "M1-02 | LMB: Jab -> Cross | R: Reset scene");
            GUI.Label(new Rect(28, 44, 400, 24),
                "Phase: " + Module.Phase + "  Step: " + (Module.Step == 0 ? "Jab" : "Cross")
                + "  Buffered: " + Module.HasBufferedInput);
            GUI.Label(new Rect(28, 70, 400, 24),
                "Attacks: " + Module.AttackCount + "  Hits: " + Module.HitCount
                + "  Chains: " + Module.ChainCount);
            GUI.Label(new Rect(28, 96, 400, 24), "blue target flashes orange on hit");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
        }
    }
}

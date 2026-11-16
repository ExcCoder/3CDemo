using UnityEngine;

namespace CCCProject.Animations
{
    /// <summary>
    /// 动画表现层参数：全局播放倍率。
    /// </summary>
    /// <remarks>
    /// **步频对齐不在这里做**：动画 clip 的固有迈步速度与角色速度的匹配，靠 Controller 里
    /// 混合树子动作的 <c>timeScale</c> 完成（标定值写在 <c>Player.controller</c> 资产里）。
    /// 本资产只留一个全局倍率，给调试或整体调速用。
    /// <para>
    /// 注意：改了 <c>CharacterMovementConfig</c> 的走 / 跑速度后，要同步修改
    /// <c>Player.controller</c> 混合树的两个阈值与两个子动作的 <c>timeScale</c>，
    /// 否则脚步会与地面打滑。
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "AnimationSettings", menuName = "CCCProject/Animation Settings")]
    public sealed class AnimationSettings : ScriptableObject
    {
        [Header("播放倍率")]
        [Tooltip("全局播放倍率。1 = 原速。用于调试或整体调速，步频对齐不靠它。")]
        [SerializeField] private float _playbackScale = 1f;

        /// <summary>全局播放倍率。</summary>
        public float PlaybackScale => _playbackScale;
    }
}

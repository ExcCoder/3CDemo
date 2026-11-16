using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 翻滚手感参数：位移曲线、总距离与转向策略。
    /// </summary>
    /// <remarks>
    /// 曲线来自动画自带的根位移，烘焙后由代码驱动角色位移：
    /// 动画负责姿态，代码负责位移，两者共用同一条曲线，因此表现与位移天然同步。
    /// </remarks>
    [CreateAssetMenu(fileName = "DodgeSettings", menuName = "CCCProject/Dodge Settings")]
    public sealed class DodgeSettings : ScriptableObject
    {
        [SerializeField, Tooltip("归一化位移曲线：横轴是动画进度 0~1，纵轴是已前进距离占总距离的比例。")]
        private AnimationCurve _distanceProfile = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [SerializeField, Min(0f), Tooltip("翻滚总位移（米）：由动画根位移烘焙得到，可按需缩放。")]
        private float _totalDistance = 4.82f;

        [SerializeField, Min(0.05f), Tooltip("翻滚动作时长（秒）：取自动画长度，决定位移曲线的时间基准。")]
        private float _duration = 1.47f;

        [SerializeField, Range(0f, 1f), Tooltip("转向速度上限倍率：翻滚时角色转向所选方向的转速，1 表示与平时一致。")]
        private float _turnSpeedScale = 1f;

        [SerializeField, Tooltip("是否按输入方向翻滚：关闭时固定向角色当前朝向前翻。")]
        private bool _useInputDirection = true;

        /// <summary>归一化位移曲线。</summary>
        public AnimationCurve DistanceProfile => _distanceProfile;

        /// <summary>翻滚总位移（米）。</summary>
        public float TotalDistance => _totalDistance;

        /// <summary>翻滚动作时长（秒）。</summary>
        public float Duration => _duration;

        /// <summary>转向速度上限倍率。</summary>
        public float TurnSpeedScale => _turnSpeedScale;

        /// <summary>是否按输入方向翻滚。</summary>
        public bool UseInputDirection => _useInputDirection;

        /// <summary>写入烘焙结果（由编辑器装配脚本调用）。</summary>
        /// <param name="profile">归一化位移曲线。</param>
        /// <param name="totalDistance">总位移（米）。</param>
        /// <param name="duration">动作时长（秒）。</param>
        public void SetProfile(AnimationCurve profile, float totalDistance, float duration)
        {
            _distanceProfile = profile;
            _totalDistance = totalDistance;
            _duration = duration;
        }
    }
}

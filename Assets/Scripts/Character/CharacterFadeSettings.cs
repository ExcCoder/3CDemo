using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 角色近距离淡出的距离阈值：相机离角色多近时开始淡出、多近时完全不可见。
    /// </summary>
    /// <remarks>
    /// 只放"什么时候淡"，不放"怎么淡"——怎么淡是 <c>CCCProject/CharacterFade</c> 着色器的事。
    /// 相机贴到角色身上时靠它把角色隐掉，避免近裁剪面切进模型内部。
    /// </remarks>
    [CreateAssetMenu(menuName = "CCCProject/Character Fade Settings", fileName = "CharacterFadeSettings")]
    public sealed class CharacterFadeSettings : ScriptableObject
    {
        [SerializeField, Min(0f), Tooltip("相机与该锚点的距离小于等于此值（米）时，角色完全不可见。")]
        private float _hideDistance = 0.45f;

        [SerializeField, Min(0f), Tooltip("相机与该锚点的距离大于等于此值（米）时，角色完全可见。")]
        private float _showDistance = 1.2f;

        /// <summary>完全不可见的距离（米）。</summary>
        public float HideDistance => _hideDistance;

        /// <summary>完全可见的距离（米）；保证大于隐藏距离，避免除零与反向插值。</summary>
        public float ShowDistance => Mathf.Max(_showDistance, _hideDistance + 0.01f);
    }
}

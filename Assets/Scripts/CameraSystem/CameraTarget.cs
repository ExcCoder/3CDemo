using UnityEngine;

namespace CCCProject.CameraSystem
{
    /// <summary>
    /// 挂在角色上的相机目标：向相机提供跟随锚点与看向锚点。
    /// 相机只依赖本组件，不直接依赖角色控制、动画或模型结构，保证角色与相机解耦。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraTarget : MonoBehaviour
    {
        [Header("锚点高度（相对角色根节点）")]
        [SerializeField, Tooltip("跟随锚点高度：相机围绕该点做环绕。")]
        private float _followHeight = 1.3f;

        [SerializeField, Tooltip("看向锚点高度：镜头中心对准该点。")]
        private float _lookHeight = 1.6f;

        private Transform _followAnchor;
        private Transform _lookAnchor;

        /// <summary>跟随锚点，供相机 Follow 使用。</summary>
        public Transform FollowAnchor => _followAnchor;

        /// <summary>看向锚点，供相机 LookAt 使用。</summary>
        public Transform LookAnchor => _lookAnchor;

        private void Awake()
        {
            // 锚点运行时生成：换模型、调整身高不需要改相机侧的任何引用。
            _followAnchor = CreateAnchor("_CameraFollowAnchor", _followHeight);
            _lookAnchor = CreateAnchor("_CameraLookAnchor", _lookHeight);
        }

        /// <summary>
        /// 按相机参数资产设置两个锚点的高度（米，相对角色根节点）。
        /// 相机绑定目标时会调用；锚点已生成则直接移动，未生成则留到 Awake 生成时生效。
        /// </summary>
        /// <param name="followHeight">跟随锚点高度。</param>
        /// <param name="lookHeight">看向锚点高度。</param>
        public void ConfigureAnchors(float followHeight, float lookHeight)
        {
            _followHeight = followHeight;
            _lookHeight = lookHeight;

            if (_followAnchor != null)
                _followAnchor.localPosition = new Vector3(0f, _followHeight, 0f);

            if (_lookAnchor != null)
                _lookAnchor.localPosition = new Vector3(0f, _lookHeight, 0f);
        }

        private void OnDestroy()
        {
            if (_followAnchor != null)
                Destroy(_followAnchor.gameObject);

            if (_lookAnchor != null)
                Destroy(_lookAnchor.gameObject);
        }

        private Transform CreateAnchor(string anchorName, float localHeight)
        {
            var anchor = new GameObject(anchorName);
            anchor.transform.SetParent(transform, false);
            anchor.transform.localPosition = new Vector3(0f, localHeight, 0f);
            return anchor.transform;
        }
    }
}

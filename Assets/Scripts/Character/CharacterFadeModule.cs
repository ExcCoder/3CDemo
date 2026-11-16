using CCCProject.CameraSystem;
using CCCProject.Core;
using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 角色近距离淡出：相机贴到角色身上时，按距离把角色抖动隐掉，避免近裁剪面切开模型。
    /// </summary>
    /// <remarks>
    /// 纯 C# 类：生命周期由 <see cref="ModuleCenter"/> 管理，每帧由 <see cref="ITickable"/> 驱动。
    /// <para>
    /// 只读两个事实：输出相机的位置、角色看向锚点的位置；只写一个属性：材质上的 <c>_Fade</c>。
    /// 用 <see cref="MaterialPropertyBlock"/> 写入，不产生材质实例、不打断 batching、不改资产。
    /// 淡出方式（屏幕空间 Bayer 抖动）由 <c>CCCProject/CharacterFade</c> 着色器负责。
    /// </para>
    /// </remarks>
    public sealed class CharacterFadeModule : IGameModule, ITickable
    {
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        private readonly CameraModule _camera;
        private readonly Transform _anchor;
        private readonly Renderer[] _renderers;
        private readonly CharacterFadeSettings _settings;

        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private float _lastFade = float.NaN;

        /// <summary>当前生效的淡出值：1 为完全可见，0 为完全不可见。</summary>
        public float CurrentFade { get; private set; } = 1f;

        /// <summary>强制淡出值（0~1）；小于 0 表示按距离自动计算。调试与截图验证用。</summary>
        public float FadeOverride { get; set; } = -1f;

        /// <summary>构造淡出模块。</summary>
        /// <param name="camera">相机模块，提供输出相机位置。</param>
        /// <param name="anchor">角色身上的看向锚点，作为"角色位置"的度量点。</param>
        /// <param name="renderers">需要淡出的渲染器（角色模型下的全部 Renderer）。</param>
        /// <param name="settings">距离阈值资产。</param>
        public CharacterFadeModule(
            CameraModule camera,
            Transform anchor,
            Renderer[] renderers,
            CharacterFadeSettings settings)
        {
            _camera = camera;
            _anchor = anchor;
            _renderers = renderers;
            _settings = settings;
        }

        /// <summary>启动：校验依赖，并把角色设为完全可见。</summary>
        public void OnLaunch()
        {
            if (_settings == null)
            {
                Debug.LogError("[CharacterFadeModule] 未指定 CharacterFadeSettings，淡出不会生效。");
                return;
            }

            if (_renderers == null || _renderers.Length == 0)
            {
                Debug.LogWarning("[CharacterFadeModule] 没有可淡出的 Renderer，淡出不会生效。");
                return;
            }

            if (_camera == null || _anchor == null)
            {
                Debug.LogError("[CharacterFadeModule] 缺少相机或锚点依赖，淡出不会生效。");
                return;
            }

            Apply(1f);
        }

        /// <summary>关闭：把角色恢复为完全可见，避免下次启动残留半透明。</summary>
        public void OnShutdown()
        {
            Apply(1f);
        }

        /// <summary>每帧：按相机与角色的距离求淡出值并写入材质属性块。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            if (_settings == null || _renderers == null || _renderers.Length == 0)
            {
                return;
            }

            if (_camera == null || _anchor == null)
            {
                return;
            }

            float fade = FadeOverride >= 0f ? Mathf.Clamp01(FadeOverride) : EvaluateDistanceFade();
            Apply(fade);
        }

        /// <summary>距离 → 淡出值：隐藏距离内为 0，显示距离外为 1，中间线性过渡。</summary>
        private float EvaluateDistanceFade()
        {
            Transform view = _camera.ViewTransform;
            if (view == null)
            {
                return 1f;
            }

            float distance = Vector3.Distance(view.position, _anchor.position);
            return Mathf.InverseLerp(_settings.HideDistance, _settings.ShowDistance, distance);
        }

        /// <summary>把淡出值写进所有渲染器；值没变时跳过，避免每帧重复提交。</summary>
        private void Apply(float fade)
        {
            CurrentFade = fade;

            if (!float.IsNaN(_lastFade) && Mathf.Abs(fade - _lastFade) < 0.001f)
            {
                return;
            }

            _lastFade = fade;
            _block.SetFloat(FadeId, fade);

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(_block);
                }
            }
        }
    }
}

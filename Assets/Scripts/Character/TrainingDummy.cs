using UnityEngine;
namespace CCCProject.Character
{
    /// <summary>固定训练假人：计数并短暂闪色，不实现 AI 或生命值。</summary>
    public sealed class TrainingDummy : MonoBehaviour
    {
        [SerializeField] private Color _idleColor = new Color(0.15f, 0.55f, 0.9f);
        [SerializeField] private Color _hitColor = new Color(1f, 0.4f, 0.05f);
        [SerializeField, Min(0f)] private float _flashDuration = 0.2f;
        private Renderer _renderer;
        private MaterialPropertyBlock _properties;
        private float _flashUntil;
        /// <summary>累计命中次数。</summary>
        public int Hits { get; private set; }
        private void Awake() { _renderer = GetComponentInChildren<Renderer>(); _properties = new MaterialPropertyBlock(); }
        /// <summary>收到一次有效命中。</summary>
        public void ReceiveHit() { Hits++; _flashUntil = Time.time + _flashDuration; }
        private void Update()
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_properties);
            _properties.SetColor("_Color", Time.time < _flashUntil ? _hitColor : _idleColor);
            _renderer.SetPropertyBlock(_properties);
        }
    }
}

using Cinemachine;
using UnityEngine;

namespace CCCProject.CameraSystem
{
    /// <summary>
    /// 自研的遮挡求解：球体扫掠 + 多轮收敛 + 距离限速，替换 Cinemachine 2.8.9 的射线拉近。
    /// </summary>
    /// <remarks>
    /// 为什么不用 <c>CinemachineCollider</c>：它在拉近路径上用**射线**判定，
    /// 视线几乎平行于墙面时射线只能给出"沿视线后退"，没有法向分量，
    /// 相机中心会沉进墙面几厘米（近裁剪面 0.1 大于该深度，于是能看穿墙）。
    /// 本扩展改成从看向锚点发**球体扫掠**，用球的半径保证任意角度都有离墙余量；
    /// 每帧迭代若干次，处理凹角里"推开之后又撞上另一面"的多点接触；
    /// 最后对"相机到锚点的距离"做限速，避免遮挡解除时整段瞬移。
    /// 挂在三条 rig 上（与 Collider 同一阶段 Body），由 <see cref="CameraRigBinding"/> 注入参数。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CameraSphereSolver : CinemachineExtension
    {
        [SerializeField, Min(0.01f), Tooltip("代理球半径（米）：贴墙余量，略大于近裁剪面。")]
        private float _radius = 0.2f;

        [SerializeField, Min(0f), Tooltip("相机离看向锚点的最近距离（米）。")]
        private float _minimumDistance = 0.25f;

        [SerializeField, Min(1), Tooltip("每帧最多的分离次数：凹角里先撞 A 面、推开又撞 B 面时需要多轮。")]
        private int _iterations = 3;

        [SerializeField, Min(0f), Tooltip("回弹最大速度（米/秒）：遮挡解除后臂长恢复的上限，0 表示不限速。")]
        private float _pullOutSpeed = 4f;

        [SerializeField, Min(0f), Tooltip("拉近最大速度（米/秒），0 表示立即贴住（推荐，避免瞬移进墙）。")]
        private float _pullInSpeed = 0f;

        private LayerMask _collideAgainst = 1 << 8;
        private float _distance;
        private bool _initialized;

        /// <summary>配置参数。</summary>
        /// <param name="radius">代理球半径（米）。</param>
        /// <param name="minimumDistance">最近距离（米）。</param>
        /// <param name="iterations">每帧最多分离次数。</param>
        /// <param name="pullOutSpeed">回弹限速（米/秒），0 为不限。</param>
        /// <param name="pullInSpeed">拉近限速（米/秒），0 为不限。</param>
        /// <param name="mask">遮挡物层掩码。</param>
        public void Configure(
            float radius,
            float minimumDistance,
            int iterations,
            float pullOutSpeed,
            float pullInSpeed,
            int mask)
        {
            _radius = Mathf.Max(0.01f, radius);
            _minimumDistance = Mathf.Max(0f, minimumDistance);
            _iterations = Mathf.Max(1, iterations);
            _pullOutSpeed = Mathf.Max(0f, pullOutSpeed);
            _pullInSpeed = Mathf.Max(0f, pullInSpeed);
            _collideAgainst = mask;
            _initialized = false;
        }

        /// <inheritdoc />
        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Body
                || !state.HasLookAt
                || _collideAgainst.value == 0)
            {
                return;
            }

            Vector3 lookAt = state.ReferenceLookAt;
            Vector3 offset = state.CorrectedPosition - lookAt;
            float desiredDistance = offset.magnitude;
            if (desiredDistance < 1e-4f)
            {
                return;
            }

            Vector3 dir = offset / desiredDistance;
            float solved = SolveDistance(lookAt, dir, desiredDistance);

            float dt = deltaTime > 0f ? deltaTime : Time.deltaTime;
            if (!_initialized || Mathf.Abs(solved - _distance) > 8f)
            {
                _distance = solved;
                _initialized = true;
            }
            else
            {
                float speed = solved < _distance ? _pullInSpeed : _pullOutSpeed;
                _distance = speed > 0f
                    ? Mathf.MoveTowards(_distance, solved, speed * dt)
                    : solved;
            }

            // 拉近永远即时生效，限速只作用于回弹：避免"撞墙瞬间被限速拖进墙里"。
            float finalDistance = Mathf.Min(solved, _distance);
            state.PositionCorrection += dir * (finalDistance - desiredDistance);
        }

        /// <summary>球扫求"相机能待的最远距离"：迭代处理凹角的多点接触。</summary>
        private float SolveDistance(Vector3 lookAt, Vector3 dir, float desired)
        {
            float allowed = desired;
            for (int i = 0; i < _iterations; i++)
            {
                RaycastHit hit;
                if (!Physics.SphereCast(
                        lookAt,
                        _radius,
                        dir,
                        out hit,
                        allowed,
                        _collideAgainst,
                        QueryTriggerInteraction.Ignore))
                {
                    break;
                }

                float next = Mathf.Max(_minimumDistance, hit.distance - 0.005f);
                if (next >= allowed)
                {
                    break;
                }

                allowed = next;
            }

            return Mathf.Max(_minimumDistance, allowed);
        }
    }
}

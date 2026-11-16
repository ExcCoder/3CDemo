using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 攻击状态退出时通知动作模块。
    /// </summary>
    /// <remarks>
    /// 状态退出本身不决定动作是否结束：Jab 退出到 Cross 时整串动作还在继续，
    /// 因此这里只把事实交给 <see cref="AttackModule"/>，由它判断是段间交接还是结束。
    /// </remarks>
    public sealed class AttackStateExit : StateMachineBehaviour
    {
        /// <summary>Animator 状态退出通知。</summary>
        /// <param name="animator">所属 Animator。</param>
        /// <param name="stateInfo">退出的状态信息。</param>
        /// <param name="layerIndex">所在层。</param>
        public override void OnStateExit(
            Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            var relay = animator.GetComponent<AttackRelay>();
            relay?.Module?.OnStateExited();
        }
    }
}

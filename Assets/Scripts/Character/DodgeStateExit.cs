using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>翻滚状态退出时通知动作模块归还控制权。</summary>
    public sealed class DodgeStateExit : StateMachineBehaviour
    {
        /// <summary>Animator 状态退出通知。</summary>
        /// <param name="animator">所属 Animator。</param>
        /// <param name="stateInfo">退出的状态信息。</param>
        /// <param name="layerIndex">所在层。</param>
        public override void OnStateExit(
            Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            var relay = animator.GetComponent<DodgeRelay>();
            relay?.Module?.OnStateExited();
        }
    }
}

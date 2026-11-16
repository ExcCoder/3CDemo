using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 挂在 Animator 同物体：转发动画状态退出事件。
    /// </summary>
    /// <remarks>
    /// 引擎适配器。状态退出回调只能写在挂在 Animator 上的 MonoBehaviour 上，
    /// 因此由本组件把事件转交给 <see cref="DodgeModule"/>。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DodgeRelay : MonoBehaviour
    {
        /// <summary>由组合根绑定的翻滚模块。</summary>
        public DodgeModule Module { get; set; }

        /// <summary>翻滚动画状态退出通知。</summary>
        public void OnDodgeStateExit()
        {
            Module?.OnStateExited();
        }
    }
}

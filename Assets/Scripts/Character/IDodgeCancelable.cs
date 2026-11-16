using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 可被翻滚取消的动作：向翻滚模块暴露"现在能不能打断我"。
    /// </summary>
    /// <remarks>
    /// 翻滚需要一个统一的提问口，而不是直接认识攻击模块：
    /// 这样以后加入受击、吟唱、起身等可打断状态时，
    /// 它们各自实现同一份契约，翻滚的代码不用改。
    /// </remarks>
    public interface IDodgeCancelable
    {
        /// <summary>动作是否正在进行中（还没回到可自由行动的状态）。</summary>
        bool IsActionActive { get; }

        /// <summary>当前是否处于允许翻滚打断的取消窗口。</summary>
        bool DodgeCancelOpen { get; }

        /// <summary>
        /// 接受翻滚打断：放弃当前动作，并把控制权交给翻滚。
        /// </summary>
        /// <remarks>
        /// 调用方（翻滚）会先取得动作锁再调用本方法，
        /// 因此本方法释放自己的锁时不会误开翻滚持有的那份。
        /// </remarks>
        void AcceptDodgeCancel();
    }
}

using CCCProject.Core;
using UnityEngine;

namespace CCCProject.Inputs
{
    /// <summary>
    /// 鼠标指针捕获（鼠标居中）：把光标锁定到屏幕中心并隐藏，避免鼠标移出窗口后视角失控。
    /// 属于输入层的设备状态，不属于相机实现——相机只负责读轴，"要不要捕获鼠标"由玩法状态决定
    /// （进游戏锁、进菜单放、过场保持隐藏但不让视角转）。
    /// </summary>
    /// <remarks>
    /// 纯 C# 类：没有 MonoBehaviour 的 <c>OnDisable</c> / <c>OnApplicationFocus</c> 可用，
    /// 所以焦点变化改成在 <see cref="Tick"/> 里轮询 <see cref="Application.isFocused"/>；
    /// 脚本重编译导致域重载时，用 <see cref="ResetStatics"/> 兜底释放光标。
    /// </remarks>
    public sealed class PointerLockModule : IGameModule, ITickable
    {
        private bool _wantLocked;
        private bool _lastFocused;

        /// <summary>当前是否处于捕获状态（玩家意图）。</summary>
        public bool IsLocked => _wantLocked;

        /// <summary>构造指针捕获模块。</summary>
        /// <param name="lockOnLaunch">启动时是否立即捕获鼠标。</param>
        public PointerLockModule(bool lockOnLaunch = true)
        {
            _wantLocked = lockOnLaunch;
        }

        /// <summary>启动：按配置捕获鼠标。</summary>
        public void OnLaunch()
        {
            _lastFocused = Application.isFocused;
            Apply();
        }

        /// <summary>关闭：释放鼠标，保证退出时编辑器 / 游戏里光标可见。</summary>
        public void OnShutdown()
        {
            SetLocked(false);
        }

        /// <summary>设置鼠标捕获状态。</summary>
        /// <param name="locked">true = 锁定到屏幕中心并隐藏光标；false = 释放光标。</param>
        public void SetLocked(bool locked)
        {
            _wantLocked = locked;
            Apply();
        }

        /// <summary>轮询窗口焦点：切出窗口释放、切回来重新锁定。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            bool focused = Application.isFocused;
            if (focused == _lastFocused)
            {
                return;
            }

            _lastFocused = focused;
            Apply();
        }

        private void Apply()
        {
            // 没拿到焦点时锁定光标没有意义（系统会自动恢复可见），这里统一按"未聚焦 = 释放"处理。
            bool lockNow = _wantLocked && Application.isFocused;
            Cursor.lockState = lockNow ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockNow;
        }

        // 域重载（脚本重编译 / 重新进入 Play）时静态状态被清空，这里补一次释放，
        // 否则光标会一直消失、鼠标也没法点界面。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}

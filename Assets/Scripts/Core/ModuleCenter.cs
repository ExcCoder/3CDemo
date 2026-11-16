using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCCProject.Core
{
    /// <summary>
    /// 模块中心：**模块的注册表 + 生命周期的唯一所有者**，同时是全局唯一的常驻 MonoBehaviour（帧泵）。
    /// </summary>
    /// <remarks>
    /// 职责划分：
    /// <list type="bullet">
    /// <item><description><see cref="Register"/>：登记并立即调用 <c>OnLaunch</c>；</description></item>
    /// <item><description><see cref="Unregister"/> / <see cref="Clear"/>：按**逆序**调用 <c>OnShutdown</c>；</description></item>
    /// <item><description><see cref="Update"/>：每帧按**注册顺序**驱动所有 <see cref="ITickable"/> 模块。</description></item>
    /// </list>
    /// 生命周期之所以放在这里，是因为"模块什么时候开始、什么时候结束"必须有唯一答案；
    /// 分散到各个宿主身上（谁 new 出来谁负责关）迟早会漏。
    /// 它是 <see cref="MonoSingleton{T}"/>，所以"每帧驱动模块"由它自己的 <c>Update</c> 完成——
    /// 组合根只负责注册，不用再管 Tick。
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class ModuleCenter : MonoSingleton<ModuleCenter>
    {
        private List<IGameModule> _order;
        private Dictionary<Type, IGameModule> _modules;

        /// <summary>已注册模块的数量。</summary>
        public int Count
        {
            get
            {
                EnsureRegistry();
                return _modules.Count;
            }
        }

        /// <summary>按注册顺序排列的模块列表（只读）。</summary>
        public IReadOnlyList<IGameModule> Modules
        {
            get
            {
                EnsureRegistry();
                return _order;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureRegistry();
        }

        private void Update()
        {
            // 帧泵：模块的每帧更新按注册顺序在这里驱动，组合根不用管。
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// 注册模块：以它的**实际类型**为键，并立即调用 <see cref="IGameModule.OnLaunch"/>。
        /// </summary>
        /// <param name="module">模块实例（普通 C# 类）。</param>
        public void Register(IGameModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            EnsureRegistry();

            Type type = module.GetType();
            if (_modules.ContainsKey(type))
            {
                Debug.LogWarning($"[ModuleCenter] 模块 {type.Name} 已注册，忽略本次注册。");
                return;
            }

            _modules.Add(type, module);
            _order.Add(module);

            try
            {
                module.OnLaunch();
            }
            catch (Exception exception)
            {
                // 单个模块启动失败不阻断其余模块，完整异常仍打到 Console 方便定位。
                Debug.LogException(exception);
            }
        }

        /// <summary>注销指定模块：调用 <c>OnShutdown</c> 并从注册表移除。</summary>
        /// <param name="module">要注销的模块。</param>
        public void Unregister(IGameModule module)
        {
            if (module == null)
                return;

            EnsureRegistry();

            Type type = module.GetType();
            if (!_modules.TryGetValue(type, out var registered))
                return;

            _modules.Remove(type);
            _order.Remove(registered);

            try
            {
                registered.OnShutdown();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>获取已注册模块；未注册时返回 null 并输出警告。</summary>
        /// <typeparam name="T">模块的具体类型（不是接口类型）。</typeparam>
        public T Get<T>() where T : class
        {
            EnsureRegistry();

            if (_modules.TryGetValue(typeof(T), out var module))
                return module as T;

            Debug.LogWarning($"[ModuleCenter] 未找到模块 {typeof(T).Name}，请确认它已注册。");
            return null;
        }

        /// <summary>尝试获取已注册模块，未注册时返回 false。</summary>
        /// <typeparam name="T">模块的具体类型。</typeparam>
        /// <param name="module">取出的模块实例。</param>
        public bool TryGet<T>(out T module) where T : class
        {
            EnsureRegistry();

            if (_modules.TryGetValue(typeof(T), out var found))
            {
                module = found as T;
                return module != null;
            }

            module = null;
            return false;
        }

        /// <summary>
        /// 按**逆注册顺序**关闭并清空所有模块（宿主销毁时调用）。
        /// 逆序是为了让后启动的模块先停——它可能正依赖先启动的模块。
        /// </summary>
        public void Clear()
        {
            EnsureRegistry();

            for (int i = _order.Count - 1; i >= 0; i--)
            {
                try
                {
                    _order[i].OnShutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            _order.Clear();
            _modules.Clear();
        }

        /// <summary>按注册顺序驱动所有 <see cref="ITickable"/> 模块。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            EnsureRegistry();

            for (int i = 0; i < _order.Count; i++)
            {
                if (_order[i] is ITickable tickable)
                    tickable.Tick(deltaTime);
            }
        }

        /// <summary>
        /// 注册表是实例字段，域重载后会被重置，这里按需重建，避免空引用。
        /// </summary>
        private void EnsureRegistry()
        {
            if (_order == null)
                _order = new List<IGameModule>();

            if (_modules == null)
                _modules = new Dictionary<Type, IGameModule>();
        }
    }
}

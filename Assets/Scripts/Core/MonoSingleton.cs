using UnityEngine;

namespace CCCProject.Core
{
    /// <summary>
    /// 自动懒加载的 MonoBehaviour 单例基类。
    /// </summary>
    /// <remarks>
    /// 用法：派生类继承本类，运行时**第一次访问 <see cref="Instance"/>** 时自动创建宿主对象，
    /// 场景里不需要手放这个组件，也不需要谁来引用它——省掉一整条接线。
    /// <list type="bullet">
    /// <item><description>重复实例：先到的留下，后到的销毁并给出警告；</description></item>
    /// <item><description>跨场景存活：默认 <c>DontDestroyOnLoad</c>，派生类可用 <see cref="PersistAcrossScenes"/> 关掉；</description></item>
    /// <item><description>非运行期不创建：编辑器状态下 <see cref="Instance"/> 返回 null。</description></item>
    /// </list>
    /// </remarks>
    /// <typeparam name="T">具体的单例类型（必须是本类的派生类）。</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T s_instance;

        /// <summary>全局唯一实例；运行期按需创建，非运行期返回 null。</summary>
        public static T Instance
        {
            get
            {
                if (s_instance == null)
                    s_instance = FindExisting();

                if (s_instance == null && Application.isPlaying)
                    s_instance = CreateInstance();

                return s_instance;
            }
        }

        /// <summary>是否跨场景存活（<c>DontDestroyOnLoad</c>）；派生类可覆盖。</summary>
        protected virtual bool PersistAcrossScenes => true;

        protected virtual void Awake()
        {
            if (s_instance == null)
            {
                s_instance = (T)(object)this;
            }
            else if (!ReferenceEquals(s_instance, this))
            {
                Debug.LogWarning($"[{typeof(T).Name}] 检测到重复的单例实例，已忽略后出现的那个。", this);
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (ReferenceEquals(s_instance, this))
                s_instance = null;
        }

        /// <summary>场景里已经放过一个的话，直接用它，不再新建。</summary>
        private static T FindExisting()
        {
            return FindObjectOfType<T>();
        }

        private static T CreateInstance()
        {
            var host = new GameObject(typeof(T).Name);
            var instance = host.AddComponent(typeof(T)) as T;

            if (instance != null && instance.PersistAcrossScenes)
                DontDestroyOnLoad(host);

            return instance;
        }
    }
}

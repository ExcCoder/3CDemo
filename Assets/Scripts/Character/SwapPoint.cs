using UnityEngine;

namespace CCCProject.Character
{
    /// <summary>
    /// 出生点（地编脚本）：挂在场景里的空物体上，标记"角色从哪儿出现"。
    /// </summary>
    /// <remarks>
    /// 地编只需要：在场景里摆一个空物体 → 挂上本组件 → 指定要生成的角色预制体（必要时旋转这个物体）。
    /// 生成逻辑由 <see cref="Spawn"/> 负责，启动流程（组合根）调用它，不需要地编手写生成代码。
    /// 一个场景可以放多个出生点（换人 / 传送时用），启动时用哪个由组合根指定。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SwapPoint : MonoBehaviour
    {
        [SerializeField, Tooltip("在此点生成的角色预制体。")]
        private GameObject _characterPrefab;

        [SerializeField, Tooltip("生成时朝向相机水平前方（关闭则沿用出生点自身的旋转）。")]
        private bool _faceCamera;

        /// <summary>本出生点要生成的角色预制体。</summary>
        public GameObject CharacterPrefab => _characterPrefab;

        /// <summary>
        /// 在出生点生成角色。
        /// </summary>
        /// <returns>生成出来的角色实例；预制体没配时返回 null（并输出错误）。</returns>
        public GameObject Spawn()
        {
            if (_characterPrefab == null)
            {
                Debug.LogError($"[SwapPoint] 出生点 {name} 没有指定角色预制体。", this);
                return null;
            }

            Quaternion rotation = _faceCamera ? GetCameraRotation() : transform.rotation;

            GameObject instance = Instantiate(_characterPrefab, transform.position, rotation);
            instance.name = _characterPrefab.name;

            return instance;
        }

        /// <summary>相机水平前方的朝向；相机不可用时退回出生点自身的旋转。</summary>
        private Quaternion GetCameraRotation()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return transform.rotation;

            Vector3 forward = mainCamera.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                return transform.rotation;

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}

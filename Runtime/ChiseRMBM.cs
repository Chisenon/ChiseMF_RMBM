using System;
using UnityEngine;
using VRC.SDKBase;

namespace chisemf.rmbm.runtime
{
    /// <summary>
    /// マスクテクスチャを使用してメッシュの一部を削除するコンポーネント
    /// EmptyObjectにアタッチして、アバター内の任意のメッシュオブジェクトを選択可能
    /// </summary>
    [AddComponentMenu("ChiseNote/Chise Remove Mesh By Mask")]
    [DisallowMultipleComponent]
    public class ChiseRMBM : MonoBehaviour, IEditorOnly
    {
        [Header("Target Mesh Object")]
        [SerializeField]
        [Tooltip("Object with SkinnedMeshRenderer or MeshRenderer to be processed")]
        public GameObject targetMeshObject;

        [Header("Material Slot Settings")]
        [SerializeField]
        public MaterialSlot[] materials = Array.Empty<MaterialSlot>();

        [Serializable]
        public struct MaterialSlot : IEquatable<MaterialSlot>
        {
            [SerializeField] 
            [Tooltip("Enable processing for this material slot")]
            public bool enabled;

            [SerializeField] 
            [Tooltip("Mask texture specifying the area to delete")]
            public Texture2D mask;

            [SerializeField] 
            [Tooltip("Delete mode")]
            public RemoveMode mode;

            public bool Equals(MaterialSlot other) =>
                enabled == other.enabled && Equals(mask, other.mask) && mode == other.mode;

            public override bool Equals(object obj) => obj is MaterialSlot other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(enabled, mask, (int)mode);
            public static bool operator ==(MaterialSlot left, MaterialSlot right) => left.Equals(right);
            public static bool operator !=(MaterialSlot left, MaterialSlot right) => !left.Equals(right);
        }

        /// <summary>
        /// メッシュの削除モード
        /// </summary>
        public enum RemoveMode
        {
            [Tooltip("Delete mesh in the black area of the mask")]
            RemoveBlack,
            [Tooltip("Delete mesh in the white area of the mask")]
            RemoveWhite,
        }

        [Header("Settings")]
        [Tooltip("Output the processing result to the console")]
        public bool logToConsole = true;

        private void Reset()
        {
            // リセット時は空の配列で初期化
            // エディタでターゲットオブジェクトが選択された時に自動更新される
            materials = Array.Empty<MaterialSlot>();
        }

        /// <summary>
        /// ターゲットオブジェクトから利用可能なレンダラーを取得
        /// </summary>
        /// <returns>SkinnedMeshRendererまたはMeshRenderer</returns>
        public Renderer GetTargetRenderer()
        {
            if (targetMeshObject == null) return null;
            
            var skinnedRenderer = targetMeshObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer != null) return skinnedRenderer;
            
            var meshRenderer = targetMeshObject.GetComponent<MeshRenderer>();
            return meshRenderer;
        }

        /// <summary>
        /// ターゲットオブジェクトのマテリアル配列を取得
        /// </summary>
        /// <returns>マテリアル配列</returns>
        public Material[] GetTargetMaterials()
        {
            var renderer = GetTargetRenderer();
            return renderer?.sharedMaterials;
        }

        /// <summary>
        /// マテリアルスロット配列を自動更新
        /// </summary>
        public void UpdateMaterialSlots()
        {
            var targetMaterials = GetTargetMaterials();
            if (targetMaterials == null)
            {
                Debug.Log("[chisemf.rmbm] UpdateMaterialSlots: targetMaterials is null");
                materials = Array.Empty<MaterialSlot>();
                return;
            }

            Debug.Log($"[chisemf.rmbm] UpdateMaterialSlots: Updating to {targetMaterials.Length} slots");
            
            var oldMaterials = materials;
            materials = new MaterialSlot[targetMaterials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                // 既存の設定があれば保持、なければデフォルト値
                if (i < oldMaterials.Length)
                {
                    materials[i] = oldMaterials[i];
                }
                else
                {
                    materials[i] = new MaterialSlot
                    {
                        enabled = false,
                        mask = null,
                        mode = RemoveMode.RemoveBlack
                    };
                }
            }
            
            Debug.Log($"[chisemf.rmbm] UpdateMaterialSlots: Completed with {materials.Length} slots");
        }
    }
}

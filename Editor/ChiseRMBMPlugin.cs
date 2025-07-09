using chisemf.rmbm.runtime;
using nadena.dev.ndmf;
using UnityEngine;
using System.Linq;
using System;
using UnityEditor;
using System.Collections.Generic;

[assembly: ExportsPlugin(typeof(chisemf.rmbm.editor.ChiseRMBMPlugin))]

namespace chisemf.rmbm.editor
{
    /// <summary>
    /// ChiseRMBMコンポーネントを処理するNDMFプラグイン
    /// </summary>
    public class ChiseRMBMPlugin : Plugin<ChiseRMBMPlugin>
    {
        public override string QualifiedName => "chisemf.remove-mesh-by-mask";
        public override string DisplayName => "chisemf Remove Mesh By Mask";

        protected override void Configure()
        {
            // Transformingフェーズでメッシュ削除処理を実行
            InPhase(BuildPhase.Transforming).Run("Remove Mesh By Mask", ctx =>
            {
                var removeMeshComponents = ctx.AvatarRootObject.GetComponentsInChildren<ChiseRMBM>();

                foreach (var component in removeMeshComponents)
                {
                    ProcessChiseRMBM(ctx, component);
                    
                    // 処理完了後、コンポーネントを削除
                    UnityEngine.Object.DestroyImmediate(component);
                }
            });
        }

        /// <summary>
        /// ChiseRMBMコンポーネントを処理する
        /// </summary>
        private void ProcessChiseRMBM(BuildContext context, ChiseRMBM component)
        {
            // ターゲットオブジェクトからレンダラーを取得
            var targetRenderer = component.GetTargetRenderer();
            if (targetRenderer == null)
            {
                if (component.logToConsole)
                {
                    Debug.LogError($"[chisemf Remove Mesh By Mask] No target renderer found on {component.gameObject.name}. Please select a target mesh object.");
                }
                return;
            }

            // SkinnedMeshRendererの場合のみ処理（MeshRendererは現在未対応）
            var skinnedMeshRenderer = targetRenderer as SkinnedMeshRenderer;
            if (skinnedMeshRenderer == null)
            {
                if (component.logToConsole)
                {
                    Debug.LogError($"[chisemf Remove Mesh By Mask] Target object '{component.targetMeshObject.name}' has MeshRenderer. Only SkinnedMeshRenderer is supported.");
                }
                return;
            }

            var mesh = skinnedMeshRenderer.sharedMesh;
            if (mesh == null)
            {
                if (component.logToConsole)
                {
                    Debug.LogError($"[chisemf Remove Mesh By Mask] Shared mesh is null on target object '{component.targetMeshObject.name}'.");
                }
                return;
            }

            // 実際のメッシュ処理（AvatarOptimizerのアルゴリズムを移植）
            ProcessMeshWithMask(component, skinnedMeshRenderer, mesh);
        }

        /// <summary>
        /// マスクに基づいて実際にメッシュの三角形を削除する
        /// </summary>
        private void ProcessMeshWithMask(ChiseRMBM component, SkinnedMeshRenderer renderer, Mesh mesh)
        {
            var materialSettings = component.materials;

            // メッシュを複製して編集可能にする
            var newMesh = UnityEngine.Object.Instantiate(mesh);
            newMesh.name = mesh.name + "_Masked";

            bool meshModified = false;

            // 各サブメッシュを処理
            for (var i = 0; i < newMesh.subMeshCount && i < materialSettings.Length; i++)
            {
                var materialSetting = materialSettings[i];
                if (!materialSetting.enabled) continue;

                if (materialSetting.mask == null)
                {
                    if (component.logToConsole)
                    {
                        Debug.LogError($"[chisemf Remove Mesh By Mask] Mask is null for submesh {i}");
                    }
                    continue;
                }

                var mask = materialSetting.mask;
                var textureWidth = mask.width;
                var textureHeight = mask.height;
                Color32[] pixels;

                if (mask.isReadable)
                {
                    pixels = mask.GetPixels32();
                }
                else
                {
                    // テクスチャが読み取り可能でない場合の自動修正
                    var importer = GetTextureImporter(mask);
                    if (importer == null)
                    {
                        if (component.logToConsole)
                        {
                            Debug.LogError($"[chisemf Remove Mesh By Mask] Mask texture '{mask.name}' is not readable and cannot be fixed.");
                        }
                        continue;
                    }
                    else
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                        pixels = mask.GetPixels32();
                    }
                }

                // UV座標取得関数（AvatarOptimizerのGetValueと同じ）
                int GetValue(float u, float v)
                {
                    var x = Mathf.FloorToInt(Modulo(u, 1) * textureWidth);
                    var y = Mathf.FloorToInt(Modulo(v, 1) * textureHeight);
                    if (y * textureWidth + x < 0 || y * textureWidth + x >= pixels.Length) 
                    {
                        throw new IndexOutOfRangeException($"x: {x}, y: {y}, u: {u}, v: {v}, w: {textureWidth}, h: {textureHeight}, l: {pixels.Length}");
                    }
                    var pixel = pixels[y * textureWidth + x];
                    return Mathf.Max(Mathf.Max(pixel.r, pixel.g), pixel.b);
                }

                // 削除判定関数
                Func<float, float, bool> isRemoved;
                switch (materialSetting.mode)
                {
                    case ChiseRMBM.RemoveMode.RemoveWhite:
                        isRemoved = (u, v) => GetValue(u, v) > 127;
                        break;
                    case ChiseRMBM.RemoveMode.RemoveBlack:
                        isRemoved = (u, v) => GetValue(u, v) <= 127;
                        break;
                    default:
                        if (component.logToConsole)
                        {
                            Debug.LogError("[chisemf Remove Mesh By Mask] Unknown remove mode");
                        }
                        continue;
                }

                // サブメッシュの三角形を処理
                if (ProcessSubMesh(newMesh, i, isRemoved, component.logToConsole))
                {
                    meshModified = true;
                }
            }

            // メッシュが変更された場合のみ適用
            if (meshModified)
            {
                // 未使用の頂点を削除
                RemoveUnusedVertices(newMesh);
                // 新しいメッシュを適用
                renderer.sharedMesh = newMesh;
            }
        }

        /// <summary>
        /// サブメッシュの三角形を処理して、マスクに基づいて削除する
        /// </summary>
        private bool ProcessSubMesh(Mesh mesh, int submeshIndex, Func<float, float, bool> isRemoved, bool logToConsole)
        {
            var triangles = mesh.GetTriangles(submeshIndex);
            var uv = mesh.uv;
            if (uv == null || uv.Length == 0)
            {
                if (logToConsole)
                {
                    Debug.LogError($"[chisemf Remove Mesh By Mask] No UV coordinates found for submesh {submeshIndex}");
                }
                return false;
            }
            var newTriangles = new List<int>();
            int removedTriangleCount = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var v0 = triangles[i];
                var v1 = triangles[i + 1];
                var v2 = triangles[i + 2];
                bool shouldRemove = isRemoved(uv[v0].x, uv[v0].y) &&
                                   isRemoved(uv[v1].x, uv[v1].y) &&
                                   isRemoved(uv[v2].x, uv[v2].y);
                if (!shouldRemove)
                {
                    newTriangles.Add(v0);
                    newTriangles.Add(v1);
                    newTriangles.Add(v2);
                }
                else
                {
                    removedTriangleCount++;
                }
            }
            if (removedTriangleCount > 0)
            {
                mesh.SetTriangles(newTriangles, submeshIndex);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 未使用の頂点を削除（簡易版）
        /// </summary>
        private void RemoveUnusedVertices(Mesh mesh)
        {
            // この実装は簡易版です。完全な実装にはより複雑なアルゴリズムが必要
            // 現在は三角形の削除のみ行い、頂点の削除は行いません
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
        }

        /// <summary>
        /// テクスチャインポーターを取得
        /// </summary>
        private TextureImporter GetTextureImporter(Texture2D texture)
        {
            if (texture == null) return null;
            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetImporter.GetAtPath(path) as TextureImporter;
        }

        /// <summary>
        /// AvatarOptimizerのUtils.Moduloと同じ実装
        /// </summary>
        private float Modulo(float a, float b)
        {
            return a - b * Mathf.Floor(a / b);
        }
    }
}

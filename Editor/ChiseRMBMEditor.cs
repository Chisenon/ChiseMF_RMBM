using UnityEngine;
using UnityEditor;
using chisemf.rmbm.runtime;
using System.Collections.Generic;
using System.Linq;
using VRC.SDK3.Avatars.Components;

namespace chisemf.rmbm.editor
{
    [CustomEditor(typeof(ChiseRMBM), true)]
    public class ChiseRMBMEditor : Editor
    {
        private ChiseRMBM component;
        private SerializedProperty targetMeshObjectProp;
        private SerializedProperty materialsProp;
        private SerializedProperty logToConsoleProp;
        
        private List<GameObject> availableMeshObjects = new List<GameObject>();
        private string[] meshObjectNames = new string[0];
        private int selectedMeshObjectIndex = 0;

        private void OnEnable()
        {
            component = (ChiseRMBM)target;
            targetMeshObjectProp = serializedObject.FindProperty("targetMeshObject");
            materialsProp = serializedObject.FindProperty("materials");
            logToConsoleProp = serializedObject.FindProperty("logToConsole");
            
            RefreshMeshObjects();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // === Banner ===
            DrawBanner();
            
            // === Avatar ===
            DrawAvatarSection();
            
            // === Mesh Object ===
            DrawMeshObjectSection();
            
            // === Material Settings ===
            DrawMaterialSettingsSection();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// アバター内のメッシュオブジェクトを検索
        /// </summary>
        private void RefreshMeshObjects()
        {
            availableMeshObjects.Clear();

            // 先頭にnull（NONE）を追加
            availableMeshObjects.Add(null);

            // アバタールートを検索
            var avatarRoot = FindAvatarRoot();
            if (avatarRoot == null)
            {
                meshObjectNames = new string[] { "- NONE -", "No avatar found" };
                return;
            }

            // アバター内のメッシュレンダラーを持つオブジェクトを検索
            var renderers = avatarRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer)
                {
                    availableMeshObjects.Add(renderer.gameObject);
                }
            }

            // ドロップダウン用の名前配列を作成
            if (availableMeshObjects.Count > 1)
            {
                var names = availableMeshObjects.Skip(1).Select(obj => GetObjectDisplayName(obj, avatarRoot)).ToList();
                names.Insert(0, "- NONE -");
                meshObjectNames = names.ToArray();
            }
            else
            {
                meshObjectNames = new string[] { "- NONE -", "No mesh objects found" };
            }

            UpdateSelectedIndex();
        }

        /// <summary>
        /// アバタールートを検索
        /// </summary>
        private GameObject FindAvatarRoot()
        {
            // 現在のオブジェクトから上に向かってVRCAvatarDescriptorを検索
            Transform current = component.transform;
            while (current != null)
            {
                if (current.GetComponent<VRCAvatarDescriptor>() != null)
                {
                    return current.gameObject;
                }
                current = current.parent;
            }

            // 見つからない場合はシーン内で検索
            var avatarDescriptor = FindObjectOfType<VRCAvatarDescriptor>();
            return avatarDescriptor?.gameObject;
        }

        /// <summary>
        /// オブジェクトの表示名を生成（階層パスを含む）
        /// </summary>
        private string GetObjectDisplayName(GameObject obj, GameObject avatarRoot)
        {
            var renderer = obj.GetComponent<Renderer>();
            string rendererType = renderer is SkinnedMeshRenderer ? "[SMR]" : "[MR]";
            
            // アバタールートからの相対パスを生成
            string path = GetRelativePath(obj.transform, avatarRoot.transform);
            return $"{rendererType} {path}";
        }

        /// <summary>
        /// 相対パスを取得
        /// </summary>
        private string GetRelativePath(Transform target, Transform root)
        {
            if (target == root) return root.name;
            
            var path = new List<string>();
            Transform current = target;
            
            while (current != null && current != root)
            {
                path.Add(current.name);
                current = current.parent;
            }
            
            if (current == root && path.Count > 0)
            {
                path.Reverse();
                return string.Join("/", path);
            }
            
            return target.name;
        }

        /// <summary>
        /// 現在選択されているオブジェクトのインデックスを更新
        /// </summary>
        private void UpdateSelectedIndex()
        {
            selectedMeshObjectIndex = 0;
            if (component.targetMeshObject != null)
            {
                for (int i = 1; i < availableMeshObjects.Count; i++)
                {
                    if (availableMeshObjects[i] == component.targetMeshObject)
                    {
                        selectedMeshObjectIndex = i;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// マテリアルスロットの描画
        /// </summary>
        private void DrawMaterialSlots()
        {
            var targetMaterials = component.GetTargetMaterials();
            if (targetMaterials == null) 
            {
                EditorGUILayout.HelpBox("Target materials not found", MessageType.Warning);
                return;
            }

            if (component.materials.Length == 0)
            {
                EditorGUILayout.HelpBox("Initializing material slots...", MessageType.Info);
                component.UpdateMaterialSlots();
                EditorUtility.SetDirty(component);
                return;
            }

            for (int i = 0; i < component.materials.Length; i++)
            {
                DrawMaterialSlot(i, targetMaterials);
            }
        }

        /// <summary>
        /// 個別のマテリアルスロットを描画
        /// </summary>
        private void DrawMaterialSlot(int index, Material[] targetMaterials)
        {
            if (index >= materialsProp.arraySize)
            {
                EditorGUILayout.HelpBox($"Material slot {index} is out of bounds (array size : {materialsProp.arraySize})", MessageType.Error);
                return;
            }

            var material = index < targetMaterials.Length ? targetMaterials[index] : null;
            var slotProp = materialsProp.GetArrayElementAtIndex(index);
            
            // Material name for box title
            string materialName = material ? material.name : "None";
            
            EditorGUILayout.BeginVertical("box");
            
            // Slot header (checkbox + material name + status)
            EditorGUILayout.BeginHorizontal();
            var enabledProp = slotProp.FindPropertyRelative("enabled");
            
            // Checkbox
            bool newEnabled = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(20));
            if (newEnabled != enabledProp.boolValue)
            {
                enabledProp.boolValue = newEnabled;
            }
            
            // Material info
            EditorGUILayout.LabelField($"Material {index} : {materialName}", EditorStyles.boldLabel);
            
            // Status indicator with color
            string statusText = enabledProp.boolValue ? "Enabled" : "Disabled";
            Color statusColor = enabledProp.boolValue ? Color.green : Color.red;
            
            var statusStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = statusColor },
                fontStyle = FontStyle.Bold
            };
            
            EditorGUILayout.LabelField(statusText, statusStyle, GUILayout.Width(60));
            
            EditorGUILayout.EndHorizontal();

            // Show detailed settings only when enabled
            if (enabledProp.boolValue)
            {
                EditorGUI.indentLevel++;
                
                // Mask texture
                var maskProp = slotProp.FindPropertyRelative("mask");
                EditorGUILayout.PropertyField(maskProp, new GUIContent("Mask Texture"));
                
                // Remove mode
                var modeProp = slotProp.FindPropertyRelative("mode");
                EditorGUILayout.PropertyField(modeProp, new GUIContent("Remove Mode"));

                // Mask texture information display
                var maskTexture = maskProp.objectReferenceValue as Texture2D;
                if (maskTexture != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Size : {maskTexture.width} x {maskTexture.height}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Format : {maskTexture.format}", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Readable : {(maskTexture.isReadable ? "Yes" : "No")}", EditorStyles.miniLabel);

                    if (!maskTexture.isReadable)
                    {
                        if (GUILayout.Button("Fix", GUILayout.Width(50)))
                        {
                            MakeTextureReadable(maskTexture);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    if (!maskTexture.isReadable)
                    {
                        EditorGUILayout.HelpBox("Texture is not readable. Click 'Fix' to correct.", MessageType.Warning);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Mask texture is required", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        /// <summary>
        /// テクスチャを読み取り可能にする
        /// </summary>
        private void MakeTextureReadable(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                
                Debug.Log($"[chisemf.rmbm] Made texture readable: {texture.name}");
            }
        }

        /// <summary>
        /// Draw banner section
        /// </summary>
        private void DrawBanner()
        {
            // Banner style with consistent font size
            var bannerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            
            // Gradient background
            var rect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.4f, 0.8f, 0.8f));
            
            var labelRect = new Rect(rect.x, rect.y + 15, rect.width, 20);
            GUI.Label(labelRect, "Remove Mesh By Mask", bannerStyle);
            
            // Sub-title style
            var subTitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.LowerRight,
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(1f, 1f, 1f, 0.7f) }
            };
            
            var subLabelRect = new Rect(rect.x, rect.y + 32, rect.width - 5, 15);
            GUI.Label(subLabelRect, "Edit by Chisenon", subTitleStyle);
            
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// Draw avatar section
        /// </summary>
        private void DrawAvatarSection()
        {
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.LabelField("Avatar", EditorStyles.boldLabel);
            
            var avatarRoot = FindAvatarRoot();
            if (avatarRoot != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Current：", GUILayout.Width(60));
                EditorGUILayout.ObjectField(avatarRoot, typeof(GameObject), true);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("VRCAvatarDescriptor not found", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Draw mesh object selection section
        /// </summary>
        private void DrawMeshObjectSection()
        {
            EditorGUILayout.BeginVertical("box");

            // Header with title and refresh button
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mesh Object", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                RefreshMeshObjects();
            }
            EditorGUILayout.EndHorizontal();

            if (availableMeshObjects.Count <= 1)
            {
                EditorGUILayout.HelpBox("No mesh objects found", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            // Mesh object selection
            UpdateSelectedIndex();

            int newIndex = EditorGUILayout.Popup("Target", selectedMeshObjectIndex, meshObjectNames);
            if (newIndex != selectedMeshObjectIndex)
            {
                selectedMeshObjectIndex = newIndex;
                if (selectedMeshObjectIndex == 0)
                {
                    targetMeshObjectProp.objectReferenceValue = null;
                }
                else if (selectedMeshObjectIndex > 0 && selectedMeshObjectIndex < availableMeshObjects.Count)
                {
                    targetMeshObjectProp.objectReferenceValue = availableMeshObjects[selectedMeshObjectIndex];
                    component.UpdateMaterialSlots();
                }
                EditorUtility.SetDirty(component);
            }

            // Object information display
            if (component.targetMeshObject != null)
            {
                var renderer = component.GetTargetRenderer();
                if (renderer != null)
                {
                    var materials = component.GetTargetMaterials();
                    if (materials != null)
                    {
                        // Material slot array size check and auto update
                        if (component.materials.Length != materials.Length)
                        {
                            component.UpdateMaterialSlots();
                            EditorUtility.SetDirty(component);
                        }
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Draw material settings section
        /// </summary>
        private void DrawMaterialSettingsSection()
        {
            if (component.targetMeshObject == null || component.GetTargetRenderer() == null)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Material Settings", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Please select a mesh object", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var targetMaterials = component.GetTargetMaterials();
            if (targetMaterials == null || targetMaterials.Length == 0)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Material Settings", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Selected object has no materials", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Material Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            DrawMaterialSlots();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
    }
}

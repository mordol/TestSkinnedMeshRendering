using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace AnimationBaker.Editor
{
    public class AnimationBakerWindow : EditorWindow
    {
        [MenuItem("Window/Animation Baker")]
        public static void ShowWindow()
        {
            GetWindow<AnimationBakerWindow>("Animation Baker");
        }

        public GameObject characterRoot;
        public SkinnedMeshRenderer[] skinnedMeshRenderers;
        public AnimationClip[] animationClips;
        public Texture bakedTexture;
        
        Vector2 m_ScrollPos;
        bool m_ScrollFlag;
        
        private void OnGUI()
        {
            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos, false, false);
            m_ScrollFlag = true;

            //GUILayout.Space(10);
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical();
            {
                GUI_Settings_SkinnedMeshRenderer();
                EditorGUILayout.Space();
                
                GUI_Settings_AnimationClips();
                EditorGUILayout.Space();
                
                GUI_Settings_Output();
            }
            EditorGUILayout.EndVertical();

            if (m_ScrollFlag)
                EditorGUILayout.EndScrollView();

            m_ScrollFlag = false;
        }

        private void GUI_Settings_SkinnedMeshRenderer()
        {
            EditorGUILayout.LabelField("> Character", EditorStyles.boldLabel);
            
            Rect myRect = GUILayoutUtility.GetRect(0,30,GUILayout.ExpandWidth(true));
            GUI.Box(myRect,"Drag and Drop Prefab or FBX asset here");
            
            if (myRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    
                    var go = DragAndDrop.objectReferences[0] as GameObject;

                    if (go == null)
                    {
                        EditorUtility.DisplayDialog("Error", "Please drag and drop GameObject", "OK");
                    }
                    else if (go.scene.IsValid())
                    {
                        EditorUtility.DisplayDialog("Error", "Please drag and drop Prefab or FBX asset", "OK");
                    }
                    else
                    {
                        skinnedMeshRenderers = go.GetComponentsInChildren<SkinnedMeshRenderer>();
                        if (skinnedMeshRenderers != null && skinnedMeshRenderers.Length > 0)
                        {
                            characterRoot = go;
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Error", "SkinnedMeshRenderer not found in the GameObject", "OK");
                        }                        
                    }
                    
                    Event.current.Use();
                }
            }

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Root", GUILayout.Width(100));
                characterRoot = (GameObject)EditorGUILayout.ObjectField(characterRoot, typeof(GameObject), false);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.LabelField("Skinned Mesh Renderer", GUILayout.Width(150));

            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; skinnedMeshRenderers != null && i < skinnedMeshRenderers.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.ObjectField(skinnedMeshRenderers[i], typeof(SkinnedMeshRenderer), false);
                    }
                    EditorGUILayout.EndHorizontal();                
                }                
            }
        }
        
        private void GUI_Settings_AnimationClips()
        {
            EditorGUILayout.LabelField("> Animation Clips", EditorStyles.boldLabel);
            
            Rect myRect = GUILayoutUtility.GetRect(0,30,GUILayout.ExpandWidth(true));
            GUI.Box(myRect,"Drag and Drop Animation Clips or folder here");
            
            if (myRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    
                    if (DragAndDrop.objectReferences[0] as AnimationClip != null)
                    {
                        AddAnimationClipToList(DragAndDrop.objectReferences);
                    }
                    else if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                    {
                        var tempList = new List<AnimationClip>();
                        var assetPath = DragAndDrop.paths[0];
                        var assetPaths = AssetDatabase.FindAssets("t:AnimationClip", new string[] { assetPath });
                        foreach (var guid in assetPaths)
                        {
                            var clipPath = AssetDatabase.GUIDToAssetPath(guid);
                            tempList.Add(AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath));
                        }

                        AddAnimationClipToList(tempList.ToArray());
                    }
                    
                    Event.current.Use();
                }
            }
            
            if (animationClips != null && animationClips.Length > 0)
            {
                if (GUILayout.Button($"Clear clip list ({animationClips.Length})"))
                {
                    animationClips = null;
                    return;
                }

                for(int i = 0; i < animationClips.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    animationClips[i] = (AnimationClip)EditorGUILayout.ObjectField(animationClips[i], typeof(AnimationClip), false);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        
        void AddAnimationClipToList(UnityEngine.Object[] list)
        {
            var index = 0;
            if (animationClips == null)
            {
                animationClips = new AnimationClip[list.Length];
            }
            else
            {
                index = animationClips.Length;
                Array.Resize(ref animationClips, animationClips.Length + list.Length);
            }

            Array.Copy(list, 0, animationClips, index, list.Length);
        }
            
        private void GUI_Settings_Output()
        {
            EditorGUILayout.LabelField("> Output", EditorStyles.boldLabel);
            
            GUILayout.Space(10);
            if (GUILayout.Button("Bake"))
            {
                BakeAnimation();
                Debug.Log("Bake");
            }
            
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Output Path", GUILayout.Width(100));
                EditorGUILayout.TextField("");
            }
            EditorGUILayout.EndHorizontal();
            
            // TODO: Show baked animation information (clips)
            
            if (bakedTexture != null)
            {
                EditorGUILayout.Space();
                EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetRect(0, 200, 0, 200), bakedTexture, null, ScaleMode.ScaleToFit);
            }
        }

        private bool BakeAnimation()
        {
            if (skinnedMeshRenderers == null || skinnedMeshRenderers.Length == 0)
            {
                Debug.LogError("Skinned Mesh Renderer is not set");
                return false;
            }
            
            if (animationClips == null || animationClips.Length == 0)
            {
                Debug.LogError("Animation Clips are not set");
                return false;
            }
            
            // TODO: Bake animation with multiple SkinnedMeshRenderers
            var bakedAnimation = AnimationBakerUtil.BakeAnimation(characterRoot, skinnedMeshRenderers[0], animationClips);
            
            Debug.Log($"Texture {bakedAnimation.texture.width} x {bakedAnimation.texture.height}");
            foreach (var info in bakedAnimation.infos)
            {
                Debug.Log($"info: {info.name} row: {info.row} count: {info.count}");
            }
            
            Debug.Log($"uvstep ({1f / (float)bakedAnimation.texture.width}, {1 / (float)bakedAnimation.texture.height})");
            
            bakedTexture = bakedAnimation.texture;
            
            AssetDatabase.CreateAsset(bakedAnimation.texture, "Assets/TestBakedTexture.asset");
            return true;
        }
    }
}

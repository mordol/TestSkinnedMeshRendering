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

        public SkinnedMeshRenderer skinnedMeshRenderer;
        public AnimationClip[] animationClips;
        
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
            EditorGUILayout.LabelField("Skinned Mesh Renderer", EditorStyles.boldLabel);
            
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
                    
                    if (DragAndDrop.objectReferences[0] != null)
                    {
                        skinnedMeshRenderer = FindSkinnedMeshRenderer(DragAndDrop.objectReferences[0]);
                    }
                    Event.current.Use();
                }
            }
            
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Skinned Mesh Renderer", GUILayout.Width(150));
                skinnedMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(skinnedMeshRenderer, typeof(SkinnedMeshRenderer), false);
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private SkinnedMeshRenderer FindSkinnedMeshRenderer(UnityEngine.Object obj)
        {
            if (obj is SkinnedMeshRenderer)
            {
                return obj as SkinnedMeshRenderer;
            }
            
            if (obj is GameObject)
            {
                var go = obj as GameObject;
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    return smr;
                }
            }
            
            var subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(obj));
            foreach (var subAsset in subAssets)
            {
                var go = subAsset as GameObject;
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    return smr;
                }
            }
            
            return null;
        }
        
        private void GUI_Settings_AnimationClips()
        {
            EditorGUILayout.LabelField("Animation Clips", EditorStyles.boldLabel);
            
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
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            
            GUILayout.Space(10);
            if (GUILayout.Button("Bake"))
            {
                Debug.Log("Bake");
            }
            
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Output Path", GUILayout.Width(150));
                EditorGUILayout.TextField("");
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}

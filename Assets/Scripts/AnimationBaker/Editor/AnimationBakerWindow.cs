using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
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
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Skinned Mesh Renderer", GUILayout.Width(150));
                EditorGUILayout.ObjectField(null, typeof(SkinnedMeshRenderer), false);
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void GUI_Settings_AnimationClips()
        {
            EditorGUILayout.LabelField("Animation Clips", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Animation Clips", GUILayout.Width(150));
                EditorGUILayout.ObjectField(null, typeof(AnimationClip), true);
            }
            EditorGUILayout.EndHorizontal();
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

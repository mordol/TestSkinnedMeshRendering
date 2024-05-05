using System.Collections.Generic;
using UnityEngine;

namespace AnimationBaker
{
    public class BakedAnimationControl : MonoBehaviour
    {
        public BakedAnimationInfo bakedAnimationInfo;
        public int currentClipIndex;
        public bool autoPlay = false;

        int m_FrameID;
        BakedAnimationInfo.ClipInfo m_CurrentClipInfo;
        List<Material> m_Materials;
        float m_ElapsedTime;
        
        private void Awake()
        {
            m_Materials = new List<Material>();
            var meshRenderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var meshRenderer in meshRenderers)
            {
                var materials = meshRenderer.materials;
                foreach (var material in materials)
                {
                    material.SetTexture("_AnimMap", bakedAnimationInfo.texture);
                    material.SetVector("_UVStepForBone", new Vector4(bakedAnimationInfo.uvStep.x, bakedAnimationInfo.uvStep.y, 0, 0));
                    m_Materials.Add(material);
                }
            }

            m_FrameID = Shader.PropertyToID("_Frame");
        }

        // Start is called before the first frame update
        void Start()
        {
            if (autoPlay)
            {
                PlayAnimation(currentClipIndex);
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (m_CurrentClipInfo == null)
                return;
            
            // TODO: Delete this after test animation switching
            if (Input.GetKeyUp(KeyCode.Alpha1))
            {
                PlayAnimation(++currentClipIndex % bakedAnimationInfo.clipInfos.Length);
            }
            
            
            // TODO: framerate (fixed at 60 for now) and loop processing is required.
            var frameNum = (m_ElapsedTime * 60f) % m_CurrentClipInfo.count;
            frameNum += m_CurrentClipInfo.row;
            
            foreach (var material in m_Materials)
            {
                material.SetFloat(m_FrameID, frameNum);
            }
            
            m_ElapsedTime += Time.deltaTime;
        }
        
        public bool PlayAnimation(int clipIndex, float startTime = 0)
        {
            if (bakedAnimationInfo == null)
                return false;
            
            if (clipIndex < 0 || clipIndex >= bakedAnimationInfo.clipInfos.Length)
                return false;

            currentClipIndex = clipIndex;
            m_CurrentClipInfo = bakedAnimationInfo.clipInfos[currentClipIndex];
            m_ElapsedTime = startTime;
            return true;
        }
    }
}
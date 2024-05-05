using System.Collections.Generic;
using UnityEngine;

namespace AnimationBaker
{
    public class BakedAnimationInfo: ScriptableObject
    {
        [System.Serializable]
        public class ClipInfo
        {
            public string name;
            public int row;         // first row index in the texture
            public int count;
            
            public int GetRandomFrame()
            {
                return Random.Range(0, count) + row;
            }
        }
            
        public ClipInfo[] clipInfos;
        public Texture2D texture;
        public Vector2 uvStep;
        
        Dictionary<string, ClipInfo> m_ClipInfoDic;
        
        public void OnEnable()
        {
            if (m_ClipInfoDic != null)
                return;
            
            m_ClipInfoDic = new Dictionary<string, ClipInfo>();
            foreach (var info in clipInfos)
            {
                m_ClipInfoDic[info.name] = info;
            }
        }
        
        public ClipInfo GetClipInfo(string clipName)
        {
            if (m_ClipInfoDic == null)
                return null;
            
            if (m_ClipInfoDic.TryGetValue(clipName, out var clipInfo))
                return clipInfo;
            
            return null;
        }
    }
}

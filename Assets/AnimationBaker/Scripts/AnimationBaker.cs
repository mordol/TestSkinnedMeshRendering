using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace AnimationBaker
{
    public struct BakedAnimation
    {
        public struct ClipInfo
        {
            public string name;
            public int row;         // first row index in the texture
            public int count;
        }
            
        public ClipInfo[] infos;
        public Texture2D texture;
    }

}

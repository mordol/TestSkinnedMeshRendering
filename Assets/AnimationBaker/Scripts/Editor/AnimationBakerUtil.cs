using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AnimationBaker.Editor
{
    public static class AnimationBakerUtil
    {
        public static BakedAnimation BakeAnimation(GameObject root, SkinnedMeshRenderer skinnedMeshRenderer, AnimationClip[] animationClips)
        {
            var bakedAnimation = new BakedAnimation();
            
            var bones = skinnedMeshRenderer.bones;
            var bindPoses = skinnedMeshRenderer.sharedMesh.bindposes;
            
            // Result texture specification
                // 1. Each row is a frame
                // 2. Each column(3 pixels + (0,0,0,1)) is a 4x4 matrix for bone
                
            var textureWidth = bones.Length * 3;
            var textureHeight = 0;
            bakedAnimation.infos = new BakedAnimation.ClipInfo[animationClips.Length];

            for (int i = 0; i < animationClips.Length; i++)
            {
                var clip = animationClips[i];
                
                bakedAnimation.infos[i].name = clip.name;
                bakedAnimation.infos[i].row = textureHeight;
                bakedAnimation.infos[i].count = Mathf.CeilToInt(clip.length * clip.frameRate);
                
                textureHeight += bakedAnimation.infos[i].count;
            }
            
            // Using 16bit float(RGBAHalf) for transform matrix
            bakedAnimation.texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBAHalf, false, true);
            //var texView = texture.GetPixelData<half4>(0);
            var texData = new NativeArray<half4>(textureWidth * textureHeight, Allocator.Temp);

            for (int i = 0; i < animationClips.Length; i++)
            {
                for (int j = 0; j < bakedAnimation.infos[i].count; j++)
                {
                    var row = bakedAnimation.infos[i].row + j;
                    var baseIndex = row * textureWidth;
                    
                    // Sample animation
                    var time = j / animationClips[i].frameRate;
                    animationClips[i].SampleAnimation(root, time);

                    bones = skinnedMeshRenderer.bones;
                    for (int k = 0; k < bones.Length; k++)
                    {
                        var bone = bones[k];
                        var boneIndex = k * 3;
                        var mtx = bone.localToWorldMatrix * bindPoses[k];

                        texData[baseIndex + boneIndex + 0] = new half4(mtx.GetRow(0));
                        texData[baseIndex + boneIndex + 1] = new half4(mtx.GetRow(1));
                        texData[baseIndex + boneIndex + 2] = new half4(mtx.GetRow(2));
                        
                        //Debug.Log($"frame{j}.bone{k}: {mtx.GetRow(0)} {mtx.GetRow(1)} {mtx.GetRow(2)}");
                    }
                }
            }
            
            // Save texture
            bakedAnimation.texture.SetPixelData(texData, 0);
            bakedAnimation.texture.Apply();
            
            return bakedAnimation;
        }
    }
}
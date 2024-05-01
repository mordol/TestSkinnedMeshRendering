using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AnimationBaker.Editor
{
    public static class AnimationBakerUtil
    {
        public static BakedAnimationInfo BakeAnimation(GameObject root, SkinnedMeshRenderer skinnedMeshRenderer, AnimationClip[] animationClips)
        {
            var bakedAnimation = new BakedAnimationInfo();
            
            var bones = skinnedMeshRenderer.bones;
            var bindPoses = skinnedMeshRenderer.sharedMesh.bindposes;
            
            // Result texture specification
                // 1. Each row is a frame
                // 2. Each column(3 pixels + (0,0,0,1)) is a 4x4 matrix for bone
                
            var textureWidth = bones.Length * 3;
            var textureHeight = 0;
            bakedAnimation.clipInfos = new BakedAnimationInfo.ClipInfo[animationClips.Length];

            for (int i = 0; i < animationClips.Length; i++)
            {
                var clip = animationClips[i];
                var clipInfo = new BakedAnimationInfo.ClipInfo();
                
                clipInfo.name = clip.name;
                clipInfo.row = textureHeight;
                clipInfo.count = Mathf.CeilToInt(clip.length * clip.frameRate);
                
                bakedAnimation.clipInfos[i] = clipInfo;
                textureHeight += clipInfo.count;
            }
            
            bakedAnimation.uvStep = new Vector2(1f / textureWidth, 1f / textureHeight);
            
            // Using 16bit float(RGBAHalf) for transform matrix
            bakedAnimation.texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBAHalf, false, true);
            var texData = new NativeArray<half4>(textureWidth * textureHeight, Allocator.Temp);

            for (int i = 0; i < animationClips.Length; i++)
            {
                for (int j = 0; j < bakedAnimation.clipInfos[i].count; j++)
                {
                    var row = bakedAnimation.clipInfos[i].row + j;
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
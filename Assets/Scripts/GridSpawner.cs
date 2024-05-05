using System.Collections;
using System.Collections.Generic;
using AnimationBaker;
using UnityEngine;

public class GridSpawner : MonoBehaviour
{
    public GameObject spawnPrefab;
    public int spawnCount = 1000;
    public float gridSize = 0.5f;
    
    // Start is called before the first frame update
    void Start()
    {
        if (spawnPrefab == null || spawnCount <= 0)
        {
            return;
        }

        var gridCount = Mathf.CeilToInt(Mathf.Sqrt(spawnCount));
        var halfGridCount = Mathf.CeilToInt(gridCount / 2f);
        var spawnIndex = 0;
        
        for (int x = -halfGridCount; x < halfGridCount; x++)
        {
            for (int z = -halfGridCount; z < halfGridCount; z++)
            {
                if (spawnIndex++ >= spawnCount)
                    break;
                
                Vector3 spawnPosition = new Vector3(x * gridSize, 0, z * gridSize);
                
                var instance = Instantiate(spawnPrefab, spawnPosition, Quaternion.identity);
                var animControl = instance.GetComponent<BakedAnimationControl>();
                if (animControl != null)
                {
                    var clipIndex = Random.Range(0, animControl.bakedAnimationInfo.clipInfos.Length);
                    var clipInfo = animControl.bakedAnimationInfo.clipInfos[clipIndex];
                    animControl.PlayAnimation(clipIndex, Random.Range(0f, clipInfo.count / 60f));
                }
                else
                {
                    var animation = instance.GetComponent<Animation>();
                    if (animation != null)
                    {
                        var clipIndex = Random.Range(0, animation.GetClipCount());
                        foreach (AnimationState state in animation)
                        {
                            if (clipIndex-- == 0)
                            {
                                animation.Play(state.name);
                                state.wrapMode = WrapMode.Loop;
                                state.time = Random.Range(0f, state.length);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}

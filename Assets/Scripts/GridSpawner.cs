using System.Collections;
using System.Collections.Generic;
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
                
                Vector3 spawnPosition = new Vector3(
                    x * gridSize,
                    0,
                    z * gridSize
                );
                
                Instantiate(spawnPrefab, spawnPosition, Quaternion.identity);
            }
        }
    }
}

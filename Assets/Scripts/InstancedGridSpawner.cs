using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstancedGridSpawner : MonoBehaviour
{
    public GameObject spawnPrefab;
    public int spawnCount = 1000;
    public float gridSize = 0.5f;

    [System.Serializable]
    public struct MeshInfoForInstancing
    {
        public Mesh mesh;
        public Material[] materials;
        public ComputeBuffer[] argsBuffers;
    }
    
    public MeshInfoForInstancing[] meshInfos;
    
    ComputeBuffer meshPropertiesBuffer;
    Bounds bounds;
    
    struct MeshProperties
    {
        public Matrix4x4 mat;
        public float frame;

        public static int Size() {
            return
                sizeof(float) * 4 * 4 + // matrix;
                sizeof(float) * 1;      // frame;
        }
    }
    
    // Start is called before the first frame update
    void Start()
    {
        if (spawnPrefab == null || spawnCount <= 0)
            return;
        
        // Collecting mesh and materials information from spawnPrefab
        var meshRenderers = spawnPrefab.GetComponentsInChildren<MeshRenderer>();
        if (meshRenderers == null || meshRenderers.Length <= 0)
            return;
        
        meshInfos = new MeshInfoForInstancing[meshRenderers.Length];
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            meshInfos[i].mesh = meshRenderers[i].GetComponent<MeshFilter>().sharedMesh;
            meshInfos[i].materials = meshRenderers[i].sharedMaterials;
        }
        
        Initialize();
    }
    
    private void Update()
    {
        for (int i = 0; i < meshInfos.Length; i++)
        {
            var meshInfo = meshInfos[i];
            
            if (meshInfo.mesh is null || meshInfo.materials == null || meshInfo.materials.Length <= 0 || meshInfo.argsBuffers == null || meshInfo.argsBuffers.Length <= 0)
                continue;
            
            for (int j = 0; j < meshInfo.materials.Length; j++)
            {
                Graphics.DrawMeshInstancedIndirect(
                    meshInfo.mesh, j,
                    meshInfo.materials[j],
                    bounds,
                    meshInfo.argsBuffers[j]);
            }
        }
    }
    
    void Initialize()
    {
        MeshProperties[] properties = new MeshProperties[spawnCount];
        var gridCount = Mathf.CeilToInt(Mathf.Sqrt(spawnCount));
        var halfGridCount = Mathf.CeilToInt(gridCount / 2f);
        bounds = new Bounds(Vector3.zero, new Vector3(gridCount * gridSize, 1f, gridCount * gridSize));
        var spawnIndex = 0;
        
        for (int x = -halfGridCount; x < halfGridCount; x++)
        {
            for (int z = -halfGridCount; z < halfGridCount; z++)
            {
                if (spawnIndex >= spawnCount)
                    break;
                
                MeshProperties props = new MeshProperties();
                Vector3 position = new Vector3(x * gridSize, 0, z * gridSize);
                Quaternion rotation = Quaternion.identity;
                Vector3 scale = Vector3.one;

                props.mat = Matrix4x4.TRS(position, rotation, scale);
                props.frame = 0f;
                properties[spawnIndex++] = props;
            }
        }
        
        meshPropertiesBuffer = new ComputeBuffer(spawnCount, MeshProperties.Size());
        meshPropertiesBuffer.SetData(properties);
        
        for (int i = 0; i < meshInfos.Length; i++)
        {
            var mesh = meshInfos[i].mesh;
            if (mesh is null)
                continue;
            
            var materials = meshInfos[i].materials;
            if (materials == null || materials.Length <= 0)
                continue;
            
            meshInfos[i].argsBuffers = new ComputeBuffer[materials.Length];
            
            for (int j = 0; j < materials.Length; j++)
            {
                materials[j].SetBuffer("_Properties", meshPropertiesBuffer);
                //Debug.Log($"mesh[{i}] material[{j}] indexCount: {mesh.GetIndexCount(j)}, indexStart: {mesh.GetIndexStart(j)}, baseVertex: {mesh.GetBaseVertex(j)}");
                
                meshInfos[i].argsBuffers[j] = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
                meshInfos[i].argsBuffers[j].SetData(new uint[5]
                {
                    (uint)mesh.GetIndexCount(j), 
                    (uint)spawnCount,
                    (uint)mesh.GetIndexStart(j), 
                    (uint)mesh.GetBaseVertex(j),
                    0
                });
            }
        }
    }
    
    private void OnDisable()
    {
        if (meshPropertiesBuffer != null)
        {
            meshPropertiesBuffer.Release();
            meshPropertiesBuffer = null;
        }
        
        for (int i = 0; i < meshInfos.Length; i++)
        {
            if (meshInfos[i].argsBuffers == null)
                continue;
            
            for (int j = 0; j < meshInfos[i].argsBuffers.Length; j++)
            {
                if (meshInfos[i].argsBuffers[j] != null)
                {
                    meshInfos[i].argsBuffers[j].Release();
                    meshInfos[i].argsBuffers[j] = null;
                }
            }
        }
    }
}

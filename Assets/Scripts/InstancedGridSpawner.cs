using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstancedGridSpawner : MonoBehaviour
{
    public GameObject spawnPrefab;
    public int spawnCount = 1000;
    public float gridSize = 0.5f;
    public AnimationBaker.BakedAnimationInfo bakedAnimationInfo;

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

        public static int Size()
        {
            return
                sizeof(float) * 4 * 4 + // matrix;
                sizeof(float) * 1;      // frame;
        }
    }
    
    // For animation frame calculation
    struct ClipInfo
    {
        public float row;
        public float count;
        public float frameStep;
        
        public static int Size()
        {
            return
                sizeof(float) * 3; // row, count, frameStep
        }
    }
    
    public ComputeShader animationFrameCompute;
    ComputeBuffer animationClipInfoBuffer;

    // The current animation clip index of the instance
    int[] currentClipIndices;
    
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
        
        currentClipIndices = new int[spawnCount];
        for (int i = 0; i < spawnCount; i++)
        {
            currentClipIndices[i] = Random.Range(0, bakedAnimationInfo.clipInfos.Length);
        }
        
        Initialize();
    }
    
    private void Update()
    {
        if (meshInfos == null || meshInfos.Length <= 0)
            return;
        
        var kernel = animationFrameCompute.FindKernel("AnimationFrame");
        animationFrameCompute.SetFloat("_TimeDelta", Time.deltaTime);
        animationFrameCompute.Dispatch(kernel, Mathf.CeilToInt(spawnCount / 64f), 1, 1);
        
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
                props.frame = bakedAnimationInfo.clipInfos[currentClipIndices[spawnIndex]].GetRandomFrame();
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
                
                materials[j].SetTexture("_AnimMap", bakedAnimationInfo.texture);
                materials[j].SetVector("_UVStepForBone", new Vector4(bakedAnimationInfo.uvStep.x, bakedAnimationInfo.uvStep.y, 0, 0));
            }
        }
        
        // For animation frame calculation
        var kernel = animationFrameCompute.FindKernel("AnimationFrame");
        animationFrameCompute.SetBuffer(kernel, "_Properties", meshPropertiesBuffer);
        
        ClipInfo[] clipInfos = new ClipInfo[spawnCount];

        for (int i = 0; i < spawnCount; i++)
        {
            var clipInfo = bakedAnimationInfo.clipInfos[currentClipIndices[i]];
            clipInfos[i].row = clipInfo.row;
            clipInfos[i].count = clipInfo.count;
            clipInfos[i].frameStep = 60f;
        }
        
        animationClipInfoBuffer = new ComputeBuffer(spawnCount, ClipInfo.Size());
        animationClipInfoBuffer.SetData(clipInfos);
        animationFrameCompute.SetBuffer(kernel, "_ClipInfo", animationClipInfoBuffer);
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
        
        if (animationClipInfoBuffer != null)
        {
            animationClipInfoBuffer.Release();
            animationClipInfoBuffer = null;
        }
    }
}

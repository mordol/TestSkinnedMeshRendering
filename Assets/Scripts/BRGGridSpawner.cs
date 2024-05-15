using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public class BRGGridSpawner : MonoBehaviour
{
    public GameObject spawnPrefab;
    public int spawnCount = 1000;
    public float gridSize = 0.5f;
    public AnimationBaker.BakedAnimationInfo bakedAnimationInfo;
    [FormerlySerializedAs("TrackingTarget")] public Transform trackingTarget;
    
    [System.Serializable]
    public struct MeshInfoForInstancing
    {
        public Mesh mesh;
        public Material[] materials;
        
        public BatchMeshID meshID;
        public BatchMaterialID[] materialIDs;
    }
    
    public MeshInfoForInstancing[] meshInfos;
    
    // For animation frame calculation with compute shader
    struct AnimationProperties
    {
        public float clipIndex;
        public float frame;

        public static int Size()
        {
            return
                sizeof(float) * 1 +     // clipIndex;
                sizeof(float) * 1;      // frame;
        }
    }
    
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
    public string animationFrameComputeKernelName = "AnimationFrameForBRG";
    ComputeBuffer animationPropertiesBuffer;
    ComputeBuffer animationClipInfoBuffer;
    
    
    // For BRG
    
    private BatchRendererGroup m_BRG;
    int drawCommnadsCount = 0;
    
    private GraphicsBuffer m_InstanceData;
    private BatchID m_BatchID;

    // Some helper constants to make calculations more convenient.
    private const int kSizeOfMatrix = sizeof(float) * 4 * 4;	// 64
    private const int kSizeOfPackedMatrix = sizeof(float) * 4 * 3;	// 48
    private const int kSizeOfFloat4 = sizeof(float) * 4;		// 16
    private const int kBytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4;	// 96 + 16 = 112
    private const int kExtraBytes = kSizeOfMatrix * 2;		// 128

    // The PackedMatrix is a convenience type that converts matrices into
    // the format that Unity-provided SRP shaders expect.
    struct PackedMatrix
    {
        public float c0x;
        public float c0y;
        public float c0z;
        public float c1x;
        public float c1y;
        public float c1z;
        public float c2x;
        public float c2y;
        public float c2z;
        public float c3x;
        public float c3y;
        public float c3z;

        public PackedMatrix(Matrix4x4 m)
        {
            c0x = m.m00;
            c0y = m.m10;
            c0z = m.m20;
            c1x = m.m01;
            c1y = m.m11;
            c1z = m.m21;
            c2x = m.m02;
            c2y = m.m12;
            c2z = m.m22;
            c3x = m.m03;
            c3y = m.m13;
            c3z = m.m23;
        }
    }

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
        
        InitializeComputeBuffer();
        
        // Create BRG instance
        m_BRG = new BatchRendererGroup(this.OnPerformCulling, IntPtr.Zero);
        
        drawCommnadsCount = 0;
        for (int i = 0; i < meshInfos.Length; i++)
        {
            var mesh = meshInfos[i].mesh;
            if (mesh is null)
                continue;

            var materials = meshInfos[i].materials;
            if (materials == null || materials.Length <= 0)
                continue;
            
            meshInfos[i].meshID = m_BRG.RegisterMesh(mesh);
            meshInfos[i].materialIDs = new BatchMaterialID[materials.Length];
            
            for (int j = 0; j < materials.Length; j++)
            {
                materials[j].SetTexture("_AnimMap", bakedAnimationInfo.texture);
                materials[j].SetVector("_UVStepForBone", new Vector4(bakedAnimationInfo.uvStep.x, bakedAnimationInfo.uvStep.y, 0, 0));
                materials[j].SetBuffer("_Properties", animationPropertiesBuffer);
                
                meshInfos[i].materialIDs[j] = m_BRG.RegisterMaterial(materials[j]);
                drawCommnadsCount++;
            }
        }

        AllocateInstanceDateBuffer();
        PopulateInstanceDataBuffer();
        
        // TODO: refactoring
        var kernel = animationFrameCompute.FindKernel(animationFrameComputeKernelName);
        animationFrameCompute.SetBuffer(kernel, "_InstanceData", m_InstanceData);
    }

    void InitializeComputeBuffer()
    {
        ClipInfo[] clipInfos = new ClipInfo[bakedAnimationInfo.clipInfos.Length];
        for (int i = 0; i < clipInfos.Length; i++)
        {
            var clipInfo = bakedAnimationInfo.clipInfos[i];
            clipInfos[i].row = clipInfo.row;
            clipInfos[i].count = clipInfo.count;
            clipInfos[i].frameStep = 60f;
        }
        
        AnimationProperties[] properties = new AnimationProperties[spawnCount];
        for (int i = 0; i < spawnCount; i++)
        {
            var clipIndex = UnityEngine.Random.Range(0, clipInfos.Length);
            properties[i].clipIndex = clipIndex; 
            properties[i].frame = bakedAnimationInfo.clipInfos[clipIndex].GetRandomFrame();
        }
        
        animationPropertiesBuffer = new ComputeBuffer(spawnCount, AnimationProperties.Size());
        animationPropertiesBuffer.SetData(properties);

        animationClipInfoBuffer = new ComputeBuffer(clipInfos.Length, ClipInfo.Size());
        animationClipInfoBuffer.SetData(clipInfos);
        
        // For animation frame calculation
        var kernel = animationFrameCompute.FindKernel(animationFrameComputeKernelName);
        animationFrameCompute.SetBuffer(kernel, "_Properties", animationPropertiesBuffer);
        animationFrameCompute.SetBuffer(kernel, "_ClipInfo", animationClipInfoBuffer);
        animationFrameCompute.SetInt("_InstanceCount", spawnCount);
    }

    void AllocateInstanceDateBuffer()
    {
        // kBytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4; (obj2world, world2obj, color)
        // https://docs.unity3d.com/ScriptReference/GraphicsBuffer-ctor.html
           // public GraphicsBuffer(GraphicsBuffer.Target target, int count, int stride);
        m_InstanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
            BufferCountForInstances(kBytesPerInstance, spawnCount, kExtraBytes),
            sizeof(int)); // 112, 3,128, 4?
    }

    // Raw buffers are allocated in ints. This is a utility method that calculates
    // the required number of ints for the data.
    int BufferCountForInstances(int bytesPerInstance, int numInstances, int extraBytes = 0)
    {
        // Round byte counts to int multiples
        bytesPerInstance = (bytesPerInstance + sizeof(int) - 1) / sizeof(int) * sizeof(int);
           // (112 + 4 - 1) / 4 * 4 = 115 / 4 * 4 = 28.75 * 4 = 28 * 4 = 112
        extraBytes = (extraBytes + sizeof(int) - 1) / sizeof(int) * sizeof(int);
           // (128 + 3) / 4 * 4 = 131 / 4 * 4 = 32.75 * 4 = 128
        int totalBytes = bytesPerInstance * numInstances + extraBytes;
           // 112 * 3 + 128 = 464
        return totalBytes / sizeof(int);
           // 464 / 4 = 116
    }

    void PopulateInstanceDataBuffer()
    {
        // Place a zero matrix at the start of the instance data buffer, so loads from address 0 return zero.
        var zero = new Matrix4x4[1] { Matrix4x4.zero };
        
        var gridCount = Mathf.CeilToInt(Mathf.Sqrt(spawnCount));
        var halfGridCount = Mathf.CeilToInt(gridCount / 2f);
        var spawnIndex = 0;

        //var matrices = new Matrix4x4[spawnCount];
        var objectToWorld = new PackedMatrix[spawnCount];
        var worldToObject = new PackedMatrix[spawnCount];
        var colors = new Vector4[spawnCount];
        
        for (int x = -halfGridCount; x < halfGridCount; x++)
        {
            for (int z = -halfGridCount; z < halfGridCount; z++)
            {
                if (spawnIndex >= spawnCount)
                    break;
                
                Vector3 position = new Vector3(x * gridSize, 0, z * gridSize);
                Quaternion rotation = Quaternion.identity;
                Vector3 scale = Vector3.one;
                var matrix = Matrix4x4.TRS(position, rotation, scale);
                
                // Convert the transform matrices into the packed format that the shader expects.
                objectToWorld[spawnIndex] = new PackedMatrix(matrix);
                // Also create packed inverse matrices.
                worldToObject[spawnIndex] = new PackedMatrix(matrix.inverse);
                colors[spawnIndex] = new Vector4(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 1);
                spawnIndex++;
            }
        }
        
        // In this simple example, the instance data is placed into the buffer like this:
        // Offset | Description
        //      0 | 64 bytes of zeroes, so loads from address 0 return zeroes (float4x4 matrix 64byte)
        //     64 | 32 uninitialized bytes to make working with SetData easier, otherwise unnecessary
        //     96 | unity_ObjectToWorld, three packed float3x4 matrices (48byte * 3 = 144byte)
        //    240 | unity_WorldToObject, three packed float3x4 matrices
        //    384 | _BaseColor, three float4s (16byte)
        // Calculates start addresses for the different instanced properties. unity_ObjectToWorld starts
        // at address 96 instead of 64, because the computeBufferStartIndex parameter of SetData
        // is expressed as source array elements, so it is easier to work in multiples of sizeof(PackedMatrix).
        uint byteAddressObjectToWorld = kSizeOfPackedMatrix * 2;
        uint byteAddressWorldToObject = byteAddressObjectToWorld + kSizeOfPackedMatrix * (uint)spawnCount;
        uint byteAddressColor = byteAddressWorldToObject + kSizeOfPackedMatrix * (uint)spawnCount;
        
        // Upload the instance data to the GraphicsBuffer so the shader can load them.
        // https://docs.unity3d.com/ScriptReference/GraphicsBuffer.SetData.html
        // public void SetData(Array data, int managedBufferStartIndex, int graphicsBufferStartIndex, int count);
           // graphicsBufferStartIndex: The first element index in the graphics buffer to receive the data.
        m_InstanceData.SetData(zero, 0, 0, 1);
        m_InstanceData.SetData(objectToWorld, 0, (int)(byteAddressObjectToWorld / kSizeOfPackedMatrix), objectToWorld.Length); // 96 / 48 = 2
        m_InstanceData.SetData(worldToObject, 0, (int)(byteAddressWorldToObject / kSizeOfPackedMatrix), worldToObject.Length); // 240 / 48 = 5
        m_InstanceData.SetData(colors, 0, (int)(byteAddressColor / kSizeOfFloat4), colors.Length); // 384 / 16 = 24
        
        // Set up metadata values to point to the instance data. Set the most significant bit 0x80000000 in each
        // which instructs the shader that the data is an array with one value per instance, indexed by the instance index.
        // Any metadata values that the shader uses that are not set here will be 0. When a value of 0 is used with
        // UNITY_ACCESS_DOTS_INSTANCED_PROP (i.e. without a default), the shader interprets the
        // 0x00000000 metadata value and loads from the start of the buffer. The start of the buffer is
        // a zero matrix so this sort of load is guaranteed to return zero, which is a reasonable default value.
        var metadata = new NativeArray<MetadataValue>(3, Allocator.Temp);
        metadata[0] = new MetadataValue
        {
            NameID = Shader.PropertyToID("unity_ObjectToWorld"),
            Value = 0x80000000 | byteAddressObjectToWorld,
        };
        metadata[1] = new MetadataValue
        {
            NameID = Shader.PropertyToID("unity_WorldToObject"), 
            Value = 0x80000000 | byteAddressWorldToObject,
        };
        metadata[2] = new MetadataValue
        {
            NameID = Shader.PropertyToID("_BaseColor"), 
            Value = 0x80000000 | byteAddressColor,
        };
        
        // Finally, create a batch for the instances and make the batch use the GraphicsBuffer with the
        // instance data as well as the metadata values that specify where the properties are.
        m_BatchID = m_BRG.AddBatch(metadata, m_InstanceData.bufferHandle);
    }

    void OnDisable()
    {
        m_BRG.Dispose();
        m_InstanceData.Dispose();
        
        if (animationPropertiesBuffer != null)
        {
            animationPropertiesBuffer.Release();
            animationPropertiesBuffer = null;
        }
        
        if (animationClipInfoBuffer != null)
        {
            animationClipInfoBuffer.Release();
            animationClipInfoBuffer = null;
        }
    }

    void Update()
    {
        if (meshInfos == null || meshInfos.Length <= 0)
            return;
        
        var kernel = animationFrameCompute.FindKernel(animationFrameComputeKernelName);
        animationFrameCompute.SetFloat("_TimeDelta", Time.deltaTime);
        if (trackingTarget != null)
        {
            animationFrameCompute.SetVector("_TargetPosition", trackingTarget.position);
        }
        animationFrameCompute.Dispatch(kernel, Mathf.CeilToInt(spawnCount / 64f), 1, 1);   
    }

    unsafe JobHandle OnPerformCulling(
        BatchRendererGroup rendererGroup,
        BatchCullingContext cullingContext,
        BatchCullingOutput cullingOutput,
        IntPtr userContext)
    {
        // UnsafeUtility.Malloc() requires an alignment, so use the largest integer type's alignment
        // which is a reasonable default.
        int alignment = UnsafeUtility.AlignOf<long>();

        // Acquire a pointer to the BatchCullingOutputDrawCommands struct so you can easily
        // modify it directly.
        var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
        
        // Allocate memory for the output arrays. In a more complicated implementation, you would calculate
        // the amount of memory to allocate dynamically based on what is visible.
        // This example assumes that all of the instances are visible and thus allocates
        // memory for each of them. The necessary allocations are as follows:
        // - a single draw command (which draws kNumInstances instances)
        // - a single draw range (which covers our single draw command)
        // - kNumInstances visible instance indices.
        // You must always allocate the arrays using Allocator.TempJob.
        drawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>() * drawCommnadsCount, alignment, Allocator.TempJob);
        drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(), alignment, Allocator.TempJob);
        drawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(spawnCount * sizeof(int), alignment, Allocator.TempJob);

        drawCommands->drawCommandPickingInstanceIDs = null;
        drawCommands->drawCommandCount = drawCommnadsCount;
        drawCommands->drawRangeCount = 1;
        drawCommands->visibleInstanceCount = spawnCount;

        // This example doens't use depth sorting, so it leaves instanceSortingPositions as null.
        drawCommands->instanceSortingPositions = null;
        drawCommands->instanceSortingPositionFloatCount = 0;

        int drawCommandIndex = 0;
        for (int i = 0; i < meshInfos.Length; i++)
        {
            var mesh = meshInfos[i].mesh;
            if (mesh is null)
                continue;
        
            var materials = meshInfos[i].materials;
            if (materials == null || materials.Length <= 0)
                continue;
        
            for (int j = 0; j < materials.Length; j++)
            {
                // Configure the single draw command to draw kNumInstances instances
                // starting from offset 0 in the array, using the batch, material and mesh
                // IDs registered in the Start() method. It doesn't set any special flags.
                drawCommands->drawCommands[drawCommandIndex].visibleOffset = 0;
                drawCommands->drawCommands[drawCommandIndex].visibleCount = (uint)spawnCount;
                drawCommands->drawCommands[drawCommandIndex].batchID = m_BatchID;
                drawCommands->drawCommands[drawCommandIndex].materialID = meshInfos[i].materialIDs[j];
                drawCommands->drawCommands[drawCommandIndex].meshID = meshInfos[i].meshID;
                drawCommands->drawCommands[drawCommandIndex].submeshIndex = (ushort)j;
                drawCommands->drawCommands[drawCommandIndex].splitVisibilityMask = 0xff;
                drawCommands->drawCommands[drawCommandIndex].flags = 0;
                drawCommands->drawCommands[drawCommandIndex].sortingPosition = 0;
                drawCommandIndex++;
            }
        }
        
        // Configure the single draw range to cover the single draw command which
        // is at offset 0.
        drawCommands->drawRanges[0].drawCommandsBegin = 0;
        drawCommands->drawRanges[0].drawCommandsCount = (ushort)drawCommnadsCount;

        // This example doesn't care about shadows or motion vectors, so it leaves everything
        // at the default zero values, except the renderingLayerMask which it sets to all ones
        // so Unity renders the instances regardless of mask settings.
        drawCommands->drawRanges[0].filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, };

        // Finally, write the actual visible instance indices to the array. In a more complicated
        // implementation, this output would depend on what is visible, but this example
        // assumes that everything is visible.
        for (int i = 0; i < spawnCount; ++i)
            drawCommands->visibleInstances[i] = i;
        
        // This example doesn't use jobs, so it can return an empty JobHandle.
        // Performance-sensitive applications should use Burst jobs to implement
        // culling and draw command output. In this case, this function would return a
        // handle here that completes when the Burst jobs finish.
        return new JobHandle();
    }
}

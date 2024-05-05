using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public class TestBRG : MonoBehaviour
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
    
        // BRG에 종속이라 직렬화하면 안됨. 장면이 바껴 강제로 asset이 나라가면 id도 무효해짐
        public BatchMeshID meshID;
        public BatchMaterialID[] materialIDs;
    }
    
    public MeshInfoForInstancing[] meshInfos;
    
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
    private const int kNumInstances = 3;

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

    private void Start()
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
        
        // instance 생성
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

            // Mesh, material은 managed C# 객체라 Burst C#에서 못씀. 그래서 BRG에서 사용하려면 등록해야 함.
            // BRG가 rendering에 사용되기 전이라면, Runtime에서도 추가할 수 있음
            // 필요없으면 unregist 해야함. 특히, unload하려면 꼭.
            // 내부적으로 참조횟수 관리, 참조 0되면 진짜 등록해제, 동일 asset은 동일 ID 반환.
            
            meshInfos[i].meshID = m_BRG.RegisterMesh(mesh);
            meshInfos[i].materialIDs = new BatchMaterialID[materials.Length];
            
            for (int j = 0; j < materials.Length; j++)
            {
                meshInfos[i].materialIDs[j] = m_BRG.RegisterMaterial(materials[j]);
                drawCommnadsCount++;
            }
        }

        AllocateInstanceDateBuffer();
        PopulateInstanceDataBuffer();
    }

    private void AllocateInstanceDateBuffer()
    {
        // kBytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4; (obj2world, world2obj, color)
        // https://docs.unity3d.com/ScriptReference/GraphicsBuffer-ctor.html
           // public GraphicsBuffer(GraphicsBuffer.Target target, int count, int stride);
           // float도 4byte일껀데 float으로 계산해도 동일할껀데..
        m_InstanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
            BufferCountForInstances(kBytesPerInstance, kNumInstances, kExtraBytes),
            sizeof(int)); // 112, 3,128, 4?
    }

    // Raw buffers are allocated in ints. This is a utility method that calculates
    // the required number of ints for the data.
    // bytesPerInstance나 extraBytes 값에 따라서 count가 정수로 안떨어질 수 있기 때문에 round(버림)처리를 한다.
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

    private void PopulateInstanceDataBuffer()
    {
        // Place a zero matrix at the start of the instance data buffer, so loads from address 0 return zero.
        var zero = new Matrix4x4[1] { Matrix4x4.zero };

        // Create transform matrices for three example instances.
        var matrices = new Matrix4x4[kNumInstances]
        {
            Matrix4x4.Translate(new Vector3(-2, 0, 0)),
            Matrix4x4.Translate(new Vector3(0, 0, 0)),
            Matrix4x4.Translate(new Vector3(2, 0, 0)),
        };

        // Convert the transform matrices into the packed format that the shader expects.
        var objectToWorld = new PackedMatrix[kNumInstances]
        {
            new PackedMatrix(matrices[0]),
            new PackedMatrix(matrices[1]),
            new PackedMatrix(matrices[2]),
        };

        // Also create packed inverse matrices.
        var worldToObject = new PackedMatrix[kNumInstances]
        {
            new PackedMatrix(matrices[0].inverse),
            new PackedMatrix(matrices[1].inverse),
            new PackedMatrix(matrices[2].inverse),
        };

        // Make all instances have unique colors.
        var colors = new Vector4[kNumInstances]
        {
            new Vector4(1, 0, 0, 1),
            new Vector4(0, 1, 0, 1),
            new Vector4(0, 0, 1, 1),
        };

        // GraphicBuffer에 접근할 주소 계산
        // In this simple example, the instance data is placed into the buffer like this:
        // Offset | Description
        //      0 | 64 bytes of zeroes, so loads from address 0 return zeroes (float4x4 matrix 64byte)
        //     64 | 32 uninitialized bytes to make working with SetData easier, otherwise unnecessary
        //     96 | unity_ObjectToWorld, three packed float3x4 matrices (48byte * 3개 = 144byte)
        //    240 | unity_WorldToObject, three packed float3x4 matrices
        //    384 | _BaseColor, three float4s (16byte)
        // Calculates start addresses for the different instanced properties. unity_ObjectToWorld starts
        // at address 96 instead of 64, because the computeBufferStartIndex parameter of SetData
        // is expressed as source array elements, so it is easier to work in multiples of sizeof(PackedMatrix).
        uint byteAddressObjectToWorld = kSizeOfPackedMatrix * 2;
        uint byteAddressWorldToObject = byteAddressObjectToWorld + kSizeOfPackedMatrix * kNumInstances;
        uint byteAddressColor = byteAddressWorldToObject + kSizeOfPackedMatrix * kNumInstances;

        // 드디어 GraphicBuffer에 instance data upload
        // Upload the instance data to the GraphicsBuffer so the shader can load them.
        // https://docs.unity3d.com/ScriptReference/GraphicsBuffer.SetData.html
        // public void SetData(Array data, int managedBufferStartIndex, int graphicsBufferStartIndex, int count);
           // graphicsBufferStartIndex: The first element index in the graphics buffer to receive the data.
        m_InstanceData.SetData(zero, 0, 0, 1);
           // graphicsBufferStartIndex는 byte가 아니고 입력 배열 요소의 크기 단위로 index를 넣어야함. 즉, objectToWorld의 byte index는 96이지만, packedmatrix 크기 48byte 단위로 보면 index가 2가됨.
        m_InstanceData.SetData(objectToWorld, 0, (int)(byteAddressObjectToWorld / kSizeOfPackedMatrix), objectToWorld.Length); // 96 / 48 = 2
        m_InstanceData.SetData(worldToObject, 0, (int)(byteAddressWorldToObject / kSizeOfPackedMatrix), worldToObject.Length); // 240 / 48 = 5
        m_InstanceData.SetData(colors, 0, (int)(byteAddressColor / kSizeOfFloat4), colors.Length); // 384 / 16 = 24

        // 드디어 metadata 생성
        // Set up metadata values to point to the instance data. Set the most significant bit 0x80000000 in each
        // which instructs the shader that the data is an array with one value per instance, indexed by the instance index.
        // Any metadata values that the shader uses that are not set here will be 0. When a value of 0 is used with
        // UNITY_ACCESS_DOTS_INSTANCED_PROP (i.e. without a default), the shader interprets the
        // 0x00000000 metadata value and loads from the start of the buffer. The start of the buffer is
        // a zero matrix so this sort of load is guaranteed to return zero, which is a reasonable default value.
/* 인스턴스 데이터를 가리키도록 메타데이터 값을 설정합니다. 각각에 가장 중요한 비트 0x80000000을 설정하여 셰이더에 데이터가 인스턴스당 하나의 값을 가진 배열이며 인스턴스 인덱스에 의해 인덱싱된다는 것을 알려줍니다.
셰이더가 사용하는 메타데이터 값 중 여기에 설정되지 않은 값은 모두 0이 됩니다. 
기본값이 없는 경우(즉, 기본값이 없는 경우) UNITY_ACCESS_DOTS_INSTANCED_PROP에 0 값을 사용하면 셰이더는 0x00000000 메타데이터 값을 해석하고 버퍼의 시작부터 로드합니다. 
버퍼의 시작은 0 행렬이므로 이러한 종류의 로드는 합리적인 기본값인 0을 반환하도록 보장됩니다. */
        var metadata = new NativeArray<MetadataValue>(3, Allocator.Temp);
        metadata[0] = new MetadataValue { NameID = Shader.PropertyToID("unity_ObjectToWorld"), Value = 0x80000000 | byteAddressObjectToWorld, };
        metadata[1] = new MetadataValue { NameID = Shader.PropertyToID("unity_WorldToObject"), Value = 0x80000000 | byteAddressWorldToObject, };
        metadata[2] = new MetadataValue { NameID = Shader.PropertyToID("_BaseColor"), Value = 0x80000000 | byteAddressColor, };

        // Batch 생성
        // Finally, create a batch for the instances and make the batch use the GraphicsBuffer with the
        // instance data as well as the metadata values that specify where the properties are.
        m_BatchID = m_BRG.AddBatch(metadata, m_InstanceData.bufferHandle);
    }

    private void OnDisable()
    {
        // 등록된 mesh, material들을 자동으로 등록 해제함
        m_BRG.Dispose();
        m_InstanceData.Dispose();
    }

    // OnPerformCulling 구현, BRG의 main entry point, Culling할 때마다 호출
    public unsafe JobHandle OnPerformCulling(
        BatchRendererGroup rendererGroup,
        BatchCullingContext cullingContext,
        BatchCullingOutput cullingOutput,
        IntPtr userContext)
    {
        // BatchCullingContext을 기반으로 가시성 culling 해야하고
        // 실제 draw command를 생성하도록 BatchCullingOutput을 설정해야 함

        // 여기서 수정된 mesh, material은 rendering에 반영되지만, job scheduled된 변형은 반영안되고 동작 이상해짐.
        
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
        drawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(kNumInstances * sizeof(int), alignment, Allocator.TempJob);

        drawCommands->drawCommandPickingInstanceIDs = null;
        drawCommands->drawCommandCount = drawCommnadsCount;
        drawCommands->drawRangeCount = 1;
        drawCommands->visibleInstanceCount = kNumInstances;

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
                drawCommands->drawCommands[drawCommandIndex].visibleCount = kNumInstances;
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
        for (int i = 0; i < kNumInstances; ++i)
            drawCommands->visibleInstances[i] = i;
        
        // This example doesn't use jobs, so it can return an empty JobHandle.
        // Performance-sensitive applications should use Burst jobs to implement
        // culling and draw command output. In this case, this function would return a
        // handle here that completes when the Burst jobs finish.
        return new JobHandle();
    }
}

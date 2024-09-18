using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using BRGUtils;
using Random = UnityEngine.Random;

public class BulletShooterBRG : MonoBehaviour
{
    public float fireRate = 0.2f; // 총알 발사 속도 (초당 발사 횟수)
    public float angleRange = 60f; // 총알이 발사되는 각도 범위
    public float pingPongSpeed = 2f; // Ping Pong 속도
    public int spawnCount = 1000;
    
    [SerializeField, ReadOnly]
    int bulletCount = 0;

    public Material bulletMaterial;


    public ComputeShader bulletTransformComputeShader;
    int m_KernelIndex;
    const string kComputeShaderName = "BulletTransformForBRG";
    int m_KernelIndex_InitBullets;
    const string kComputeShaderName_InitBullets = "BulletTransformForBRG_InitBullets";
    int m_KernelIndex_GetVisibleInstanceCount;
    const string kComputeShaderName_GetVisibleInstanceCount = "BulletTransformForBRG_GetVisibleInstanceCount";
    
    // For BRG
    BatchRendererGroup m_BRG;
    
    GraphicsBuffer m_InstanceData;
    BatchID m_BatchID;

    BatchMeshID m_BatchMeshID;
    BatchMaterialID m_BatchMaterialID;

    // instance visible flags
    int[] m_InstanceVisible;
    ComputeBuffer m_InstanceVisibleBuffer;
    int[] m_InstanceVisibleCount;
    ComputeBuffer m_InstanceVisibleCountBuffer;

    // bullet data
    struct Bullet
    {
        public Vector3 direction;
        public float accumulatedDistance;

        public static int Size()
        {
            return sizeof(float) * 3 + sizeof(float);
        }
    }

    ComputeBuffer m_BulletsBuffer;

    // fire vectors
    float fireTimer = 0f;
    bool isFiring = false;
    const int kFireVectorsMax = 50;
    int m_FireVectorsCount = 0;
    Vector3[] m_FireVectors;
    ComputeBuffer m_FireVectorsBuffer;


    // Some helper constants to make calculations more convenient.
    const int kBytesPerInstance = (Size.kSizeOfPackedMatrix * 2);	// 96
    const int kExtraBytes = Size.kSizeOfMatrix * 2;		// 128
    
    void Start()
    {
        m_BRG = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);

        var mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0, -1f),
            new Vector3(0.5f, 0, -1f),
            new Vector3(-0.5f, 0, 0),
            new Vector3(0.5f, 0, 0)
        };
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(0, 0),
            new Vector2(1, 0)
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        m_BatchMeshID = m_BRG.RegisterMesh(mesh);
        m_BatchMaterialID = m_BRG.RegisterMaterial(bulletMaterial);
        
        m_KernelIndex = bulletTransformComputeShader.FindKernel(kComputeShaderName);
        m_KernelIndex_InitBullets = bulletTransformComputeShader.FindKernel(kComputeShaderName_InitBullets);
        m_KernelIndex_GetVisibleInstanceCount = bulletTransformComputeShader.FindKernel(kComputeShaderName_GetVisibleInstanceCount);
        
        bulletTransformComputeShader.SetFloat("_BulletSpeed", 5f);
        
        PopulateInstanceData();
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            isFiring = true;
            fireTimer = 0f;
        }

        if (Input.GetButtonUp("Fire1"))
        {
            isFiring = false;
        }

        if (isFiring)
        {
            fireTimer += Time.deltaTime;

            if (fireTimer >= fireRate)
            {
                fireTimer = FireBullet(fireTimer);
            }
        }

        bulletTransformComputeShader.SetFloat("_TimeDelta", Time.deltaTime);
        bulletTransformComputeShader.SetVector("_PlayerPosition", transform.position);

        // Update fire
        bulletTransformComputeShader.SetInt("_FireVectorCount", m_FireVectorsCount);
        if (m_FireVectorsCount > 0)
        {
            m_FireVectorsBuffer.SetData(m_FireVectors);
            bulletTransformComputeShader.SetBuffer(m_KernelIndex_InitBullets, "_FireVectors", m_FireVectorsBuffer);
            bulletTransformComputeShader.Dispatch(m_KernelIndex_InitBullets, 1, 1, 1);
            m_FireVectorsCount = 0;
        }

        // Update bullets
        bulletTransformComputeShader.Dispatch(m_KernelIndex, Mathf.CeilToInt(spawnCount / 64f), 1, 1);

        // Get visible instance count from compute shader
        bulletTransformComputeShader.Dispatch(m_KernelIndex_GetVisibleInstanceCount, 1, 1, 1);
        m_InstanceVisibleCountBuffer.GetData(m_InstanceVisibleCount);
        bulletCount = m_InstanceVisibleCount[0];


        // test
        m_InstanceVisibleBuffer.GetData(m_InstanceVisible);
    }

    float FireBullet(float fireTimer)
    {
        m_FireVectorsCount = Mathf.Min(Mathf.FloorToInt(fireTimer / fireRate), kFireVectorsMax);
        float remainTime = fireTimer % fireRate;
        float currentTime = Time.time;

        for (int i = 0; i < m_FireVectorsCount; i++)
        {
            float bulletTime = currentTime - (m_FireVectorsCount - 1 - i) * fireRate;
            float oscillationTime = bulletTime * pingPongSpeed;
            float angle = Mathf.PingPong(oscillationTime, angleRange) - (angleRange / 2f);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            m_FireVectors[i] = direction;
        }

        return remainTime;
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
    
    void PopulateInstanceData()
    {
        bulletTransformComputeShader.SetInt("_InstanceCount", spawnCount);

        // Initialize _FireVectors
        m_FireVectors = new Vector3[kFireVectorsMax];
        for (int i = 0; i < kFireVectorsMax; i++)
        {
            m_FireVectors[i] = Vector3.zero;
        }

        m_FireVectorsBuffer = new ComputeBuffer(kFireVectorsMax, sizeof(float) * 3);
        m_FireVectorsBuffer.SetData(m_FireVectors);
        bulletTransformComputeShader.SetBuffer(m_KernelIndex_InitBullets, "_FireVectors", m_FireVectorsBuffer);


        // Initialize _Bullets
        var bullets = new Bullet[spawnCount];
        for (int i = 0; i < spawnCount; i++)
        {
            bullets[i] = new Bullet();
        }

        m_BulletsBuffer = new ComputeBuffer(spawnCount, Bullet.Size());
        m_BulletsBuffer.SetData(bullets);
        bulletTransformComputeShader.SetBuffer(m_KernelIndex, "_Bullets", m_BulletsBuffer);
        bulletTransformComputeShader.SetBuffer(m_KernelIndex_InitBullets, "_Bullets", m_BulletsBuffer);


        // Initialize _VisibleFlags
        var instanceVisibles = new int[spawnCount];
        for (int i = 0; i < spawnCount; i++)
        {
            instanceVisibles[i] = 0;
        }

        m_InstanceVisible = new int[spawnCount];

        m_InstanceVisibleBuffer = new ComputeBuffer(spawnCount, sizeof(int));
        m_InstanceVisibleBuffer.SetData(instanceVisibles);
        bulletTransformComputeShader.SetBuffer(m_KernelIndex, "_VisibleFlags", m_InstanceVisibleBuffer);
        bulletTransformComputeShader.SetBuffer(m_KernelIndex_InitBullets, "_VisibleFlags", m_InstanceVisibleBuffer);
        bulletTransformComputeShader.SetBuffer(m_KernelIndex_GetVisibleInstanceCount, "_VisibleFlags", m_InstanceVisibleBuffer);
        bulletMaterial.SetBuffer("_VisibleFlags", m_InstanceVisibleBuffer);
        bulletMaterial.SetInt("_VisibleFlagsCount", spawnCount);

        // Initialize _VisibleCount
        m_InstanceVisibleCountBuffer = new ComputeBuffer(1, sizeof(int));
        m_InstanceVisibleCount = new int[1] { 0 };
        m_InstanceVisibleCountBuffer.SetData(m_InstanceVisibleCount);
        bulletTransformComputeShader.SetBuffer(m_KernelIndex_GetVisibleInstanceCount, "_VisibleCount", m_InstanceVisibleCountBuffer);


        // Place a zero matrix at the start of the instance data buffer, so loads from address 0 return zero.
        var zero = new Matrix4x4[1] { Matrix4x4.zero };

        //var matrices = new Matrix4x4[spawnCount];
        var objectToWorld = new PackedMatrix[spawnCount];
        var worldToObject = new PackedMatrix[spawnCount];

        for (int i = 0; i < spawnCount; i++)
        {
            var matrix = Matrix4x4.TRS(new Vector3(Random.value, 0, Random.value * 10f), Quaternion.identity, Vector3.one);
                
            // Convert the transform matrices into the packed format that the shader expects.
            objectToWorld[i] = new PackedMatrix(matrix);
            // Also create packed inverse matrices.
            worldToObject[i] = new PackedMatrix(matrix.inverse);
            
            // // Convert the transform matrices into the packed format that the shader expects.
            // objectToWorld[i] = new PackedMatrix(Matrix4x4.identity);
            // // Also create packed inverse matrices.
            // worldToObject[i] = new PackedMatrix(Matrix4x4.identity.inverse);
        }
        
        // In this simple example, the instance data is placed into the buffer like this:
        // Offset | Description
        //      0 | 64 bytes of zeroes, so loads from address 0 return zeroes (float4x4 matrix 64byte)
        //     64 | 32 uninitialized bytes to make working with SetData easier, otherwise unnecessary
        //     96 | unity_ObjectToWorld, three packed float3x4 matrices (48byte * 3 = 144byte)
        //    240 | unity_WorldToObject, three packed float3x4 matrices
        // Calculates start addresses for the different instanced properties. unity_ObjectToWorld starts
        // at address 96 instead of 64, because the computeBufferStartIndex parameter of SetData
        // is expressed as source array elements, so it is easier to work in multiples of sizeof(PackedMatrix).
        uint byteAddressObjectToWorld = Size.kSizeOfPackedMatrix * 2;
        uint byteAddressWorldToObject = byteAddressObjectToWorld + Size.kSizeOfPackedMatrix * (uint)spawnCount;
        
        // kBytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4; (obj2world, world2obj, color)
        // https://docs.unity3d.com/ScriptReference/GraphicsBuffer-ctor.html
        // public GraphicsBuffer(GraphicsBuffer.Target target, int count, int stride);
        m_InstanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
            BufferCountForInstances(kBytesPerInstance, spawnCount, kExtraBytes),
            sizeof(int)); // 112, 3,128, 4?
        
        // Upload the instance data to the GraphicsBuffer so the shader can load them.
        // https://docs.unity3d.com/ScriptReference/GraphicsBuffer.SetData.html
        // public void SetData(Array data, int managedBufferStartIndex, int graphicsBufferStartIndex, int count);
           // graphicsBufferStartIndex: The first element index in the graphics buffer to receive the data.
        m_InstanceData.SetData(zero, 0, 0, 1);
        m_InstanceData.SetData(objectToWorld, 0, (int)(byteAddressObjectToWorld / Size.kSizeOfPackedMatrix), objectToWorld.Length); // 96 / 48 = 2
        m_InstanceData.SetData(worldToObject, 0, (int)(byteAddressWorldToObject / Size.kSizeOfPackedMatrix), worldToObject.Length); // 240 / 48 = 5
        
        bulletTransformComputeShader.SetBuffer(m_KernelIndex, "_InstanceData", m_InstanceData);
        bulletTransformComputeShader.SetBuffer(m_KernelIndex_InitBullets, "_InstanceData", m_InstanceData);

        // Set up metadata values to point to the instance data. Set the most significant bit 0x80000000 in each
        // which instructs the shader that the data is an array with one value per instance, indexed by the instance index.
        // Any metadata values that the shader uses that are not set here will be 0. When a value of 0 is used with
        // UNITY_ACCESS_DOTS_INSTANCED_PROP (i.e. without a default), the shader interprets the
        // 0x00000000 metadata value and loads from the start of the buffer. The start of the buffer is
        // a zero matrix so this sort of load is guaranteed to return zero, which is a reasonable default value.
        var metadata = new NativeArray<MetadataValue>(2, Allocator.Temp);
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
        
        // Finally, create a batch for the instances and make the batch use the GraphicsBuffer with the
        // instance data as well as the metadata values that specify where the properties are.
        m_BatchID = m_BRG.AddBatch(metadata, m_InstanceData.bufferHandle);
    }

    private void OnDisable()
    {
        m_BRG.Dispose();
        m_InstanceData.Dispose();

        if (m_BulletsBuffer != null)
        {
            m_BulletsBuffer.Release();
            m_BulletsBuffer = null;
        }

        if (m_InstanceVisibleBuffer != null)
        {
            m_InstanceVisibleBuffer.Release();
            m_InstanceVisibleBuffer = null;
        }

        if (m_InstanceVisibleCountBuffer != null)
        {
            m_InstanceVisibleCountBuffer.Release();
            m_InstanceVisibleCountBuffer = null;
        }

        if (m_FireVectorsBuffer != null)
        {
            m_FireVectorsBuffer.Release();
            m_FireVectorsBuffer = null;
        }
    }

    unsafe JobHandle OnPerformCulling(
        BatchRendererGroup rendererGroup,
        BatchCullingContext cullingContext,
        BatchCullingOutput cullingOutput,
        IntPtr userContext)
    {
        var visibleInstanceCount = m_InstanceVisibleCount[0];

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
        drawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>(), alignment, Allocator.TempJob);
        drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(), alignment, Allocator.TempJob);
        
        drawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(visibleInstanceCount * sizeof(int), alignment, Allocator.TempJob);
        drawCommands->visibleInstanceCount = visibleInstanceCount;

        drawCommands->drawCommandPickingInstanceIDs = null;
        drawCommands->drawCommandCount = 1;
        drawCommands->drawRangeCount = 1;

        // This example doens't use depth sorting, so it leaves instanceSortingPositions as null.
        drawCommands->instanceSortingPositions = null;
        drawCommands->instanceSortingPositionFloatCount = 0;

        // Configure the single draw command to draw kNumInstances instances
        // starting from offset 0 in the array, using the batch, material and mesh
        // IDs registered in the Start() method. It doesn't set any special flags.
        drawCommands->drawCommands[0].visibleOffset = 0;
        drawCommands->drawCommands[0].visibleCount = (uint)visibleInstanceCount;
        drawCommands->drawCommands[0].batchID = m_BatchID;
        drawCommands->drawCommands[0].materialID = m_BatchMaterialID;
        drawCommands->drawCommands[0].meshID = m_BatchMeshID;
        drawCommands->drawCommands[0].submeshIndex = (ushort)0;
        drawCommands->drawCommands[0].splitVisibilityMask = 0xff;
        drawCommands->drawCommands[0].flags = 0;
        drawCommands->drawCommands[0].sortingPosition = 0;
        
        // Configure the single draw range to cover the single draw command which
        // is at offset 0.
        drawCommands->drawRanges[0].drawCommandsBegin = 0;
        drawCommands->drawRanges[0].drawCommandsCount = (ushort)1;

        // This example doesn't care about shadows or motion vectors, so it leaves everything
        // at the default zero values, except the renderingLayerMask which it sets to all ones
        // so Unity renders the instances regardless of mask settings.
        drawCommands->drawRanges[0].filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, };

        // Finally, write the actual visible instance indices to the array. In a more complicated
        // implementation, this output would depend on what is visible, but this example
        // assumes that everything is visible.

        // Simple traversal to extract visible index from visibleFlog
        // for (int i = 0; i < visibleInstanceCount; i++)
        // {
        //     drawCommands->visibleInstances[i] = i;
        // }

        // test
        for (int i = 0, index = 0; i < spawnCount; i++)
        {
            if (m_InstanceVisible[i] > 0)
            {
                drawCommands->visibleInstances[index++] = i;
            }
        }
        
        // This example doesn't use jobs, so it can return an empty JobHandle.
        // Performance-sensitive applications should use Burst jobs to implement
        // culling and draw command output. In this case, this function would return a
        // handle here that completes when the Burst jobs finish.
        return new JobHandle();
    }
}
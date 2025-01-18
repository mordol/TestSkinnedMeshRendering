using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public unsafe class DrawCommandTypesTest: MonoBehaviour
{
    public Mesh mesh;
    public Material regularMaterial;
    public ComputeShader computeShader;

    private int m_KernelIndex;
    const string kComputeShaderName = "CSMain";
    private int visibleCount;

    private BatchRendererGroup _batchRendererGroup;
    private GraphicsBuffer _gpuPersistentInstanceData;
    private GraphicsBuffer _gpuVisibleInstances;
    private GraphicsBuffer _gpuIndexedIndirectBuffer;
    private uint _gpuVisibleInstancesWindow;

    private uint _elementsPerDraw;

    private BatchID _batchID;
    private BatchMaterialID _regularMaterialID;
    private BatchMeshID _meshID;
    private int _itemCount;
    private bool _initialized;

    private NativeArray<Vector4> _sysmemBuffer;
    private NativeArray<int> _sysmemVisibleInstances;

    private bool UseConstantBuffer => BatchRendererGroup.BufferTarget == BatchBufferTarget.ConstantBuffer;

    public static T* Malloc<T>(int count) where T : unmanaged
    {
        return (T*)UnsafeUtility.Malloc(
            UnsafeUtility.SizeOf<T>() * count,
            UnsafeUtility.AlignOf<T>(),
            Allocator.TempJob);
    }

    static MetadataValue CreateMetadataValue(int nameID, int gpuAddress, bool isOverridden)
    {
        const uint kIsOverriddenBit = 0x80000000;
        return new MetadataValue
        {
            NameID = nameID,
            Value = (uint)gpuAddress | (isOverridden ? kIsOverriddenBit : 0),
        };
    }

    public JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
    {
        if (!_initialized)
        {
            return new JobHandle();
        }

        BatchCullingOutputDrawCommands drawCommands = new BatchCullingOutputDrawCommands();

        var filterSettings = new BatchFilterSettings
        {
            renderingLayerMask = 1,
            layer = 0,
            motionMode = MotionVectorGenerationMode.ForceNoMotion,
            shadowCastingMode = ShadowCastingMode.On,
            receiveShadows = true,
            staticShadowCaster = false,
            allDepthSorted = false
        };
        drawCommands.drawRangeCount = 1;
        drawCommands.drawRanges = Malloc<BatchDrawRange>(1);

        // Indirect draw range
        drawCommands.drawRanges[0] = new BatchDrawRange
        {
            drawCommandsType = BatchDrawCommandType.Indirect,
            drawCommandsBegin = 0,
            //drawCommandsCount = 5,
            drawCommandsCount = 1,
            filterSettings = filterSettings,
        };

        drawCommands.visibleInstances = Malloc<int>(_itemCount);
        UnsafeUtility.MemCpy(drawCommands.visibleInstances, _sysmemVisibleInstances.GetUnsafePtr(), _itemCount * sizeof(int));

        drawCommands.visibleInstanceCount = _itemCount;

        // Indirect draw command
        // drawCommands.indirectDrawCommandCount = 5;
        // drawCommands.indirectDrawCommands = Malloc<BatchDrawCommandIndirect>(5);
        // for (uint i = 0; i < 5; ++i)
        // {
        //     drawCommands.indirectDrawCommands[i] = new BatchDrawCommandIndirect
        //     {
        //         flags = BatchDrawCommandFlags.None,
        //         batchID = _batchID,
        //         materialID = _regularMaterialID,
        //         splitVisibilityMask = 0xff,
        //         sortingPosition = 0,
        //         visibleOffset = i * 2,
        //         meshID = _meshID,
        //         topology = MeshTopology.Triangles,

        //         visibleInstancesBufferHandle = _gpuVisibleInstances.bufferHandle,
        //         visibleInstancesBufferWindowOffset = 0,
        //         visibleInstancesBufferWindowSizeBytes = _gpuVisibleInstancesWindow,

        //         indirectArgsBufferHandle = _gpuIndexedIndirectBuffer.bufferHandle,
        //         indirectArgsBufferOffset = i * GraphicsBuffer.IndirectDrawIndexedArgs.size,
        //     };
        // }
        drawCommands.indirectDrawCommandCount = 1;
        drawCommands.indirectDrawCommands = Malloc<BatchDrawCommandIndirect>(1);
        for (uint i = 0; i < 1; ++i)
        {
            drawCommands.indirectDrawCommands[i] = new BatchDrawCommandIndirect
            {
                flags = BatchDrawCommandFlags.None,
                batchID = _batchID,
                materialID = _regularMaterialID,
                splitVisibilityMask = 0xff,
                sortingPosition = 0,
                visibleOffset = i * 10,
                meshID = _meshID,
                topology = MeshTopology.Triangles,

                visibleInstancesBufferHandle = _gpuVisibleInstances.bufferHandle,
                visibleInstancesBufferWindowOffset = 0,
                visibleInstancesBufferWindowSizeBytes = _gpuVisibleInstancesWindow,

                indirectArgsBufferHandle = _gpuIndexedIndirectBuffer.bufferHandle,
                indirectArgsBufferOffset = i * GraphicsBuffer.IndirectDrawIndexedArgs.size,
            };
        }

        drawCommands.instanceSortingPositions = null;
        drawCommands.instanceSortingPositionFloatCount = 0;

        cullingOutput.drawCommands[0] = drawCommands;
        return new JobHandle();
    }


    // Start is called before the first frame update
    void Start()
    {
        uint kBRGBufferMaxWindowSize = 128 * 1024 * 1024;
        uint kBRGBufferAlignment = 16;
        if (UseConstantBuffer)
        {
            kBRGBufferMaxWindowSize = (uint)(BatchRendererGroup.GetConstantBufferMaxWindowSize());
            kBRGBufferAlignment = (uint)(BatchRendererGroup.GetConstantBufferOffsetAlignment());
        }

        _batchRendererGroup = new BatchRendererGroup(this.OnPerformCulling, IntPtr.Zero);

        _itemCount = 10;
        visibleCount = _itemCount;

        // Compute shader
        m_KernelIndex = computeShader.FindKernel(kComputeShaderName);

        // Bounds
        Bounds bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(1048576.0f, 1048576.0f, 1048576.0f));
        _batchRendererGroup.SetGlobalBounds(bounds);

        // Register mesh and material
        if (mesh) _meshID = _batchRendererGroup.RegisterMesh(mesh);
        if (regularMaterial) _regularMaterialID = _batchRendererGroup.RegisterMaterial(regularMaterial);

        // Batch metadata buffer
        int objectToWorldID = Shader.PropertyToID("unity_ObjectToWorld");
        int matrixPreviousID = Shader.PropertyToID("unity_MatrixPreviousM");
        int worldToObjectID = Shader.PropertyToID("unity_WorldToObject");
        int colorID = Shader.PropertyToID("_BaseColor");
        int positionsID = Shader.PropertyToID("_Positions");
        int normalsID = Shader.PropertyToID("_Normals");
        int tangentsID = Shader.PropertyToID("_Tangents");
        int baseIndexID = Shader.PropertyToID("_BaseIndex");

        // Generate a grid of objects...
        int bigDataBufferVector4Count = 4 + _itemCount * (3 * 3 + 1);      // 4xfloat4 zero + per instance = { 3x mat4x3, 1x float4 color }
        uint brgWindowSize = 0;
        _sysmemBuffer = new NativeArray<Vector4>(bigDataBufferVector4Count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        if (UseConstantBuffer)
        {
            _gpuPersistentInstanceData = new GraphicsBuffer(GraphicsBuffer.Target.Constant, (int)bigDataBufferVector4Count * 16 / (4 * 4), 4 * 4);
            brgWindowSize = (uint)bigDataBufferVector4Count * 16;
        }
        else
        {
            _gpuPersistentInstanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw, (int)bigDataBufferVector4Count * 16 / 4, 4);
        }

        // 64 bytes of zeroes, so loads from address 0 return zeroes. This is a BatchRendererGroup convention.
        int positionOffset = 4;
        _sysmemBuffer[0] = new Vector4(0, 0, 0, 0);
        _sysmemBuffer[1] = new Vector4(0, 0, 0, 0);
        _sysmemBuffer[2] = new Vector4(0, 0, 0, 0);
        _sysmemBuffer[3] = new Vector4(0, 0, 0, 0);

        // Matrices
        var itemCountOffset = _itemCount * 3; // one packed matrix
        for (int i = 0; i < _itemCount; ++i)
        {
            /*
             *  mat4x3 packed like this:
             *
                    float4x4(
                            p1.x, p1.w, p2.z, p3.y,
                            p1.y, p2.x, p2.w, p3.z,
                            p1.z, p2.y, p3.x, p3.w,
                            0.0, 0.0, 0.0, 1.0
                        );
            */

            float px = (i % 10) * 2.0f;
            float py = -(i / 10) * 2.0f;
            float pz = 0.0f;

            // compute the new current frame matrix
            _sysmemBuffer[positionOffset + i * 3 + 0] = new Vector4(1, 0, 0, 0);
            _sysmemBuffer[positionOffset + i * 3 + 1] = new Vector4(1, 0, 0, 0);
            _sysmemBuffer[positionOffset + i * 3 + 2] = new Vector4(1, px, py, pz);

            // we set the same matrix for the previous matrix
            _sysmemBuffer[positionOffset + i * 3 + 0 + itemCountOffset] = _sysmemBuffer[positionOffset + i * 3 + 0];
            _sysmemBuffer[positionOffset + i * 3 + 1 + itemCountOffset] = _sysmemBuffer[positionOffset + i * 3 + 1];
            _sysmemBuffer[positionOffset + i * 3 + 2 + itemCountOffset] = _sysmemBuffer[positionOffset + i * 3 + 2];

            // compute the new inverse matrix
            _sysmemBuffer[positionOffset + i * 3 + 0 + itemCountOffset * 2] = new Vector4(1, 0, 0, 0);
            _sysmemBuffer[positionOffset + i * 3 + 1 + itemCountOffset * 2] = new Vector4(1, 0, 0, 0);
            _sysmemBuffer[positionOffset + i * 3 + 2 + itemCountOffset * 2] = new Vector4(1, -px, -py, -pz);
        }

        // Colors
        int colorOffset = positionOffset + itemCountOffset * 3;
        for (int i = 0; i < _itemCount; i++)
        {
            // write colors right after the 4x3 matrices
            _sysmemBuffer[colorOffset + i] = new Vector4((float)i / (float)_itemCount, 0f, 0f, 1.0f);
        }
        _gpuPersistentInstanceData.SetData(_sysmemBuffer);

        // GPU side visible instances
        _sysmemVisibleInstances = new NativeArray<int>(_itemCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        if (UseConstantBuffer)
        {
            _gpuVisibleInstances = new GraphicsBuffer(GraphicsBuffer.Target.Constant, sizeof(int) * _itemCount / 4, sizeof(int) * 4);
            _gpuVisibleInstancesWindow = (uint)(sizeof(int) * _itemCount);
        }
        else
        {
            _gpuVisibleInstances = new GraphicsBuffer(GraphicsBuffer.Target.Raw, sizeof(int) * _itemCount, sizeof(int));
        }
        computeShader.SetBuffer(m_KernelIndex, "_VisibleInstances", _gpuVisibleInstances);

        for (int i = 0; i < _itemCount; ++i)
        {
            _sysmemVisibleInstances[i] = i;
        }
        _gpuVisibleInstances.SetData(_sysmemVisibleInstances);

        //_elementsPerDraw = (uint)indices.Length;
        _elementsPerDraw = mesh.GetIndexCount(0);

        // Indexed Indirect buffer
        // _gpuIndexedIndirectBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        // var indexedIndirectData = new GraphicsBuffer.IndirectDrawIndexedArgs[5];
        // for (uint i = 0; i < 5; ++i)
        // {
        //     indexedIndirectData[i] = new GraphicsBuffer.IndirectDrawIndexedArgs
        //     {
        //         indexCountPerInstance = _elementsPerDraw,
        //         instanceCount = 2,
        //         startIndex = 0,
        //         baseVertexIndex = 0,
        //         startInstance = 0,
        //     };
        // }
        _gpuIndexedIndirectBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        var indexedIndirectData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        for (uint i = 0; i < 1; ++i)
        {
            indexedIndirectData[i] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = _elementsPerDraw,
                instanceCount = 10,
                startIndex = 0,
                baseVertexIndex = 0,
                startInstance = 0,
            };
        }

        _gpuIndexedIndirectBuffer.SetData(indexedIndirectData);


        var batchMetadata = new NativeArray<MetadataValue>(4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        batchMetadata[0] = CreateMetadataValue(objectToWorldID, 64, true);       // matrices
        batchMetadata[1] = CreateMetadataValue(matrixPreviousID, 64 + _itemCount * UnsafeUtility.SizeOf<Vector4>() * 3, true); // previous matrices
        batchMetadata[2] = CreateMetadataValue(worldToObjectID, 64 + _itemCount * UnsafeUtility.SizeOf<Vector4>() * 3 * 2, true); // inverse matrices
        batchMetadata[3] = CreateMetadataValue(colorID, 64 + _itemCount * UnsafeUtility.SizeOf<Vector4>() * 3 * 3, true); // colors

        // Register batch
        _batchID = _batchRendererGroup.AddBatch(batchMetadata, _gpuPersistentInstanceData.bufferHandle, 0, brgWindowSize);

        _initialized = true;
    }

    void Update()
    {
        bool isChanged = false;

        if (Input.GetKey(KeyCode.Alpha0))
        {
            visibleCount = 0;
            isChanged = true;
        }
        else if (Input.GetKey(KeyCode.Alpha1))
        {
            visibleCount = 1;
            isChanged = true;
        }
        else if (Input.GetKey(KeyCode.Alpha2))
        {
            visibleCount = 2;
            isChanged = true;
        }
        else if (Input.GetKey(KeyCode.Alpha3))
        {
            visibleCount = 3;
            isChanged = true;
        }
        else if (Input.GetKey(KeyCode.Alpha4))
        {
            visibleCount = 4;
            isChanged = true;
        }
        else if (Input.GetKey(KeyCode.Alpha5))
        {
            visibleCount = 5;
            isChanged = true;
        }
        else if (Input.GetKey(KeyCode.Alpha6))
        {
            visibleCount = 6;
            isChanged = true;
        }
        else if (Input.GetKey(KeyCode.Alpha7))
        {
            visibleCount = 7;
            isChanged = true;
        }
        else if (Input.GetKey(KeyCode.Alpha8))
        {
            visibleCount = 8;
            isChanged = true;
        }
        else if (Input.GetKey(KeyCode.Alpha9))
        {
            visibleCount = 9;
            isChanged = true;
        }

        if (isChanged)  
        {
            computeShader.SetInt("_VisibleCount", visibleCount);
            computeShader.Dispatch(m_KernelIndex, 1, 1, 1);
        }
    }

    private void OnDestroy()
    {
        if (_initialized)
        {
            _batchRendererGroup.RemoveBatch(_batchID);
            if (regularMaterial) _batchRendererGroup.UnregisterMaterial(_regularMaterialID);
            if (mesh) _batchRendererGroup.UnregisterMesh(_meshID);

            _batchRendererGroup.Dispose();
            _gpuPersistentInstanceData.Dispose();
            _gpuVisibleInstances.Dispose();
            _gpuIndexedIndirectBuffer.Dispose();

            _sysmemBuffer.Dispose();
            _sysmemVisibleInstances.Dispose();
        }
    }
}
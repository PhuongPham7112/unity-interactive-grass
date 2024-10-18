using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GrassModel : MonoBehaviour
{
    #region GRASS_PHYSICS_PARAMS
    private int physicsKernelIndex;
    private int cullingKernelIndex;
    private int argsKernelIndex;

    private float[] forceData;
    private int[] visibilityData;
    private uint[] visibilityCounterData;
    private Vector4[] collidersData; // colliders position + extent
    private Vector4[] grassV1Positions; // v1 xyz + grass height
    private Vector4[] grassV2Positions; // v2 xyz + grass width
    private Matrix4x4[] grassModelMatrices;

    private ComputeBuffer forceBuffer;
    private ComputeBuffer collidersBuffer;
    private ComputeBuffer grass1PosBuffer;
    private ComputeBuffer grass2PosBuffer;
    private ComputeBuffer grassMatrixBuffer;
    private ComputeBuffer visibleIndexGrassBuffer;
    private ComputeBuffer visibleGrassCounterBuffer;

    [SerializeField] private GameObject grassPrefab;
    [SerializeField] private Material grassMaterial;
    [SerializeField] private ComputeShader grassPhysicsCS;
    [SerializeField] private ComputeShader grassCullingCS;
    [SerializeField] private ComputeShader grassIndirectArgsCS;
    [SerializeField] public SphereCollider[] colliders;
    
    [SerializeField] public float collisionDecreaseAmount = 0.5f;
    [SerializeField] public float stiffnessCoefficient = 0.1f;
    [SerializeField] public float grassMass = 0.5f;
    [SerializeField] public float grassWidth = 1.0f;
    [SerializeField] public float grassHeight = 1.0f;
    [SerializeField] private int numPoints;
    [SerializeField] private int numColliders;
    private MaterialPropertyBlock propertyBlock;
    #endregion

    #region GRASS_CULLING_PARAMS
    RenderParams rp;
    Mesh grassMesh;
    GraphicsBuffer commandBuf;
    GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;
    const int commandCount = 1;
    #endregion


    // Start is called before the first frame update
    void Start()
    {
        physicsKernelIndex = grassPhysicsCS.FindKernel("CSMain");
        cullingKernelIndex = grassCullingCS.FindKernel("CSMain");
        argsKernelIndex = grassIndirectArgsCS.FindKernel("CSMain");

        grassMesh = grassPrefab.GetComponent<MeshFilter>().sharedMesh;
        rp = new RenderParams(grassMaterial)
        {
            worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one) // use tighter bounds for better FOV culling
        };
        propertyBlock = new MaterialPropertyBlock();

        // Get number of child objects
        numPoints = gameObject.transform.childCount;
        numColliders = colliders.Length;

        #region SIMULATION_SETUP
        // Fill the buffer with the v2 positions of the child objects
        visibilityCounterData = new uint[1];
        visibilityCounterData[0] = (uint)numPoints;
        visibilityData = new int[numPoints];

        forceData = new float[numPoints]; // Fill the buffer with force data
        grassV1Positions = new Vector4[numPoints];
        grassV2Positions = new Vector4[numPoints];
        grassModelMatrices = new Matrix4x4[numPoints]; 
        
        // Fill the buffers
        for (int i = 0; i < numPoints; i++)
        {
            forceData[i] = 0.0f;
            visibilityData[i] = i;
            grassModelMatrices[i] = gameObject.transform.GetChild(i).localToWorldMatrix;
            grassV1Positions[i] = new Vector4(0, grassHeight * 0.5f, Mathf.Epsilon, grassHeight);
            grassV2Positions[i] = new Vector4(0, grassHeight, Mathf.Epsilon, grassWidth);
        }

        // Fill the sphere colliders data
        collidersData = new Vector4[numColliders];
        for (int i = 0; i < numColliders; i++)
        {
            collidersData[i] = new Vector4(colliders[i].transform.position.x,
                colliders[i].transform.position.y,
                colliders[i].transform.position.z,
                colliders[i].radius * colliders[i].transform.localScale.x);
        }

        // Setup buffers
        forceBuffer = new ComputeBuffer(numPoints, sizeof(float));
        collidersBuffer = new ComputeBuffer(numColliders, sizeof(float) * 4);
        grass1PosBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
        grass2PosBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
        grassMatrixBuffer = new ComputeBuffer(numPoints, sizeof(float) * 16);
        visibleIndexGrassBuffer = new ComputeBuffer(numPoints, sizeof(int));
        visibleGrassCounterBuffer = new ComputeBuffer(1, sizeof(uint));

        forceBuffer.SetData(forceData);
        collidersBuffer.SetData(collidersData);
        grass1PosBuffer.SetData(grassV1Positions);
        grass2PosBuffer.SetData(grassV2Positions);
        grassMatrixBuffer.SetData(grassModelMatrices);
        visibleIndexGrassBuffer.SetData(visibilityData);
        visibleGrassCounterBuffer.SetData(visibilityCounterData);

        // Set buffers
        grassPhysicsCS.SetBuffer(physicsKernelIndex, "forceBuffer", forceBuffer);
        grassPhysicsCS.SetBuffer(physicsKernelIndex, "colliders", collidersBuffer);
        grassPhysicsCS.SetBuffer(physicsKernelIndex, "v1Positions", grass1PosBuffer);
        grassPhysicsCS.SetBuffer(physicsKernelIndex, "v2Positions", grass2PosBuffer);
        grassPhysicsCS.SetBuffer(physicsKernelIndex, "grassWorldMatrix", grassMatrixBuffer);

        // Setup properties
        grassPhysicsCS.SetFloat("grassMass", grassMass);
        grassPhysicsCS.SetFloat("deltaTime", Time.deltaTime);

        grassPhysicsCS.SetFloat("numColliders", numColliders);
        grassPhysicsCS.SetFloat("stiffnessCoefficient", stiffnessCoefficient);
        grassPhysicsCS.SetFloat("collisionDecreaseAmount", collisionDecreaseAmount);
        
        grassPhysicsCS.SetFloat("gravityParam", 0.0f);
        grassPhysicsCS.SetVector("gravityDirection", new Vector4(0, -1.0f, 0, 9.81f));
        grassPhysicsCS.SetVector("gravityPoint", new Vector4(0, 0, 0, 9.81f));
        #endregion

        #region CULLING_SETUP
        commandBuf = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, commandCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[commandCount];
        commandData[0].indexCountPerInstance = grassMesh.GetIndexCount(0); // The number of vertex indices per instance.
        commandData[0].instanceCount = (visibilityCounterData[0]); // The number of instances to render.
        commandBuf.SetData(commandData);

        grassCullingCS.SetBuffer(cullingKernelIndex, "v1Positions", grass1PosBuffer);
        grassCullingCS.SetBuffer(cullingKernelIndex, "v2Positions", grass2PosBuffer);
        grassCullingCS.SetBuffer(cullingKernelIndex, "grassWorldMatrix", grassMatrixBuffer);
        grassCullingCS.SetBuffer(cullingKernelIndex, "indexGrass", visibleIndexGrassBuffer);
        grassCullingCS.SetBuffer(cullingKernelIndex, "visibleIndexGrass", visibleIndexGrassBuffer);
        grassCullingCS.SetBuffer(cullingKernelIndex, "visibleGrassCounterBuffer", visibleGrassCounterBuffer);
        
        grassIndirectArgsCS.SetBuffer(argsKernelIndex, "indirectArgsBuffer", commandBuf);
        grassIndirectArgsCS.SetBuffer(argsKernelIndex, "visibleGrassCounterBuffer", visibleGrassCounterBuffer);
        #endregion

        grassMaterial.SetBuffer("_V1Buffer", grass1PosBuffer);
        grassMaterial.SetBuffer("_V2Buffer", grass2PosBuffer);
        grassMaterial.SetBuffer("_VisibleIndex", visibleIndexGrassBuffer);
        grassMaterial.SetBuffer("_ObjectToWorld", grassMatrixBuffer);
    }

    // Update is called once per frame
    void Update()
    {
        #region SIMULATE_GRASS
        for (int i = 0; i < numColliders; i++)
        {
            collidersData[i][0] = colliders[i].transform.position.x;
            collidersData[i][1] = colliders[i].transform.position.y;
            collidersData[i][2] = colliders[i].transform.position.z;
        }
        collidersBuffer.SetData(collidersData);
        grassPhysicsCS.SetFloat("time", Time.time);
        grassPhysicsCS.SetBuffer(physicsKernelIndex, "colliders", collidersBuffer);
        grassPhysicsCS.Dispatch(physicsKernelIndex, numPoints / 8, 1, 1);

        // Set unique properties per object
        for (int i = 0; i < numPoints; i++)
        {
            GameObject childObject = gameObject.transform.GetChild(i).gameObject;
            Renderer childRenderer = childObject.GetComponent<Renderer>();
            if (childRenderer)
            {
                childRenderer.SetPropertyBlock(propertyBlock);
            }
        }
        #endregion

        #region CULLING_GRASS
        grassCullingCS.SetMatrix("viewProjectionMatrix", Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix);
        grassCullingCS.SetVector("cameraPos", Camera.main.transform.position + Camera.main.transform.forward);
        grassCullingCS.SetVector("cameraForward", Camera.main.transform.forward);
        grassCullingCS.Dispatch(cullingKernelIndex, numPoints / 8, 1, 1);
        AsyncGPUReadback.Request(visibleGrassCounterBuffer, OnCompleteReadback);
        #endregion

        #region RENDERING_GRASS
        // Request the readback
        grassIndirectArgsCS.Dispatch(argsKernelIndex, 1, 1, 1);
        Graphics.RenderMeshIndirect(rp, grassMesh, commandBuf, commandCount);
        #endregion
    }

    void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.Log("GPU readback error detected.");
            return;
        }

        // Get the data from the request
        Unity.Collections.NativeArray<uint> data = request.GetData<uint>();

        // Now you can use the data on the CPU
        // For example, print the first element:
        if (data.Length > 0)
        {
            Debug.Log("First element: " + data[0]);
        }

        // Remember to dispose of the NativeArray when you're done with it
        data.Dispose();
    }

    void OnDestroy()
    {
        commandBuf?.Release();
        commandBuf = null;

        forceBuffer?.Release();
        collidersBuffer?.Release();
        grass1PosBuffer?.Release();
        grass2PosBuffer?.Release();
        grassMatrixBuffer?.Release();
        visibleIndexGrassBuffer?.Release();
        visibleGrassCounterBuffer?.Release();
    }
}

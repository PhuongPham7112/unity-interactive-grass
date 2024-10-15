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
    
    private float[] forceData;
    private int[] visibilityData;
    private int[] visibilityCounterData;
    private Vector4[] collidersData; // colliders position + extent
    private Vector4[] grassV1Positions; // v1 xyz + grass height
    private Vector4[] grassV2Positions; // v2 xyz + grass width
    private Matrix4x4[] grassModelMatrices;

    private ComputeBuffer forceBuffer;
    private ComputeBuffer collidersBuffer;
    private ComputeBuffer grass1PosBuffer;
    private ComputeBuffer grass2PosBuffer;
    private ComputeBuffer grassMatrixBuffer;
    private ComputeBuffer visibleGrassBuffer;
    private ComputeBuffer visibleGrassCounterBuffer;
    private ComputeBuffer indirectArgsBuffer;

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
        grassMesh = grassPrefab.GetComponent<MeshFilter>().sharedMesh;
        rp = new RenderParams(grassMaterial)
        {
            worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one) // use tighter bounds for better FOV culling
        };
        propertyBlock = new MaterialPropertyBlock();
        physicsKernelIndex = grassPhysicsCS.FindKernel("CSMain");
        cullingKernelIndex = grassCullingCS.FindKernel("CSMain");

        int argsKernelIndex = grassIndirectArgsCS.FindKernel("CSMain");

        // Get number of child objects
        numPoints = gameObject.transform.childCount;
        numColliders = colliders.Length;

        #region SIMULATION_SETUP
        // Fill the buffer with the v2 positions of the child objects
        visibilityCounterData = new int[1];
        visibilityCounterData[0] = 0;
        visibilityData = new int[numPoints];
        grassV1Positions = new Vector4[numPoints];
        grassV2Positions = new Vector4[numPoints];
        forceData = new float[numPoints]; // Fill the buffer with force data
        grassModelMatrices = new Matrix4x4[numPoints]; // Fill the buffer with model matrix
        for (int i = 0; i < numPoints; i++)
        {
            forceData[i] = 0.0f;
            visibilityData[i] = i;
            grassModelMatrices[i] = gameObject.transform.GetChild(i).localToWorldMatrix;
            grassV1Positions[i] = new Vector4(0, grassHeight * 0.5f, Mathf.Epsilon, grassHeight);
            grassV2Positions[i] = new Vector4(0, grassHeight, Mathf.Epsilon, grassWidth);
        }

        // Fill the buffer with sphere colliders data
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
        visibleGrassBuffer = new ComputeBuffer(numPoints, sizeof(int));
        visibleGrassCounterBuffer = new ComputeBuffer(1, sizeof(int));
        indirectArgsBuffer = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);

        forceBuffer.SetData(forceData);
        collidersBuffer.SetData(collidersData);
        grass1PosBuffer.SetData(grassV1Positions);
        grass2PosBuffer.SetData(grassV2Positions);
        grassMatrixBuffer.SetData(grassModelMatrices);
        visibleGrassBuffer.SetData(visibilityData);
        visibleGrassCounterBuffer.SetData(visibilityCounterData);

        // Set buffers
        grassPhysicsCS.SetBuffer(physicsKernelIndex, "forceBuffer", forceBuffer);
        grassPhysicsCS.SetBuffer(physicsKernelIndex, "colliders", collidersBuffer);
        grassPhysicsCS.SetBuffer(physicsKernelIndex, "v1Positions", grass1PosBuffer);
        grassPhysicsCS.SetBuffer(physicsKernelIndex, "v2Positions", grass2PosBuffer);
        grassMaterial.SetBuffer("_V1Buffer", grass1PosBuffer);
        grassMaterial.SetBuffer("_V2Buffer", grass2PosBuffer);
        grassPhysicsCS.SetBuffer(physicsKernelIndex, "grassWorldMatrix", grassMatrixBuffer);

        // Setup properties
        grassPhysicsCS.SetFloat("numColliders", numColliders);
        grassPhysicsCS.SetFloat("stiffnessCoefficient", stiffnessCoefficient);
        grassPhysicsCS.SetFloat("collisionDecreaseAmount", collisionDecreaseAmount);
        
        grassPhysicsCS.SetVector("gravityDirection", new Vector4(0, -1.0f, 0, 9.81f));
        grassPhysicsCS.SetVector("gravityPoint", new Vector4(0, 0, 0, 9.81f));
        grassPhysicsCS.SetFloat("gravityParam", 0.0f);
        #endregion

        #region CULLING_SETUP
        commandBuf = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, commandCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[commandCount];
        grassCullingCS.SetBuffer(cullingKernelIndex, "visibleGrass", visibleGrassBuffer);
        grassCullingCS.SetBuffer(cullingKernelIndex, "visibleGrassCounter", visibleGrassCounterBuffer);
        grassIndirectArgsCS.SetBuffer(argsKernelIndex, "visibleGrassCounter", visibleGrassCounterBuffer);
        grassIndirectArgsCS.SetBuffer(argsKernelIndex, "indirectArgsBuffer", commandBuf);
        grassCullingCS.SetInt("totalVisible", 0);
        #endregion

        grassMaterial.SetBuffer("_VisibleIndex", visibleGrassBuffer);
        grassMaterial.SetBuffer("_ObjectToWorld", grassMatrixBuffer);
        grassPhysicsCS.SetFloat("deltaTime", Time.deltaTime);
        grassPhysicsCS.SetFloat("grassMass", grassMass);
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
        grassCullingCS.Dispatch(cullingKernelIndex, numPoints / 8, 1, 1);
        #endregion

        #region RENDERING_GRASS
        commandData[0].indexCountPerInstance = grassMesh.GetIndexCount(0); // The number of vertex indices per instance.
        commandData[0].instanceCount = (uint)(visibilityCounterData[0]); // The number of instances to render.
        commandBuf.SetData(commandData);
        Graphics.RenderMeshIndirect(rp, grassMesh, commandBuf, commandCount);
        #endregion
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
        visibleGrassBuffer?.Release();
        indirectArgsBuffer?.Release();
        visibleGrassCounterBuffer?.Release();
    }
}

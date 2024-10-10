using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassModel : MonoBehaviour
{
    private int kernelIndex;
    private float[] forceData;
    private Vector4[] collidersData; // colliders position + extent
    private Vector4[] grassV1Positions; // v1 xyz + grass height
    private Vector4[] grassV2Positions; // v2 xyz + grass width
    private Matrix4x4[] grassModelMatrices;

    private ComputeBuffer forceBuffer;
    private ComputeBuffer modelMatrixBuffer;
    private ComputeBuffer collidersBuffer;
    private ComputeBuffer grass1PosBuffer;
    private ComputeBuffer grass2PosBuffer;
    
    [SerializeField] private Material grassMaterial;
    [SerializeField] private ComputeShader grassPhysicsCS;
    [SerializeField] private ComputeShader grassCulling;
    [SerializeField] public SphereCollider[] colliders;
    
    [SerializeField] public float collisionDecreaseAmount = 0.5f;
    [SerializeField] public float stiffnessCoefficient = 0.1f;
    [SerializeField] public float grassMass = 0.5f;
    [SerializeField] public float grassWidth = 1.0f;
    [SerializeField] public float grassHeight = 1.0f;
    [SerializeField] private int numPoints;
    [SerializeField] private int numColliders;

    private MaterialPropertyBlock propertyBlock;

    // Start is called before the first frame update
    void Start()
    {
        propertyBlock = new MaterialPropertyBlock();
        kernelIndex = grassPhysicsCS.FindKernel("CSMain");

        // Get number of child objects
        numPoints = gameObject.transform.childCount;
        numColliders = colliders.Length;

        // Fill the buffer with the v2 positions of the child objects
        grassV1Positions = new Vector4[numPoints];
        grassV2Positions = new Vector4[numPoints];
        forceData = new float[numPoints]; // Fill the buffer with force data
        grassModelMatrices = new Matrix4x4[numPoints]; // Fill the buffer with model matrix
        for (int i = 0; i < numPoints; i++)
        {
            forceData[i] = 0.0f;
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
        modelMatrixBuffer = new ComputeBuffer(numPoints, sizeof(float) * 16);

        forceBuffer.SetData(forceData);
        collidersBuffer.SetData(collidersData);
        grass1PosBuffer.SetData(grassV1Positions);
        grass2PosBuffer.SetData(grassV2Positions);
        modelMatrixBuffer.SetData(grassModelMatrices);

        // Set buffers
        grassPhysicsCS.SetBuffer(kernelIndex, "forceBuffer", forceBuffer);
        grassPhysicsCS.SetBuffer(kernelIndex, "colliders", collidersBuffer);
        grassPhysicsCS.SetBuffer(kernelIndex, "v1Positions", grass1PosBuffer);
        grassPhysicsCS.SetBuffer(kernelIndex, "v2Positions", grass2PosBuffer);
        grassPhysicsCS.SetBuffer(kernelIndex, "modelMatrix", modelMatrixBuffer);

        // Setup properties
        grassPhysicsCS.SetFloat("deltaTime", Time.deltaTime);
        grassPhysicsCS.SetFloat("grassMass", grassMass);
        grassPhysicsCS.SetFloat("numColliders", numColliders);
        grassPhysicsCS.SetFloat("stiffnessCoefficient", stiffnessCoefficient);
        grassPhysicsCS.SetFloat("collisionDecreaseAmount", collisionDecreaseAmount);
        
        grassPhysicsCS.SetVector("gravityDirection", new Vector4(0, -1.0f, 0, 9.81f));
        grassPhysicsCS.SetVector("gravityPoint", new Vector4(0, 0, 0, 9.81f));
        grassPhysicsCS.SetFloat("gravityParam", 0.0f);
    }

    // Update is called once per frame
    void Update()
    {
        // update collider buffer
        for (int i = 0; i < numColliders; i++)
        {
            collidersData[i][0] = colliders[i].transform.position.x;
            collidersData[i][1] = colliders[i].transform.position.y;
            collidersData[i][2] = colliders[i].transform.position.z;
        }
        collidersBuffer.SetData(collidersData);
        grassPhysicsCS.SetBuffer(kernelIndex, "colliders", collidersBuffer);
        grassPhysicsCS.SetFloat("time", Time.time);
        grassPhysicsCS.SetMatrix("worldToLocalMatrix", transform.worldToLocalMatrix);
        grassPhysicsCS.Dispatch(kernelIndex, numPoints / 8, 1, 1);

        // Set unique properties per object
        grassMaterial.SetBuffer("_V1Buffer", grass1PosBuffer);
        grassMaterial.SetBuffer("_V2Buffer", grass2PosBuffer);
        for (int i = 0; i < numPoints; i++)
        {
            GameObject childObject = gameObject.transform.GetChild(i).gameObject;
            Renderer childRenderer = childObject.GetComponent<Renderer>();
            if (childRenderer)
            {
                propertyBlock.SetInteger("_Index", i);
                childRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }

    void OnDestroy()
    {
        forceBuffer?.Release();
        collidersBuffer?.Release();
        grass1PosBuffer?.Release();
        grass2PosBuffer?.Release();
        modelMatrixBuffer?.Release();
    }
}

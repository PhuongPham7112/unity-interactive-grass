using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassModel : MonoBehaviour
{
    private int kernelIndex;
    private RenderTexture forceTexture;
    private Vector4[] collidersData; // colliders position + extent
    private Vector4[] grassV1Positions; // v1 xyz + grass height
    private Vector4[] grassV2Positions; // v2 xyz + grass width
    private Vector3[] grassGroundPositions; // v2 xyz + grass width
    
    private ComputeBuffer collidersBuffer;
    private ComputeBuffer groundPosBuffer;
    private ComputeBuffer grass1PosBuffer;
    private ComputeBuffer grass2PosBuffer;
    
    [SerializeField] public SphereCollider[] colliders;
    [SerializeField] private ComputeShader grassPhysicsCS;
    [SerializeField] private Material grassMaterial;
    
    [SerializeField] public float decreaseAmount = 0.5f;
    [SerializeField] public float stiffnessCoefficient = 0.1f;
    [SerializeField] public float collisionStrength = 0.2f;
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
        grassGroundPositions = new Vector3[numPoints];
        grassV1Positions = new Vector4[numPoints];
        grassV2Positions = new Vector4[numPoints];
        for (int i = 0; i < numPoints; i++)
        {
            grassGroundPositions[i] = gameObject.transform.GetChild(i).position;
            grassV1Positions[i] = new Vector4(0, grassHeight * 0.5f, Mathf.Epsilon, grassHeight);
            grassV2Positions[i] = new Vector4(0, grassHeight, Mathf.Epsilon, grassWidth);
        }

        // Fill the buffer with sphere colliders data
        collidersData = new Vector4[numColliders];
        for (int i = 0; i < numColliders; i++)
        {
            collidersData[i] = new Vector4(colliders[i].center.x, 
                colliders[i].center.y,
                colliders[i].center.z,
                colliders[i].radius);
            Debug.Log(colliders[i].radius);
        }

        // Set up texture for force
        forceTexture = new RenderTexture(numPoints / 2, numPoints / 2, 0);
        forceTexture.enableRandomWrite = true;
        forceTexture.Create();
        grassPhysicsCS.SetTexture(kernelIndex, "forceTexture", forceTexture);

        // Setup buffers
        collidersBuffer = new ComputeBuffer(numColliders, sizeof(float) * 4);
        groundPosBuffer = new ComputeBuffer(numPoints, sizeof(float) * 3);
        grass1PosBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
        grass2PosBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);

        collidersBuffer.SetData(collidersData);
        groundPosBuffer.SetData(grassGroundPositions);
        grass1PosBuffer.SetData(grassV1Positions);
        grass2PosBuffer.SetData(grassV2Positions);

        grassPhysicsCS.SetBuffer(kernelIndex, "colliders", collidersBuffer);
        grassPhysicsCS.SetBuffer(kernelIndex, "v1Positions", grass1PosBuffer);
        grassPhysicsCS.SetBuffer(kernelIndex, "v2Positions", grass2PosBuffer);
        grassPhysicsCS.SetBuffer(kernelIndex, "groundPositions", groundPosBuffer);

        // Setup properties
        grassPhysicsCS.SetFloat("grassMass", grassMass);
        grassPhysicsCS.SetFloat("numColliders", numColliders);
        grassPhysicsCS.SetFloat("collisionStrength", collisionStrength);
        grassPhysicsCS.SetFloat("stiffnessCoefficient", stiffnessCoefficient);
        
        grassPhysicsCS.SetVector("gravityDirection", new Vector4(0, -1.0f, 0, 9.81f));
        grassPhysicsCS.SetVector("gravityPoint", new Vector4(0, 0, 0, 9.81f));
        grassPhysicsCS.SetFloat("gravityParam", 0.0f);
    }

    // Update is called once per frame
    void Update()
    {
        // Run the compute shader
        grassPhysicsCS.SetFloat("deltaTime", Time.deltaTime);
        grassPhysicsCS.SetFloat("time", Time.time);
        grassPhysicsCS.SetMatrix("objectToWorldMatrix", transform.localToWorldMatrix);
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
        if (forceTexture) forceTexture.Release();
        collidersBuffer?.Release();
        groundPosBuffer?.Release();
        grass1PosBuffer?.Release();
        grass2PosBuffer?.Release();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassModel : MonoBehaviour
{
    private int kernelIndex;
    private Vector4[] grassV1Positions; // v1 xyz + grass height
    private Vector4[] grassV2Positions; // v2 xyz + grass width
    private ComputeBuffer grass1PosBuffer;
    private ComputeBuffer grass2PosBuffer;
    private ComputeBuffer grassLength;
    private MaterialPropertyBlock propertyBlock;
    [SerializeField] private ComputeShader grassPhysicsCS;
    [SerializeField] private Material grassMaterial;
    [SerializeField] public float grassWidth = 1.0f;
    [SerializeField] public float grassHeight = 1.0f;
    [SerializeField] private int numPoints;

    // Start is called before the first frame update
    void Start()
    {
        propertyBlock = new MaterialPropertyBlock();
        kernelIndex = grassPhysicsCS.FindKernel("CSMain");
        Debug.Log(kernelIndex);

        // Get number of child objects
        numPoints = gameObject.transform.childCount;

        // Fill the buffer with the v2 positions of the child objects
        grassV1Positions = new Vector4[numPoints];
        grassV2Positions = new Vector4[numPoints];
        for (int i = 0; i < numPoints; i++)
        {
            grassV1Positions[i] = new Vector4(0, grassHeight * 0.5f, Mathf.Epsilon, grassHeight);
            grassV2Positions[i] = new Vector4(0, grassHeight, Mathf.Epsilon, grassWidth);
        }

        // Setup buffer
        grassLength = new ComputeBuffer(numPoints, sizeof(float));
        grass1PosBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
        grass2PosBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);

        grass1PosBuffer.SetData(grassV1Positions);
        grass2PosBuffer.SetData(grassV2Positions);

        grassPhysicsCS.SetBuffer(kernelIndex, "grassLength", grassLength);
        grassPhysicsCS.SetBuffer(kernelIndex, "v1Positions", grass1PosBuffer);
        grassPhysicsCS.SetBuffer(kernelIndex, "v2Positions", grass2PosBuffer);
    }

    // Update is called once per frame
    void Update()
    {
        // Run the compute shader
        grassPhysicsCS.SetFloat("time", Time.time);
        grassPhysicsCS.SetMatrix("objectToWorldMatrix", transform.localToWorldMatrix);
        grassPhysicsCS.Dispatch(kernelIndex, numPoints / 8, 1, 1);

        // Set unique properties per object
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

        // check
        UnityEngine.Rendering.AsyncGPUReadback.Request(grassLength, request =>
        {
            if (request.hasError)
            {
                Debug.Log("GPU readback error detected.");
                return;
            }
            float result = request.GetData<float>()[0];
            // Use the result here
            Debug.Log(result);
        });

    }

    void OnDestroy()
    {
        grassLength?.Release();
        grass1PosBuffer?.Release();
        grass2PosBuffer?.Release();
    }
}

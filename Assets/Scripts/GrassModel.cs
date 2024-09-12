using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassModel : MonoBehaviour
{
    private int kernelIndex;
    private Vector3[] grassV2Positions;
    private ComputeBuffer grassPosBuffer;
    private MaterialPropertyBlock propertyBlock;
    [SerializeField] private ComputeShader grassPhysicsCS;
    [SerializeField] private Material grassMaterial;
    [SerializeField] public float grassHeight = 1.0f;
    [SerializeField] private int numPoints;

    // Start is called before the first frame update
    void Start()
    {
        propertyBlock = new MaterialPropertyBlock();
        kernelIndex = grassPhysicsCS.FindKernel("CSMain");
        
        // Get number of child objects
        numPoints = gameObject.transform.childCount;

        // Fill the buffer with the local v2 positions of the child objects
        grassV2Positions = new Vector3[numPoints];
        for (int i = 0; i < numPoints; i++)
        {
            grassV2Positions[i] = gameObject.transform.GetChild(i).position + 
                gameObject.transform.GetChild(i).up * grassHeight + 
                gameObject.transform.GetChild(i).right * 0.0f;
        }
        
        // Setup buffer
        grassPosBuffer = new ComputeBuffer(numPoints, sizeof(float) * 3);
        grassPosBuffer.SetData(grassV2Positions);
        grassPhysicsCS.SetBuffer(0, "v2Positions", grassPosBuffer);  
    }

    // Update is called once per frame
    void Update()
    {
        // Run the compute shader
        grassPhysicsCS.SetFloat("time", Time.time);
        grassPhysicsCS.SetMatrix("objectToWorldMatrix", transform.localToWorldMatrix);
        grassPhysicsCS.Dispatch(kernelIndex, numPoints / 8, 1, 1);
        grassMaterial.SetBuffer("_V2Buffer", grassPosBuffer);

        // Set unique properties per object
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
        grassPosBuffer.Release();
    }
}

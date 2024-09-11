using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassModel : MonoBehaviour
{
    private int kernelIndex;
    private Vector3[] grassV2Positions;
    private ComputeBuffer grassPosBuffer;
    [SerializeField] private ComputeShader grassPhysicsCS;
    [SerializeField] private Material grassMaterial;
    [SerializeField] public float grassHeight = 1.0f;
    [SerializeField] private int numPoints;

    // Start is called before the first frame update
    void Start()
    {
        kernelIndex = grassPhysicsCS.FindKernel("CSMain");
        
        // Get number of child objects
        numPoints = gameObject.transform.childCount;
        
        // Fill the buffer with the local v2 positions of the child objects
        for (int i = 0; i < numPoints; i++)
        {
            grassV2Positions[i] = gameObject.transform.GetChild(i).gameObject.transform.localPosition + new Vector3(0, grassHeight, Mathf.Epsilon);
        }
        
        // Setup buffer
        grassPosBuffer = new ComputeBuffer(numPoints, sizeof(float) * 3);
        grassPosBuffer.SetData(grassV2Positions);
        grassPhysicsCS.SetBuffer(0, "v2Positions", grassPosBuffer);  
    }

    // Update is called once per frame
    void Update()
    {
        grassPhysicsCS.SetFloat("time", Time.time);
        grassPhysicsCS.SetMatrix("objectToWorldMatrix", transform.localToWorldMatrix);
        grassPhysicsCS.Dispatch(kernelIndex, numPoints / 8, 1, 1);
    }

    void OnDestroy()
    {
        grassPosBuffer.Release();
    }
}

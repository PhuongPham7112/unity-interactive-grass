Shader "Custom/GrassTessellation"
{
    // add exposed properties
    Properties
    {
        _EdgeFactors("Edge Factors", Vector) = (3, 3, 3)
        _InsideFactor("Inside Factor", Float) = 3
    }
    SubShader
    {
        Pass
        {
            HLSLPROGRAM

            // setup
            #pragma vertex vert
            #pragma fragment frag
            #pragma hull hull
            #pragma domain domain
            #pragma target 5.0
            #include "UnityCG.cginc"

            float3 _EdgeFactors;
            float _InsideFactor;

            #define BARYCENTRIC_INTERPOLATE(fieldName) \
                    patch[0].fieldName * barycentricCoordinates.x + \
                    patch[1].fieldName * barycentricCoordinates.y + \
                    patch[2].fieldName * barycentricCoordinates.z
            
            #define NUM_BEZIER_CONTROL_POINTS 4

            struct TSControlPoint
            {
                float3 positionWS: INTERNALTESSPOS;
                float3 normalWS: NORMAL;
            };

            struct TSFactors
            {
                float edge[3]: SV_TessFactor; // number of times an edge subdivides
                float inside: SV_InsideTessFactor; //  roughly the number of new triangles created inside the original triangle
                float3 bezierPoints[NUM_BEZIER_CONTROL_POINTS] : BEZIERPOS; // output control points for a Bézier-curve-based tessellation
            };

            struct TSInterpolators {
                float3 normalWS: TEXCOORD0;
                float3 positionWS: TEXCOORD1;
                float4 positionCS: SV_POSITION;
            };


            TSControlPoint vert(appdata_full i)
            {
                TSControlPoint tc;
                tc.positionWS = mul(unity_ObjectToWorld, i.vertex).xyz;
                tc.normalWS = UnityObjectToWorldNormal(i.normal);
                return tc;
            }

            /*
                The patch constant function runs once per triangle, or "patch"
                It runs in parallel to the hull function
                The patch constant function has a much simpler signature. 
                It receives the input patch similarly to the hull function but outputs its own data structure. 
                This structure should contain the tessellation factors specified per edge on the triangle using SV_TessFactor. 
                Edges are arranged opposite of the vertex with the same index. So, edge zero lies between vertices one and two.
            */
            TSFactors PatchConstantFunction(
                InputPatch<TSControlPoint, 3> patch) {
                // Calculate tessellation factors
                TSFactors f;
                f.edge[0] = _EdgeFactors.x;
                f.edge[1] = _EdgeFactors.y;
                f.edge[2] = _EdgeFactors.z;
                f.inside = _InsideFactor;
                return f;
            }

            [domain("tri")]
            [partitioning("integer")]
            [outputtopology("triangle_cw")]
            [outputcontrolpoints(3)]
            [patchconstantfunc("PatchConstantFunction")]
            TSControlPoint hull(InputPatch<TSControlPoint, 3> patch, uint vertId : SV_OutputControlPointID)
            {
                return patch[vertId];
            }

            /*
            */
            [domain("tri")]
            TSInterpolators domain(TSFactors factors, // The output of the patch constant function
                OutputPatch<TSControlPoint, 3> patch, // The Input triangle
                float3 barycentricCoordinates : SV_DomainLocation // The barycentric coordinates of the vertex on the triangle
            )
            {
                TSInterpolators output;
                float3 positionWS = BARYCENTRIC_INTERPOLATE(positionWS);
                float3 normalWS = BARYCENTRIC_INTERPOLATE(normalWS);
                output.positionCS = mul(UNITY_MATRIX_VP, float4(positionWS.xyz, 1.0)); // clip space
                output.normalWS = normalWS; // world space
                output.positionWS = positionWS; // world space
                return output;
            }


            float4 frag(TSControlPoint tc) : SV_Target
            {
                float strength = saturate(dot(tc.normalWS, float3(0, 1, 0)));
                return strength * float4(0, 1, 0, 1);
            }

            ENDHLSL
        }
    }
}


Shader "Custom/GrassTessellation"
{
    // add exposed properties
    Properties
    {
        _Color("Grass Color", Color) = (0, 1, 0 ,1)
        _BezierControlV0("Curve Control Point 0", Vector) = (0, 0, 0)
        _BezierControlV1("Curve Control Point 1", Vector) = (0, 0.5, 0)
        _BezierControlV2("Curve Control Point 2", Vector) = (0, 1, 0)
        _EdgeFactors("Edge Factors", Vector) = (3, 3, 3)
        _InsideFactor("Inside Factor", Float) = 3
    }
        SubShader
    {
        Cull Off
        Pass
        {
            HLSLPROGRAM

            // setup
            //#pragma geometry geom
            #pragma vertex vert
            #pragma fragment frag
            #pragma hull hull
            #pragma domain domain
            #pragma target 5.0
            #include "UnityCG.cginc"

            float3 _BezierControlV0;
            float3 _BezierControlV1;
            float3 _BezierControlV2;
            float3 _EdgeFactors;
            float4 _Color;
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
                float3 tangentWS: TANGENT;
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
                float3 tangentWS: TEXCOORD2;
                float4 positionCS: SV_POSITION;
            };

            struct GSOutput {
                float4 positionCS: SV_POSITION;
            };


            // vertex shader
            TSControlPoint vert(appdata_full i)
            {
                TSControlPoint tc;
                tc.positionWS = mul(unity_ObjectToWorld, i.vertex).xyz;
                tc.normalWS = UnityObjectToWorldNormal(i.normal);
                tc.tangentWS = UnityObjectToWorldNormal(i.tangent);
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

            // hull shader
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
                domain shader
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
                output.tangentWS = BARYCENTRIC_INTERPOLATE(tangentWS); // world space
                return output;
            }

            //[maxvertexcount(200)]
            //void geom(triangle TSInterpolators IN[3], inout TriangleStream<GSOutput> triStream)
            //{
            //    GSOutput o;
            //    float3 offset;

            //    // newly generated geo
            //    for (int i = 0; i < 3; i++)
            //    {
            //        if (i == 0)
            //        {
            //            offset = float3(0.1, 0, 0);
            //        }
            //        else if (i == 1)
            //        {
            //            offset = float3(-0.1, 0, 0);
            //        }
            //        else if (i == 2)
            //        {
            //            offset = float3(0, 0, .2);
            //        }

            //        float3 positionOS = mul(unity_WorldToObject, float4(IN[0].positionWS, 1.0)).xyz;
            //        float3 vNormal = mul(unity_WorldToObject, float4(IN[0].normalWS, 1.0)).xyz;
            //        float3 vTangent = mul(unity_WorldToObject, float4(IN[0].tangentWS, 1.0)).xyz;
            //        float3 vBinormal = cross(vNormal, vTangent);

            //        float3x3 tangentToObject = float3x3(
            //            vTangent.x, vBinormal.x, vNormal.x,
            //            vTangent.y, vBinormal.y, vNormal.y,
            //            vTangent.z, vBinormal.z, vNormal.z
            //            );

            //        float3 offsetOS = mul(tangentToObject, offset);
            //        o.positionCS = UnityObjectToClipPos(positionOS + offsetOS);
            //        triStream.Append(o);
            //    }
            //    triStream.RestartStrip();

            //    // already have geo
            //    o.positionCS = IN[0].positionCS;
            //    triStream.Append(o);

            //    o.positionCS = IN[1].positionCS;
            //    triStream.Append(o);

            //    o.positionCS = IN[2].positionCS;
            //    triStream.Append(o);

            //    triStream.RestartStrip();
            //}

            float4 frag(TSInterpolators tc) : SV_Target
            {
                return _Color;
            }

            ENDHLSL
        }
    }
}


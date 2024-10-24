Shader "Custom/GrassTessellation"
{
    // add exposed properties
    Properties
    {
        _Color("Grass Color", Color) = (0, 1, 0 ,1)
        _BezierControlV0("Curve Control Point 0", Vector) = (0, 0, 0)
        _BezierControlV1("Curve Control Point 1", Vector) = (0, 0.1, 0)
        _BezierControlV2("Curve Control Point 2", Vector) = (0, 1, 0.3)
        _Dimension("Grass Width & Height", Vector) = (0.1, 0.5, 0)
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
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            int _Index;
            float3 _BezierControlV0;
            float3 _BezierControlV1;
            float3 _BezierControlV2;
            float3 _EdgeFactors;
            float4 _Color;
            float _InsideFactor;
            float2 _Dimension;
            StructuredBuffer<int> _VisibleIndex;
            StructuredBuffer<float4> _V1Buffer;
            StructuredBuffer<float4> _V2Buffer;
            StructuredBuffer<float4x4> _ObjectToWorld;

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
                float4 color: COLOR;
            };

            struct TSFactors
            {
                float edge[3]: SV_TessFactor; // number of times an edge subdivides
                float inside: SV_InsideTessFactor; //  roughly the number of new triangles created inside the original triangle
                float3 bezierPoints[NUM_BEZIER_CONTROL_POINTS] : BEZIERPOS; // output control points for a Bézier-curve-based tessellation
            };

            struct TSInterpolators {
                float4 color: COLOR;
                float3 normalWS: TEXCOORD0;
                float3 positionWS: TEXCOORD1;
                float3 tangentWS: TEXCOORD2;
                float4 positionCS: SV_POSITION;
            };

            struct GSOutput {
                float4 positionCS: SV_POSITION;
            };


            // vertex shader
            TSControlPoint vert(appdata_full i, uint svInstanceID : SV_InstanceID)
            {
                // setup the ID access functions
                InitIndirectDrawArgs(0);
                uint cmdID = GetCommandID(0);
                uint instanceID = GetIndirectInstanceID(svInstanceID);
                _Index = _VisibleIndex[instanceID];

                float u = i.texcoord.x;
                float v = i.texcoord.y;

                // control points
                float3 p_0 = mul(float4(0.0, 0.0, 0.0, 1.0), unity_WorldToObject).xyz;
                float3 p_1 = mul(float4(_V1Buffer[_Index].xyz, 1.0), unity_WorldToObject).xyz;
                float3 p_2 = mul(float4(_V2Buffer[_Index].xyz, 1.0), unity_WorldToObject).xyz;

                // De Casteljau's algorithm
                float3 a = v * (p_1 - p_0) + p_0;
                float3 b = v * (p_2 - p_1) + p_1;
                float3 c = v * (b - a) + a;

                // coordinate systems
                float3 tangent = normalize(b - a);
                float3 binormal = float3(1, 0, 0);
                float3 normal = normalize(cross(binormal, tangent)); // should be provided
                
                // final vertex position
                float3 c_0 = c - (1 - v) * _Dimension.x * binormal;
                float3 c_1 = c + (1 - v) * _Dimension.x * binormal;
                float3 vert =  c_0 + (c_1 - c_0) * u;
                
                // output
                TSControlPoint tc;
                tc.positionWS = mul(_ObjectToWorld[_Index], float4(vert, 1.0)).xyz;
                tc.color = float4(v, v, v, 1);
                tc.normalWS = UnityObjectToWorldNormal(normal);
                tc.tangentWS = UnityObjectToWorldNormal(tangent);
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
                output.color = BARYCENTRIC_INTERPOLATE(color);
                return output;
            }

            float4 frag(TSInterpolators tc) : SV_Target
            {

                return _Color * tc.color;
            }

            ENDHLSL
        }
    }
}


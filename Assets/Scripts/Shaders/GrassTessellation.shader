Shader "Custom/GrassTessellation"
{
    Properties
    {
        _TessellationUniform("Tessellation Uniform", Range(1, 64)) = 1
        // Add more properties here as needed
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows tessellate:tessFixed
        #pragma target 5.0
        #include "Tessellation.cginc"

        struct Input
        {
            float2 uv_MainTex;
        };

        float _TessellationUniform;

        // Tessellation function
        float4 tessFixed()
        {
            return _TessellationUniform;
        }

        // Vertex function
        void vert(inout appdata_full v)
        {
            // Modify vertices here if needed
        }

        // Surface function
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Set albedo, normal, metallic, smoothness, etc. here
            o.Albedo = fixed3(0, 1, 0);
        }
        ENDCG
    }
        FallBack "Diffuse"
}


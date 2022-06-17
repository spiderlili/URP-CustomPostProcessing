Shader "PostProcessing/Kawase Blur"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" { }
    }

    SubShader
    {   
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                float _Offset;
                float _ExtraBlur;
                float _BlurRadius;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f input) : SV_Target
            {
                float2 res = _MainTex_TexelSize.xy;
                float i = _Offset * _BlurRadius;
                
                half4 col = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv, _ExtraBlur);
                col += SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv + float2( i, i ) * res, _ExtraBlur);
                col += SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv + float2( i, -i ) * res, _ExtraBlur);
                col += SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv + float2( -i, i ) * res, _ExtraBlur);
                col += SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv + float2( -i, -i ) * res, _ExtraBlur);
                col /= 5.0f;

                col = saturate(col);
                
                return col;
            }
            ENDHLSL
        }
    }
}
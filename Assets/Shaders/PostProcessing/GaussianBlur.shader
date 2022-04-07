Shader "PostProcessing/Gaussian Blur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" { }
        _FocusPower("Focus Power", float) = 0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Tags { "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float2 _FocusScreenPosition;
            float _FocusPower;
            CBUFFER_END
            
            struct appdata
            {
                float4 vertex: POSITION;
                float2 uv: TEXCOORD0;
            };

            struct v2f
            {
                float2 uv: TEXCOORD0;
                float4 vertex: SV_POSITION;
            };

            v2f Vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.rgb);
                o.uv = v.uv;
                return o;
            }

            float4 Frag(v2f i): SV_Target
            {
                float2 uv = i.uv;

                half2 uv1 = uv + half2(_FocusPower / _ScreenParams.x, _FocusPower / _ScreenParams.y) * half2(1, 0) * - 2.0;
                half2 uv2 = uv + half2(_FocusPower / _ScreenParams.x, _FocusPower / _ScreenParams.y) * half2(1, 0) * - 1.0;
                half2 uv3 = uv + half2(_FocusPower / _ScreenParams.x, _FocusPower / _ScreenParams.y) * half2(1, 0) * 0.0;
                half2 uv4 = uv + half2(_FocusPower / _ScreenParams.x, _FocusPower / _ScreenParams.y) * half2(1, 0) * 1.0;
                half2 uv5 = uv + half2(_FocusPower / _ScreenParams.x, _FocusPower / _ScreenParams.y) * half2(1, 0) * 2.0;
                half4 s = 0;

                s += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv1) * 0.0545;
                s += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv2) * 0.2442;
                s += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv3) * 0.4026;
                s += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv4) * 0.2442;
                s += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv5) * 0.0545;

                return s;
            }
            ENDHLSL

        }
        
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float2 _FocusScreenPosition;
            float _FocusPower;
            CBUFFER_END
            
            struct appdata
            {
                float4 vertex: POSITION;
                float2 uv: TEXCOORD0;
            };

            struct v2f
            {
                float2 uv: TEXCOORD0;
                float4 vertex: SV_POSITION;
            };

            v2f Vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.rgb);
                o.uv = v.uv;
                return o;
            }

            float4 Frag(v2f i): SV_Target
            {
                float2 uv = i.uv;

                half2 uv1 = uv + half2(_FocusPower / _ScreenParams.x, _FocusPower / _ScreenParams.y) * half2(0, 1) * - 2.0;
                half2 uv2 = uv + half2(_FocusPower / _ScreenParams.x, _FocusPower / _ScreenParams.y) * half2(0, 1) * - 1.0;
                half2 uv3 = uv + half2(_FocusPower / _ScreenParams.x, _FocusPower / _ScreenParams.y) * half2(0, 1) * 0.0;
                half2 uv4 = uv + half2(_FocusPower / _ScreenParams.x, _FocusPower / _ScreenParams.y) * half2(0, 1) * 1.0;
                half2 uv5 = uv + half2(_FocusPower / _ScreenParams.x, _FocusPower / _ScreenParams.y) * half2(0, 1) * 2.0;
                half4 s = 0;

                s += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv1) * 0.0545;
                s += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv2) * 0.2442;
                s += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv3) * 0.4026;
                s += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv4) * 0.2442;
                s += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv5) * 0.0545;

                return s;
            }
            ENDHLSL

        }
    }
}
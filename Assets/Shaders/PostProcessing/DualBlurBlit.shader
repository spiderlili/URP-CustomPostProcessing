Shader "PostProcessing/DualBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurRange ("Blur Range", Float) = 1.0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // Pass 0: DownSample
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_downsample
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv[5] : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurRange;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv[0] = v.uv;
                o.uv[1] = v.uv + float2(-1, -1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[2] = v.uv + float2(-1,  1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[3] = v.uv + float2(1,  -1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[4] = v.uv + float2(1,   1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                return o;
            }

            half4 frag_downsample (v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv[0]) * 4;
                col += tex2D(_MainTex, i.uv[1]);
                col += tex2D(_MainTex, i.uv[2]);
                col += tex2D(_MainTex, i.uv[3]);
                col += tex2D(_MainTex, i.uv[4]);
                return col * 0.125; // sum / 8.0
            }
            ENDCG
        }

        // Pass 1: UpSample
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_upsample
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv[8] : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurRange;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv[0] = v.uv + float2(-1, -1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[1] = v.uv + float2(-1,  1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[2] = v.uv + float2(1,  -1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[3] = v.uv + float2(1,   1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[4] = v.uv + float2(-2,  0) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[5] = v.uv + float2(0,  -2) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[6] = v.uv + float2(2,   0) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[7] = v.uv + float2(0,   2) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                return o;
            }

            half4 frag_upsample (v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv[0]) * 2;
                col += tex2D(_MainTex, i.uv[1]) * 2;
                col += tex2D(_MainTex, i.uv[2]) * 2;
                col += tex2D(_MainTex, i.uv[3]) * 2;
                col += tex2D(_MainTex, i.uv[4]);
                col += tex2D(_MainTex, i.uv[5]);
                col += tex2D(_MainTex, i.uv[6]);
                col += tex2D(_MainTex, i.uv[7]);
                return col * 0.0833; // sum / 12.0
            }
            ENDCG
        }
    }
}
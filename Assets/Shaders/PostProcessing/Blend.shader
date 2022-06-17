Shader "PostProcessing/Blend"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        
        [Enum(UnityEngine.Rendering.BlendMode)]
        _SourceBlendMode("Source Blend Mode", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]
        _DestinationBlendMode("Destination Blend Mode", Float) = 0
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        Blend [_SourceBlendMode] [_DestinationBlendMode]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _LENSDIRT

            #include "UnityCG.cginc"

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            #if _LENSDIRT
                sampler2D _LensDirtTex;
            #endif

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                #if _LENSDIRT
                    float aspect = _ScreenParams.x / _ScreenParams.y;
                    float2 uvMod = float2(aspect, 1);
                    col *= tex2D(_LensDirtTex, i.uv * uvMod);
                #endif
                
                return col;
            }
            ENDCG
        }
    }
}
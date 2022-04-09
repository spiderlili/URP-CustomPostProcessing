// TODO: 2 pass shares 1 v2f, downsampling pass does not use up all of the struct => performance lost. make separate v2f! 
Shader "PostProcessing/DualBlur(Kawase)"
{
  Properties
  {
   [HideInInspector]_MainTex("MainTex",2D)="white"{}
   [HideInInspector]_BlurRadiusOffset("Blur Offset",float) = 3
  }

  SubShader
  {
   Tags
    {
     "RenderPipeline"="UniversalRenderPipeline"
    }
   Cull Off ZWrite Off ZTest Always

   HLSLINCLUDE
   
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

   CBUFFER_START(UnityPerMaterial)
   float4 _MainTex_ST;
   float4 _MainTex_TexelSize;
   float _BlurRadiusOffset;
   CBUFFER_END

   TEXTURE2D( _MainTex);
   SAMPLER(sampler_MainTex);

   struct a2v
   {
    float4 positionOS:POSITION;
    float2 texcoord:TEXCOORD;
   };

   struct v2f_UpSample
   {
    float4 positionCS:SV_POSITION;
    float4 texcoord[4]:TEXCOORD;
   };

   struct v2f_DownSample
   {
    float4 positionCS:SV_POSITION;
    float4 texcoord[3]:TEXCOORD;
   };
   
  ENDHLSL

   pass//Downsampling
   {
    NAME"DownSampling"
    HLSLPROGRAM
    #pragma vertex vert
    #pragma fragment frag

    v2f_DownSample vert(a2v i)
    {
     v2f_DownSample o;
     o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
     o.texcoord[2].xy=i.texcoord;
     o.texcoord[0].xy=i.texcoord+float2(1,1)*_MainTex_TexelSize.xy*(1+_BlurRadiusOffset)*0.5;
     o.texcoord[0].zw=i.texcoord+float2(-1,1)*_MainTex_TexelSize.xy*(1+_BlurRadiusOffset)*0.5;
     o.texcoord[1].xy=i.texcoord+float2(1,-1)*_MainTex_TexelSize.xy*(1+_BlurRadiusOffset)*0.5;
     o.texcoord[1].zw=i.texcoord+float2(-1,-1)*_MainTex_TexelSize.xy*(1+_BlurRadiusOffset)*0.5;
     return o;
    }

    half4 frag(v2f_DownSample i):SV_TARGET
    {
     half4 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.texcoord[2].xy)*0.5;
     for(int t=0;t<2;t++)
     {
      tex+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.texcoord[t].xy)*0.125;
      tex+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.texcoord[t].zw)*0.125;
     }
     return tex;
    }
    ENDHLSL
   }
   
   pass //Upsampling
   {
    NAME"UpSampling"
    HLSLPROGRAM
    #pragma vertex vert
    #pragma fragment frag

    v2f_UpSample vert(a2v i){
    v2f_UpSample o;
    o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
    o.texcoord[0].xy=i.texcoord+float2(1,1)*_MainTex_TexelSize.xy*(1 + _BlurRadiusOffset)*0.5;
    o.texcoord[0].zw=i.texcoord+float2(-1,1)*_MainTex_TexelSize.xy*(1 + _BlurRadiusOffset)*0.5;
    o.texcoord[1].xy=i.texcoord+float2(1,-1)*_MainTex_TexelSize.xy*(1 + _BlurRadiusOffset)*0.5;
    o.texcoord[1].zw=i.texcoord+float2(-1,-1)*_MainTex_TexelSize.xy*(1 + _BlurRadiusOffset)*0.5;
    o.texcoord[2].xy=i.texcoord+float2(0,2)*_MainTex_TexelSize.xy*(1 + _BlurRadiusOffset)*0.5;
    o.texcoord[2].zw=i.texcoord+float2(0,-2)*_MainTex_TexelSize.xy*(1 + _BlurRadiusOffset)*0.5;
    o.texcoord[3].xy=i.texcoord+float2(-2,0)*_MainTex_TexelSize.xy*(1 + _BlurRadiusOffset)*0.5;
    o.texcoord[3].zw=i.texcoord+float2(2,0)*_MainTex_TexelSize.xy*(1 + _BlurRadiusOffset)*0.5;
    return o;
    }

    half4 frag(v2f_UpSample i):SV_TARGET
    {
     half4 tex=0;
     for(int t=0;t<2;t++)
     {
      tex+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.texcoord[t].xy)/6;
      tex+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.texcoord[t].zw)/6;
     }
     for(int k=2;k<4;k++)
     {
      tex+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.texcoord[k].xy)/12;
      tex+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.texcoord[k].zw)/12;
     }
     return tex;
    }
    ENDHLSL
   }
  }
} 

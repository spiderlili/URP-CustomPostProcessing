Shader "PostProcessing/RapidGaussianBlur (BIRP)"
{
	Properties
	{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}

	SubShader
	{
		ZWrite Off
		Blend Off

		// Pass 0: Down Sample Pass
		Pass
		{
			ZTest Off
			Cull Off

			CGPROGRAM

			#pragma vertex vert_DownSmpl
			#pragma fragment frag_DownSmpl

			ENDCG

		}

		// Pass 1: Vertical Pass
		Pass
		{
			ZTest Always
			Cull Off

			CGPROGRAM
			#pragma vertex vert_BlurVertical
			#pragma fragment frag_Blur
			ENDCG
		}

		// Pass 2: Horizontal Pass
		Pass
		{
			ZTest Always
			Cull Off

			CGPROGRAM
			#pragma vertex vert_BlurHorizontal
			#pragma fragment frag_Blur
			ENDCG
		}
	}
	
	CGINCLUDE
	#include "UnityCG.cginc"

	sampler2D _MainTex;
	// Defined in UnityCG.cginc: the size of a texel of the texture
	uniform half4 _MainTex_TexelSize;
	// Controlled by C#
	uniform half _DownSampleValue;

	struct VertexInput
	{
		float4 vertex : POSITION;
		half2 texcoord : TEXCOORD0;
	};

	struct VertexOutput_DownSmpl
	{
		float4 pos : SV_POSITION;
		// right-up 
		half2 uv20 : TEXCOORD0;
		// left-down
		half2 uv21 : TEXCOORD1;
		// right-down
		half2 uv22 : TEXCOORD2;
		// left-up
		half2 uv23 : TEXCOORD3;
	};

	// Use a pre-computed sample Gaussian weights
	static const half4 GaussWeight[7] =
	{
		half4(0.0205,0.0205,0.0205,0),
		half4(0.0855,0.0855,0.0855,0),
		half4(0.232,0.232,0.232,0),
		half4(0.324,0.324,0.324,1),
		half4(0.232,0.232,0.232,0),
		half4(0.0855,0.0855,0.0855,0),
		half4(0.0205,0.0205,0.0205,0)
	};

	VertexOutput_DownSmpl vert_DownSmpl(VertexInput v)
	{
		// instance downsample output struct 
		VertexOutput_DownSmpl o;
		o.pos = UnityObjectToClipPos(v.vertex);
		
		// Downsample the image: take neighbouring pixels (up, down, left, right) around the current pixel & save them inside 4 uv sets
		o.uv20 = v.texcoord + _MainTex_TexelSize.xy* half2(0.5h, 0.5h);;
		o.uv21 = v.texcoord + _MainTex_TexelSize.xy * half2(-0.5h, -0.5h);
		o.uv22 = v.texcoord + _MainTex_TexelSize.xy * half2(0.5h, -0.5h);
		o.uv23 = v.texcoord + _MainTex_TexelSize.xy * half2(-0.5h, 0.5h);
		
		return o;
	}

	fixed4 frag_DownSmpl(VertexOutput_DownSmpl i) : SV_Target
	{
		// Define a temporary color value
		fixed4 color = fixed4(0,0,0,0);

		// add up the uv values at the 4 neighbouring pixels
		color += tex2D(_MainTex, i.uv20);
		color += tex2D(_MainTex, i.uv21);
		color += tex2D(_MainTex, i.uv22);
		color += tex2D(_MainTex, i.uv23);

		// calculate the final average value 
		return color / 4;
	}

	struct VertexOutput_Blur
	{
		float4 pos : SV_POSITION;
		half4 uv : TEXCOORD0;
		half2 offset : TEXCOORD1;
	};

	VertexOutput_Blur vert_BlurHorizontal(VertexInput v)
	{
		VertexOutput_Blur o;

		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = half4(v.texcoord.xy, 1, 1);
		// Calculate offset amount along x  
		o.offset = _MainTex_TexelSize.xy * half2(1.0, 0.0) * _DownSampleValue;

		return o;
	}

	VertexOutput_Blur vert_BlurVertical(VertexInput v)
	{
		VertexOutput_Blur o;
		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = half4(v.texcoord.xy, 1, 1);
		// Calculate offset amount along y 
		o.offset = _MainTex_TexelSize.xy * half2(0.0, 1.0) * _DownSampleValue;

		return o;
	}

	half4 frag_Blur(VertexOutput_Blur i) : SV_Target
	{
		half2 uv = i.uv.xy;

		// get offset amount 
		half2 OffsetWidth = i.offset;
		
		// offset 3 intervals from the centre, start to sum cumulative weights from leftmost (x) or uppermost (y) corner 
		half2 uv_withOffset = uv - OffsetWidth * 3.0;

		// Loop to obtain weighted color value 
		half4 color = 0;
		for (int j = 0; j< 7; j++)
		{
			// pixel value after uv offset
			half4 texCol = tex2D(_MainTex, uv_withOffset);
			// color += pixel value after offset x Gauss weight
			color += texCol * GaussWeight[j];
			// move to the next pixel, prepare the cumulative weight for the next iteration 
			uv_withOffset += OffsetWidth;
		}
		return color;
	}

	ENDCG

	FallBack Off
}

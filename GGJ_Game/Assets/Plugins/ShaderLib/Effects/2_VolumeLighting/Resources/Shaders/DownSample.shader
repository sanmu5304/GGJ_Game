Shader "ChillyRoom/VolumeLight/DownSampleDepth"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always
		 
		CGINCLUDE
		#define DOWNSAMPLE_DEPTH_MODE 2
		#include "UnityCG.cginc"	
		sampler2D _CameraDepthTexture;
		sampler2D _HalfResDepthBuffer;
		float4 _CameraDepthTexture_TexelSize;
		float4 _HalfResDepthBuffer_TexelSize;
		struct appdata
		{
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
		};
		struct v2fDownsample
		{
			float2 uv00 : TEXCOORD0;
			float2 uv01 : TEXCOORD1;
			float2 uv10 : TEXCOORD2;
			float2 uv11 : TEXCOORD3;
			float4 vertex : SV_POSITION;
		};
		
		float DownsampleDepth(v2fDownsample input, sampler2D depthTexture)
		{
			float4 depth;
			depth.x = tex2D(depthTexture, input.uv00).x;
			depth.y = tex2D(depthTexture, input.uv01).x;
			depth.z = tex2D(depthTexture, input.uv10).x;
			depth.w = tex2D(depthTexture, input.uv11).x;
#if DOWNSAMPLE_DEPTH_MODE == 0 // min  depth
            return min(min(depth.x, depth.y), min(depth.z, depth.w));
#elif DOWNSAMPLE_DEPTH_MODE == 1 // max  depth
            return max(max(depth.x, depth.y), max(depth.z, depth.w));
#elif DOWNSAMPLE_DEPTH_MODE == 2 // min/max depth in chessboard pattern

			float minDepth = min(min(depth.x, depth.y), min(depth.z, depth.w));
			float maxDepth = max(max(depth.x, depth.y), max(depth.z, depth.w));

			// chessboard pattern
			int2 position = input.vertex.xy % 2;
			int index = position.x + position.y;
			return index == 1 ? minDepth : maxDepth;
#endif
		}

		v2fDownsample vertDownsampleDepth(appdata v, float2 texelSize)
		{
			v2fDownsample o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.uv00 = v.uv - 0.5 * texelSize.xy;
			o.uv10 = o.uv00 + float2(texelSize.x, 0);
			o.uv01 = o.uv00 + float2(0, texelSize.y);
			o.uv11 = o.uv00 + texelSize.xy;
			return o;
		}



		ENDCG


		Pass
		{
			// method used to downsample depth buffer: 0 = min; 1 = max; 2 = min/max in chessboard pattern
	        
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
            #pragma target 2.0

			v2fDownsample vert(appdata v)
			{
                return vertDownsampleDepth(v, _CameraDepthTexture_TexelSize);
			}


			float frag(v2fDownsample input) : SV_Target
			{
                return DownsampleDepth(input, _CameraDepthTexture);
			}

			ENDCG
		}

		Pass
		{
			CGPROGRAM
            #pragma vertex vertQuarterDepth
            #pragma fragment frag
            #pragma target 2.0

			v2fDownsample vertQuarterDepth(appdata v)
			{
                return vertDownsampleDepth(v, _HalfResDepthBuffer_TexelSize);
			}

			float frag(v2fDownsample input) : SV_Target
			{
                return DownsampleDepth(input, _HalfResDepthBuffer);
			}

			ENDCG
		}
	}
}

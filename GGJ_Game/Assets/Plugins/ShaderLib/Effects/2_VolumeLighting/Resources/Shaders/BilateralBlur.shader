Shader "ChillyRoom/VolumeLight/BilateralBlur"
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
		#include "UnityCG.cginc"	
		#define PI 3.1415927f
		#define GAUSS_BLUR_DEVIATION 1.5   
		#define BLUR_DEPTH_FACTOR 0.5
		
		#define FULL_RES_BLUR_KERNEL_SIZE 7
		#define HALF_RES_BLUR_KERNEL_SIZE 5
		#define QUARTER_RES_BLUR_KERNEL_SIZE 6

		#define UPSAMPLE_DEPTH_THRESHOLD 1.5f

		sampler2D _MainTex;
		sampler2D _HalfMainTexture;
		sampler2D _QuaterMainTexture;

		sampler2D _CameraDepthTexture;
		sampler2D _HalfDepthTexture;
		sampler2D _QuaterDepthTexture;

        float4 _CameraDepthTexture_TexelSize;
		float4 _HalfDepthTexture_TexelSize;
		float4 _QuaterDepthTexture_TexelSize;

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

		struct v2fUpsample
		{
			float2 uv : TEXCOORD0;
			float2 uv00 : TEXCOORD1;
			float2 uv01 : TEXCOORD2;
			float2 uv10 : TEXCOORD3;
			float2 uv11 : TEXCOORD4;
			float4 vertex : SV_POSITION;
		};

		v2f vert(appdata v)
		{
			v2f o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.uv = v.uv;
			return o;
		}


		v2fUpsample vertUpsample(appdata v, float2 texelSize)
        {
            v2fUpsample o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;

            o.uv00 = v.uv - 0.5 * texelSize.xy;
            o.uv10 = o.uv00 + float2(texelSize.x, 0);
            o.uv01 = o.uv00 + float2(0, texelSize.y);
            o.uv11 = o.uv00 + texelSize.xy;
            return o;
        }

		float GaussianWeight(float offset, float deviation)
		{
			float weight = 1.0f / sqrt(2.0f * PI * deviation * deviation);
			weight *= exp(-(offset * offset) / (2.0f * deviation * deviation));
			return weight;
		}

		float4 BilateralBlur(v2f input, int2 direction,const int kernelRadius,sampler2D depthTex)
		{
			//const float deviation = kernelRadius / 2.5;
			const float deviation = kernelRadius / GAUSS_BLUR_DEVIATION; // make it really strong

			float2 uv = input.uv;
			float4 centerColor = tex2D(_MainTex, uv);
			float3 color = centerColor.xyz;
			//return float4(color, 1);
			float centerDepth = LinearEyeDepth(tex2D(depthTex, uv));

			float weightSum = 0;

			// gaussian weight is computed from constants only -> will be computed in compile time
            float weight = GaussianWeight(0, deviation);
			color *= weight;
			weightSum += weight;
			int i = 0;	
			for (i = -kernelRadius; i < 0; i += 1)
			{
                float2 offset = (direction * i)*_CameraDepthTexture_TexelSize.xy;
                float3 sampleColor = tex2D(_MainTex, uv+offset);
                float sampleDepth = LinearEyeDepth(tex2D(depthTex, uv+offset));

				float depthDiff = abs(centerDepth - sampleDepth);
                float dFactor = depthDiff * BLUR_DEPTH_FACTOR;
				float w = exp(-(dFactor * dFactor));

				// gaussian weight is computed from constants only -> will be computed in compile time
				weight = GaussianWeight(i, deviation) * w;

				color += weight * sampleColor;
				weightSum += weight;
			}

			for (i = 1; i <= kernelRadius; i += 1)
			{
				float2 offset = (direction * i)*_CameraDepthTexture_TexelSize.xy;
                float3 sampleColor = tex2D(_MainTex, uv+offset);
                float sampleDepth = LinearEyeDepth(tex2D(depthTex, uv+offset));

				float depthDiff = abs(centerDepth - sampleDepth);
                float dFactor = depthDiff * BLUR_DEPTH_FACTOR;
				float w = exp(-(dFactor * dFactor));
				
				// gaussian weight is computed from constants only -> will be computed in compile time
				weight = GaussianWeight(i, deviation) * w;

				color += weight * sampleColor;
				weightSum += weight;
			}

			color /= weightSum;
			return float4(color, centerColor.w);
		}


		float4 BilateralUpsample(v2fUpsample input, sampler2D hiDepth, sampler2D loDepth, sampler2D loColor)
		{
			const float threshold = UPSAMPLE_DEPTH_THRESHOLD;
			float4 highResDepth = LinearEyeDepth(tex2D(hiDepth, input.uv)).xxxx;
			float4 lowResDepth;
			
			lowResDepth[0] = LinearEyeDepth(tex2D(loDepth, input.uv00));
			lowResDepth[1] = LinearEyeDepth(tex2D(loDepth, input.uv10));
			lowResDepth[2] = LinearEyeDepth(tex2D(loDepth, input.uv01));
			lowResDepth[3] = LinearEyeDepth(tex2D(loDepth, input.uv11));

			float4 depthDiff = abs(lowResDepth - highResDepth);

			float accumDiff = dot(depthDiff, float4(1, 1, 1, 1));

			if (accumDiff < threshold) // small error, not an edge -> use bilinear filter
			{
				return tex2D(loColor, input.uv);
			}
            
			// find nearest sample
			float minDepthDiff = depthDiff[0];
			float2 nearestUv = input.uv00;

			if (depthDiff[1] < minDepthDiff)
			{
				nearestUv = input.uv10;
				minDepthDiff = depthDiff[1];
			}

			if (depthDiff[2] < minDepthDiff)
			{
				nearestUv = input.uv01;
				minDepthDiff = depthDiff[2];
			}

			if (depthDiff[3] < minDepthDiff)
			{
				nearestUv = input.uv11;
				minDepthDiff = depthDiff[3];
			}
			float4 col = tex2D(_MainTex, nearestUv);
			col.a =0;
			return col;
		}

		ENDCG

		//0: Full horizontal
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment horizontalFrag
			#pragma target 2.0
			
			fixed4 horizontalFrag(v2f input) : SV_Target
			{
                return BilateralBlur(input, int2(1, 0), FULL_RES_BLUR_KERNEL_SIZE,_CameraDepthTexture);
			}

			ENDCG
		}

		//1:  Full vertical
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment verticalFrag
            #pragma target 2.0
			
			fixed4 verticalFrag(v2f input) : SV_Target
			{
                return BilateralBlur(input, int2(0, 1), FULL_RES_BLUR_KERNEL_SIZE,_CameraDepthTexture);
			}

			ENDCG
		}

		//2: Half_Downsample horizontal
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment horizontalFrag
			#pragma target 2.0
			
			fixed4 horizontalFrag(v2f input) : SV_Target
			{
                return BilateralBlur(input, int2(1, 0), HALF_RES_BLUR_KERNEL_SIZE,_HalfDepthTexture);
			}

			ENDCG
		}

		//3:  Half_Downsample vertical
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment verticalFrag
            #pragma target 2.0
			
			fixed4 verticalFrag(v2f input) : SV_Target
			{
                return BilateralBlur(input, int2(0, 1), HALF_RES_BLUR_KERNEL_SIZE,_HalfDepthTexture);
			}

			ENDCG
		}

		//4: Half -> Full UpSample
		Pass
		{
			CGPROGRAM
				#pragma vertex vertUpsampleToFull
				#pragma fragment frag
				#pragma target 2.0


				v2fUpsample vertUpsampleToFull(appdata v)
				{
					return vertUpsample(v, _QuaterDepthTexture_TexelSize);
				}

				float4 frag(v2fUpsample input) : SV_Target
				{
					return BilateralUpsample(input, _CameraDepthTexture, _QuaterDepthTexture, _QuaterMainTexture);
				}

			ENDCG
		}


		//5: Quater_Downsample horizontal
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment horizontalFrag
			#pragma target 2.0
			
			fixed4 horizontalFrag(v2f input) : SV_Target
			{
                return BilateralBlur(input, int2(1, 0), QUARTER_RES_BLUR_KERNEL_SIZE,_HalfDepthTexture);
			}

			ENDCG
		}

		//6:  Quater_Downsample vertical
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment verticalFrag
            #pragma target 2.0
			
			fixed4 verticalFrag(v2f input) : SV_Target
			{
                return BilateralBlur(input, int2(0, 1), QUARTER_RES_BLUR_KERNEL_SIZE,_HalfDepthTexture);
			}

			ENDCG
		}

		//7: Quater-> half UpSample
		Pass
		{
			CGPROGRAM
				#pragma vertex vertUpsampleToFull
				#pragma fragment frag
				#pragma target 2.0


				v2fUpsample vertUpsampleToFull(appdata v)
				{
					return vertUpsample(v, _QuaterDepthTexture_TexelSize);
				}

				float4 frag(v2fUpsample input) : SV_Target
				{
					return BilateralUpsample(input, _CameraDepthTexture, _QuaterDepthTexture, _QuaterMainTexture);
				}

			ENDCG
		}
	}
}

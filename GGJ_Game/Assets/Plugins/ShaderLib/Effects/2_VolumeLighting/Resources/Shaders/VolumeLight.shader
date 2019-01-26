Shader "ChillyRoom/VolumeLight/RenderLight"
{
	Properties
	{
		[HideInInspector]_MainTex ("Texture", 2D) = "white" {}
		[HideInInspector]_LightColor("_LightColor", Color) = (1,1,1,1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		CGINCLUDE

			#pragma shader_feature SHADOWS_DEPTH
			#pragma shader_feature SPOT
			#pragma shader_feature XIAO_MI
			#pragma shader_feature NOISE
			#pragma shader_feature CONTRAST
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0

			#include "UnityCG.cginc"
			#include "AutoLight.cginc"

			sampler2D _NoiseTex;

			sampler2D _DitherTexture;
			sampler2D _LightCookieTex;
			float4 _LightColor;
			float4 _LightPos;
			sampler2D _CameraDepthTexture;
			sampler2D _LightDepthTexture;
			float4x4 _WorldViewProj;
			float4x4 _MyLightMatrix0;
			float4x4 _MyWorld2Shadow;

			float3 _CameraForward;

			// x: scattering coef, y: extinction coef, z: range w: skybox extinction coef
			float4 _VolumetricLight;
			// x: 1 - g^2, y: 1 + g^2, z: 2*g, w: 1/4pi
			float4 _MieG;

			// x: scale, y: intensity, z: intensity offset
			float4 _NoiseData;
			// x: x velocity, y: z velocity
			float4 _NoiseVelocity;
			//float4 _LightDir;
			float2 _halfResolution;
			int _SampleCount;
			float _Bias;
			float _Strength;
			float _Fallback;
			float _Contrast;

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				float3 wpos : TEXCOORD1;
				float4 spos : TEXCOORD2;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			v2f vert (appdata v)
			{
				v2f o;
				o.pos =  mul(_WorldViewProj, v.vertex);
				o.spos = mul(_WorldViewProj, v.vertex);
				
				//o.spos.y = o.spos.y*-540.0 + 540.0;
				//o.spos.x = o.spos.x *o.pos.w;
				

				o.uv = ComputeScreenPos(o.pos);
				o.wpos = mul(unity_ObjectToWorld, v.vertex);

				return o;
			}

			float MieScattering(float cosAngle, float4 g)
			{
				return g.w * (g.x / (pow(g.y - g.z * cosAngle, 1.5)));			
			}

			//计算贡献，如果在阴影范围内，贡献为0
			float GetLightAttenuation(float3 wpos,float2 cookieUV)
			{
				float atten = 1;

				float3 tolight = _LightPos.xyz - wpos;
				half3 lightDir = normalize(tolight);

				//apply cookie


				float4 uvCookie = mul(_MyLightMatrix0, float4(wpos, 1));
				// negative bias:  http://aras-p.info/blog/2010/01/07/screenspace-vs-mip-mapping/
				atten = tex2Dbias(_LightCookieTex,float4(uvCookie.xy,0,-2)).a;
				atten*= uvCookie.w<0;
				atten = tex2Dbias(_LightCookieTex, float4(cookieUV, 0, 0)).a;

				//float att = dot(tolight, tolight) * _LightPos.w;
				//atten *= tex2D(_LightTextureB0, att.rr).UNITY_ATTEN_CHANNEL;

				float4 shadowCoord = mul(_MyWorld2Shadow, float4(wpos, 1));
				//如果在阴影范围内，贡献为0
				float rZ = 1;
				float shadow = 1;
				//tex2DProj
				float2 realCoord = shadowCoord.xy/ shadowCoord.w;
				#if defined(UNITY_REVERSED_Z)
					rZ=-1;
				#endif
				shadow = (tex2D(_LightDepthTexture, realCoord.xy).r+_Bias)*rZ> (shadowCoord.z / shadowCoord.w)*rZ ? 1.0f:0.0f;
				atten *= shadow;
				return atten;
			}
		
			//_NoiseData: x: scale, z: intensity, w: intensity offset
			float GetDensity(float2 wpos)
			{
				float density = 1;
			#ifdef NOISE
				float noise = tex2D(_NoiseTex, wpos * _NoiseData.xy + float3(_Time.y * _NoiseVelocity.x, 0, _Time.y * _NoiseVelocity.y));
				//noise = tex2D(_NoiseTex, wpos.xy * _NoiseData.xy);
				noise = saturate(noise - _NoiseData.w) * _NoiseData.z;
				density = saturate(noise);
			#endif

				return density;
			}   

			//#define DITHER_8_8
			float4 RayMarch(float2 screenPos, float3 rayStart, float3 rayDir, float rayLength)
			{
			#ifdef DITHER_4_4
				float2 interleavedPos = (fmod(floor(screenPos.xy), 4.0));
				float offset = tex2D(_DitherTexture, interleavedPos / 4.0 + float2(0.5 / 4.0, 0.5 / 4.0)).w;
			#endif
			#ifdef DITHER_8_8
				//float2 interleavedPos = (fmod(floor(screenPos.xy), 8.0));
				//float2 interleavedUV = interleavedPos / 8.0;// + float2(0.5 / 8.0, 0.5 / 8.0);
				float2 interleavedUV = fmod(floor(screenPos),8.0);
				interleavedUV = interleavedUV/ 8.0 + float2(0.5 / 8.0, 0.5 / 8.0);
				//return float4(t,t,t,1);
				float offset = tex2D(_DitherTexture,interleavedUV).r;
			#else
				float2 interleavedUV = fmod(floor(screenPos),16.0);
				interleavedUV = interleavedUV/ 16.0 + float2(0.5 / 16.0, 0.5 / 16.0);
				//return float4(t,t,t,1);
				float offset = tex2D(_DitherTexture,interleavedUV).r;
			#endif
				int stepCount = _SampleCount;

				float stepSize = rayLength / stepCount;
				float3 step = rayDir * stepSize;
				float3 lightPos = mul(unity_ObjectToWorld, float4(0,0,0,1));//float3(0.57,2.13,2.65);
				float3 currentPosition = rayStart + step * offset+normalize(rayStart-lightPos)*0.25;

				float4 vlight = 0;

				float cosAngle;

				float extinction = length(_WorldSpaceCameraPos - currentPosition) * _VolumetricLight.y * 0.5;
				for (int i = 0; i < stepCount; ++i)
				{
					float2 cookieUV = float2(1 / rayStart.z * 0.5 * rayStart.x, 1 / rayStart.z * 0.5 * rayStart.y);
					cookieUV = cookieUV*0.5 + float2(0.5,0.5);
					float atten = GetLightAttenuation(currentPosition,cookieUV);
					
					screenPos = float2(screenPos.x/(_halfResolution.x*2),screenPos.y/(_halfResolution.y*2));
					float density =GetDensity(screenPos);
					
					float scattering = _VolumetricLight.x * stepSize * density;
					extinction += _VolumetricLight.y * stepSize * density;// +scattering;

				#ifdef CONTRAST
					//调高对比度
					atten =4*(2*atten-1);	//将0,1 映射到-1，1
				#endif
					float4 light = atten* scattering * exp(-extinction);

					// phase functino for spot and point lights
					float3 tolight = normalize(currentPosition - _LightPos.xyz);
					cosAngle = (dot(tolight, -rayDir));
					light *= MieScattering(cosAngle, _MieG);
					//light = cosAngle;
					vlight += light;

					currentPosition += step;
				}
				// apply light's color
				vlight *= _LightColor * _Strength;

				vlight = max(0, vlight);
				vlight.w = 0;
				return vlight;
			}

			//线段与平面的相交测试：P n*X = d，L = A + t*D, t = (d-n*A)/(n*D)
			float RayPlaneIntersect(in float3 planeNormal, in float planeD, in float3 rayOrigin, in float3 rayDir)
			{
				float NdotD = dot(planeNormal, rayDir);
				float NdotO = dot(planeNormal, rayOrigin);

				float t = -(NdotO + planeD) / NdotD;
				if (t < 0)
					t = 100000;
				return t;
			}

			//http://lousodrome.net/blog/light/2017/01/03/intersection-of-a-ray-and-a-cone/
			float2 RayConeIntersect(in float3 f3ConeApex, in float3 f3ConeAxis, in float fCosAngle, in float3 f3RayStart, in float3 f3RayDir)
			{
				float inf = 10000;
				f3RayStart -= f3ConeApex;
				float a = dot(f3RayDir, f3ConeAxis);
				float b = dot(f3RayDir, f3RayDir);
				float c = dot(f3RayStart, f3ConeAxis);
				float d = dot(f3RayStart, f3RayDir);
				float e = dot(f3RayStart, f3RayStart);
				fCosAngle *= fCosAngle;
				float A = a*a - b*fCosAngle;
				float B = 2 * (c*a - d*fCosAngle);
				float C = c*c - e*fCosAngle;
				float D = B*B - 4 * A*C;

				if (D > 0)
				{
					D = sqrt(D);
					float2 t = (-B + sign(A)*float2(-D, +D)) / (2 * A);
					bool2 b2IsCorrect = (c + a * t > 0?1:0) * (t > 0?1:0);
					t = t * b2IsCorrect + (1-b2IsCorrect) * (inf);
					return t;
				}
				else // no intersection
					return inf;
			}

			float OrthoLinearDepth(float d) {
				float linearDepth = d;
#if defined(UNITY_REVERSED_Z)
				linearDepth = 1 - linearDepth;
#endif
				linearDepth = linearDepth * (_ProjectionParams.z - _ProjectionParams.y);
				return linearDepth;
			}
			float PerspectiveLinearDepth(float d) {
				float linearDepth = Linear01Depth(d);
#if defined(UNITY_REVERSED_Z)
				//linearDepth = 1 - linearDepth;
#endif
				linearDepth = linearDepth * (_ProjectionParams.z - _ProjectionParams.y);
				return linearDepth;
			}

			float _CosAngle;
			float4 _ConeAxis;
			float4 _ConeApex;
			float _PlaneD;

		ENDCG

		Pass
		{
			ZTest Always
			
			Cull Back
			Blend One One

			CGPROGRAM
			fixed4 frag (v2f i) : SV_Target
			{
				half2 uv = i.uv.xy / i.uv.w;

				//重建worldpos
				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
				float3 rayStart = _WorldSpaceCameraPos;
				float3 rayEnd = i.wpos;

				float3 rayDir = (rayEnd - rayStart);
				float rayLength = length(rayDir); 

				rayDir /= rayLength;

				float3 r1 = rayEnd + rayDir * 0.015;

				float planeCoord = RayPlaneIntersect(_ConeAxis, _PlaneD, r1, rayDir);
				
				float2 lineCoords = RayConeIntersect(_ConeApex, _ConeAxis, _CosAngle, r1, rayDir);

				//得到_CameraForward方向的深度
				float linearDepth_O = OrthoLinearDepth(depth);
				float linearDepth_P = PerspectiveLinearDepth(depth);
				float linearDepth = lerp(linearDepth_P, linearDepth_O, unity_OrthoParams.w);

				//得到ray方向的depth ：_CameraForward/rayDir = pDepth/linearDepth
				float projectedDepth = linearDepth / dot(_CameraForward, rayDir);

				//如果有物体在灯光内，得到物体离灯光表面的距离,Z必须大于0！
				float z = max(0,projectedDepth - rayLength);

				//圆锥体只有两个面，所以同一个方向只有三种可能，穿过底部，穿过曲面，与曲面相切，取最短即可
				rayLength = min(planeCoord, min(lineCoords.x, lineCoords.y));
				//如果有物体在灯光内,太暗会有噪点在周围
				//rayLength = min(rayLength, z+((0.15*z/rayLength+0.15)*rayLength));
				
				rayLength = min(rayLength, max(z,0.8*rayLength));
				rayLength = min(rayLength, z);
				float a = 1;
				# ifdef UNITY_UV_STARTS_AT_TOP		//dx
					a = -1;
				#endif
				
				i.spos.x = i.spos.x/i.spos.w;
				i.spos.y = i.spos.y/i.spos.w;
				
				//mobile
				i.spos.xy = i.spos.xy*_halfResolution.xy + _halfResolution.xy;
				//PC
				i.spos.xy = i.pos.xy;
				float4 color = RayMarch(i.spos.xy, rayEnd, rayDir, rayLength);
				if (linearDepth > 0.999999)
				{
					color.w = lerp(color.w, 1, _VolumetricLight.w);
				}
				return pow(color,_Fallback);
			}
			ENDCG
		}

		Pass
		{
			ZTest Always
			Cull Front
			Blend One One
			CGPROGRAM
			fixed4 frag (v2f i) : SV_Target
			{
				half2 uv = i.uv.xy / i.uv.w;

				//重建worldpos
				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
				float3 rayStart = _WorldSpaceCameraPos;
				float3 rayEnd = i.wpos;

				float3 rayDir = (rayEnd - rayStart);
				float rayLength = length(rayDir); 

				rayDir /= rayLength;

				//得到_CameraForward方向的深度
				float linearDepth_O = OrthoLinearDepth(depth);
				float linearDepth_P = PerspectiveLinearDepth(depth);
				float linearDepth = lerp(linearDepth_P, linearDepth_O, unity_OrthoParams.w);

				//得到ray方向的depth ：_CameraForward/rayDir = pDepth/linearDepth
				float projectedDepth = linearDepth / dot(_CameraForward, rayDir);

				rayLength = min(rayLength, projectedDepth);
				
				float a = 1;
				# ifdef UNITY_UV_STARTS_AT_TOP		//dx
					a = -1;
				#endif
				
				i.spos.x = i.spos.x/i.spos.w;
				i.spos.y = i.spos.y/i.spos.w;
				
				//mobile
				i.spos.xy = i.spos.xy*_halfResolution.xy + _halfResolution.xy;
				//PC
				i.spos.xy = i.pos.xy;
				float4 color = RayMarch(i.spos.xy, rayStart, rayDir, rayLength);
				
				return color;
			
			}
			ENDCG
		}
	}
}

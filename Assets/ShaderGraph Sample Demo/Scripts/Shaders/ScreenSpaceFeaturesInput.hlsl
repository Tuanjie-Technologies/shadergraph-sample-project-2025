#ifndef SSFEATURE_INPUT_INCLUDED
#define SSFEATURE_INPUT_INCLUDED


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


half4 _HiZDepthParams;
half4 _ResolveParams;
half4 _SSRTexParams;
half4 _SSAOTexParams;
half4 _BlueNoiseParams;
half4 _DenoiserParams;
half4 _BloomParams;
half4 _PostProcessingParams;
half4 _LutParams;
half4 _CopyParams;
float4 _SSRParams;
    #define _MaxSteps _SSRParams.x
    #define _Thickness -_SSRParams.y
    #define _BRDFBias  _SSRParams.z
    #define _EdgeFade _SSRParams.w
half4 _SSAOParams;
    #define _Radius _SSAOParams.x
    #define _Slices _SSAOParams.y
    #define _Steps _SSAOParams.z
    #define _SSAO_Intensity _SSAOParams.w
half4 _BloomParams2;
	#define _BloomScatter _BloomParams2.x
	#define _BloomIntensity _BloomParams2.y
	#define _BloomThreshold _BloomParams2.z
	#define _BloomThresholdKnee _BloomParams2.w
half4 _DefaultValue;
float4 _UV2View;

int _SubIndex;
int _SingleChannel;
int _DownSampleTracing;
half _RadiusMultiplier;
half4 _PreBlurRotator;
half4 _BlurRotator;
half4 _PrevCamPos;
half _TaaFrameInfluence;
half _PostExposure;
half4 _HueSatCon;
half4 _ColorBalance;
half4 _ColorFilter;
int _MipLevel;
int _SwapBuffer;

half4x4 _InvPrevVP;


TEXTURE2D(_BlueNoise);
TEXTURE2D(_HiZDepthTexture);
TEXTURE2D(_ResolvedOpaqueTex);
TEXTURE2D(_HistoryTexture);
TEXTURE2D(_InputTexture);
TEXTURE2D(_AccumTexture);
TEXTURE2D(_HistoryDepthTexture);
TEXTURE2D(_HistoryAccumTexture);
TEXTURE2D(_MotionVectorTexture);
TEXTURE2D(_SourceTexture);
TEXTURE2D(_SourceTextureSC);
TEXTURE3D(_LutTex);
TEXTURE3D(_InternalLutTex);

RWTexture2D<float> _InputDepthTexture;
RWTexture2D<float> _HiZDepthBuffer;
RWTexture2D<float4> _OutputBuffer;
RWTexture2D<float> _AccumBuffer;
RWTexture2D<float4> _TracedResult;
RWTexture2D<float4> _TargetBuffer;
RWTexture2D<float> _TargetBufferSC;
RWTexture3D<float4> _LutBuffer;

SAMPLER(sampler_linear_clamp);
SAMPLER(s_point_clamp_sampler);
SAMPLER(s_point_repeat_sampler);
SAMPLER(sampler_LutTex);


struct BilateralData
{
    float4 irradiance;
    half3 normal;
	half3 posWS;
	float depth;
	half roughness;
    bool invalid;
};

float2 GetVelocityWithOffset(float2 uv, float2 depthOffsetUv)
{
	float2 offsetUv = SAMPLE_TEXTURE2D_LOD(_MotionVectorTexture, sampler_PointClamp, uv + _PostProcessingParams.zw * depthOffsetUv, 0).xy;
	return -offsetUv;
}

void AdjustBestDepthOffset(inout float bestDepth, inout float bestX, inout float bestY, float2 uv, float currX, float currY)
{
	float depth = SAMPLE_TEXTURE2D_LOD(_HiZDepthTexture, sampler_PointClamp, uv.xy + _PostProcessingParams.zw * float2(currX, currY), 0).r;

#if UNITY_REVERSED_Z
	depth = 1.0 - depth;
#endif

	bool isBest = depth < bestDepth;
	bestDepth = isBest ? depth : bestDepth;
	bestX = isBest ? currX : bestX;
	bestY = isBest ? currY : bestY;
}

float3 WorkingToPerceptual(float3 c)
{
	return c * rcp(c.x + 1.0);
}
float3 PerceptualToWorking(float3 c)
{
	return c *  rcp(1.0 - c.x);
}


#define POISSON_SAMPLE_COUNT 8
static const half3 k_PoissonDiskSamples[POISSON_SAMPLE_COUNT] =
{
    float3( -1.00             ,  0.00             , 1.0 ),
    float3(  0.00             ,  1.00             , 1.0 ),
    float3(  1.00             ,  0.00             , 1.0 ),
    float3(  0.00             , -1.00             , 1.0 ),
    float3( -0.25 * sqrt(2.0) ,  0.25 * sqrt(2.0) , 0.5 ),
    float3(  0.25 * sqrt(2.0) ,  0.25 * sqrt(2.0) , 0.5 ),
    float3(  0.25 * sqrt(2.0) , -0.25 * sqrt(2.0) , 0.5 ),
    float3( -0.25 * sqrt(2.0) , -0.25 * sqrt(2.0) , 0.5 )
};

half3 GetNormalFromBuffer(half3 normalBuffer)
{
    half3 normalWS;
    #ifdef _GBUFFER_NORMALS_OCT
        float2 remappedOctNormalWS = Unpack888ToFloat2(normalBuffer); 
        float2 octNormalWS = remappedOctNormalWS.xy * 2.0 - 1.0;
        normalWS = UnpackNormalOctQuadEncode(octNormalWS);
    #else
        normalWS = normalBuffer.xyz;
    #endif
    return SafeNormalize(normalWS);
}
float LinearEyeDepth(float z)
{
    return 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w);
}
float3 GetWorldPositionFromDepth(float depth, float2 uv)
{
    float4 posCS = float4(uv * 2 - 1, depth, 1);
    #if UNITY_UV_STARTS_AT_TOP
        posCS.y = -posCS.y;
    #endif

    float4 worldPos = mul(UNITY_MATRIX_I_VP, posCS);
    worldPos.xyz /= worldPos.w;
    return worldPos.xyz;
}

float3 GetPrevWorldPositionFromDepth(float depth, float2 uv)
{
    float4 posCS = float4(uv * 2 - 1, depth, 1);
    #if UNITY_UV_STARTS_AT_TOP
        posCS.y = -posCS.y;
    #endif

    float4 worldPos = mul(_InvPrevVP, posCS);
    worldPos.xyz /= worldPos.w;
    return worldPos.xyz;
}
float3 GetPositionSS(float3 worldPos)
{
    float4 PosSS = mul(UNITY_MATRIX_VP, float4(worldPos.xyz, 1));
    PosSS.xyz /= PosSS.w;
    #if UNITY_UV_STARTS_AT_TOP
        PosSS.y = -PosSS.y;
    #endif
    PosSS.xy = (PosSS.xy * 0.5f + 0.5f);
    return PosSS.xyz;
}

float3 GetPositionPrevSS(float3 worldPos)
{
    float4 PosSS = mul(_PrevViewProjMatrix, float4(worldPos.xyz, 1));
    PosSS.xyz /= PosSS.w;
    #if UNITY_UV_STARTS_AT_TOP
        PosSS.y = -PosSS.y;
    #endif
    PosSS.xy = (PosSS.xy * 0.5 + 0.5);
    return PosSS.xyz;
}
float3 UV2View(float2 uv, float depth)
{
    return float3((uv * _UV2View.xy + _UV2View.zw) * depth, depth);
}

half IntegrateArc_CosWeight(half2 h, half n, half cos_n)
{
    half2 Arc = -cos(2 * h - n) + cos_n + 2 * h * sin(n);
    return 0.25 * (Arc.x + Arc.y);
}

half GetGaussianWeight( half r )
{
    return exp( -0.66 * r * r ); 
}

float2 RotateVector(float4 rotator, float2 v)
{
    return v.x * rotator.xz + v.y * rotator.yw;
}

float4 GetSpecularDominantDirection( float3 N, float3 V, float linearRoughness)
{
    float NoV = abs( dot( N, V ) );
    float a = 0.298475 * log( 39.4115 - 39.0029 * linearRoughness );
    float dominantFactor = pow(saturate(1.0 - NoV), 10 ) * ( 1.0 - a ) + a;
    float3 R = reflect( -V, N );
    float3 D = lerp( N, R, dominantFactor);
    return float4(normalize(D) , dominantFactor );
}
float2x3 GetKernelBasis( float3 V, float3 N, float linearRoughness)
{
    float3x3 basis = GetLocalFrame(N);
    float3 T = basis[0];
    float3 B = basis[1];
    return float2x3( T, B );
}
float2 GetKernelSampleCoordinates(float3 offset, float3 posWS, float3 T, float3 B, float4 rotator)
{
    offset.xy = RotateVector(rotator, offset.xy);
    float3 offsetedPos = posWS + T * offset.x + B * offset.y;
	half2 posSS = GetPositionSS(offsetedPos).xy;
	return posSS;
}

half EdgeFade(half2 uv)
{
    half2 centeredUV = abs(uv * 2 - 1);
    half xFade = saturate((centeredUV.x - (1 - _EdgeFade))/(_EdgeFade));
    half yFade = saturate((centeredUV.y - (1 - _EdgeFade))/(_EdgeFade));
    return smoothstep(0.1, 1, 1 - max(xFade, yFade));
}


BilateralData GetBilateralData(float2 uv)
{
	BilateralData bilateral;
    bilateral.irradiance = 0;
    bilateral.normal = 0;
	bilateral.depth = 0;
	bilateral.posWS = 0;
	bilateral.roughness = 0;
    bilateral.invalid = false;

	float depth  = SAMPLE_TEXTURE2D_LOD(_HiZDepthTexture, s_point_clamp_sampler, uv, 0).x;
	if (depth == 0 || uv.x < 0 || uv.x >= 1 || uv.y < 0 || uv.y >= 1)
	{
		bilateral.invalid = true;
		return bilateral;
	}

	float linearDepth =  LinearEyeDepth(depth);

	half4 normalBuffer = SAMPLE_TEXTURE2D_LOD(_CameraNormalsTexture, s_point_clamp_sampler, uv, 0);
    half3 normalWS = GetNormalFromBuffer(normalBuffer.xyz);
	bilateral.roughness = 1 - normalBuffer.a;
	bilateral.irradiance = SAMPLE_TEXTURE2D_LOD(_InputTexture, sampler_linear_clamp, uv, 0);
	bilateral.normal = normalWS;
	bilateral.depth = linearDepth;
	bilateral.posWS = GetWorldPositionFromDepth(depth, uv);
	return bilateral;
}

half GetBilateralValue(BilateralData centerData, BilateralData neighbourData)
{
	float valueWeight = saturate(1 - abs(centerData.depth - neighbourData.depth));
	valueWeight *= saturate(dot(centerData.normal, neighbourData.normal));
	valueWeight *= saturate(1 - abs(centerData.roughness - neighbourData.roughness));
	float3 posDiff = centerData.posWS - neighbourData.posWS;
	valueWeight *= saturate(1 - abs(dot(posDiff, centerData.normal)));

	return valueWeight;
}

float4 ImportanceSampleGGX(float2 Xi, float Roughness)
{
    float m = Roughness * Roughness;
    float m2 = m * m;
		
    float Phi = 2 * PI * Xi.x;
				 
    float CosTheta = sqrt((1.0 - Xi.y) / (1.0 + (m2 - 1.0) * Xi.y));
    float SinTheta = sqrt(1.0 - CosTheta * CosTheta);
				 
    float3 H;
    H.x = SinTheta * cos(Phi);
    H.y = SinTheta * sin(Phi);
    H.z = CosTheta;

    float d = (CosTheta * m2 - CosTheta) * CosTheta + 1;
    float D = m2 / (PI * d * d);
    float pdf = D * CosTheta;

    return float4(H, pdf);
}

float3 TangentToWorld(float3 N, float3 H)
{
    float3 UpVector = abs(N.y) < 0.999999f ? half3(0.0, 1.0, 0.0) : half3(1.0, 0.0, 0.0);
    float3 T = normalize(cross(UpVector, N));
    float3 B = cross(N, T);
				 
    return float3((T * H.x) + (B * H.y) + (N * H.z));
}

half HiZSearchHit(float3 startPos, float3 rayDirSS, out float3 hitPos)
{
	hitPos = -1;

	// move origin to the center of the pixel
	float3 rayOrigin = startPos;
	float3 rcpRayDir = rcp(rayDirSS);
	int2 rayStep = int2(rcpRayDir.x >= 0 ? 1 : 0, rcpRayDir.y >= 0 ? 1 : 0);
	float3 raySign = float3(rcpRayDir.x >= 0 ? 1 : -1, rcpRayDir.y >= 0 ? 1 : -1, rcpRayDir.z >= 0 ? 1 : -1);
	bool rayTowardsEye = rayDirSS.z >= 0;
	if (rayTowardsEye)
		return 0;

	// calculate max distance
	float3 boundary = float3(rcpRayDir.x >= 0 ? _ScaledScreenParams.x - 0.5f : 0.5f,
							 rcpRayDir.y >= 0 ? _ScaledScreenParams.y - 0.5f : 0.5f,
							 rcpRayDir.z >= 0 ? 1 : -0.00000024);
	float3 rayBoxIntersection = (boundary - rayOrigin) * rcpRayDir;
	float tMax;
	{
		#if UNITY_REVERSED_Z
			tMax = max(max(rayBoxIntersection.x, rayBoxIntersection.y), rayBoxIntersection.z);
		#else
			tMax = min(min(rayBoxIntersection.x, rayBoxIntersection.y), rayBoxIntersection.z);
		#endif
	} 

	// origin offset
	float3 pixelIntersection = abs(0.5f * rcpRayDir.xyz);
	float offset = min(pixelIntersection.x, pixelIntersection.y);


	bool hit = false;
	bool miss = false;
	bool belowMip0 = false;
	int level = 0;
    half mask = 0;

	float count = 0;
    for (int i = 0; i < _MaxSteps; i++) 
    {
		hitPos = rayOrigin + offset * rayDirSS;

		// make sure it doesn't on edge of the pixel
		float2 toEdge = round(hitPos.xy) - hitPos.xy;
		float2 microOffset = clamp(raySign.xy * toEdge + 0.000488281f, 0, 0.000488281f);
		hitPos.xy += raySign.xy * microOffset;

		uint2 ID = (uint2)hitPos.xy >> level;


		/*     brick
			**** y ***
			*        x  rayDir: x+ y+
			z ********
		*/
		float4 brick;
		brick.z = _HiZDepthTexture.Load(uint3(ID, level)).x;
		brick.xy = (ID + rayStep) << level;
		brick.w = (1 + _Thickness) * brick.z + _Thickness / (1 + _Thickness);

		float4 distToBrick = (brick - rayOrigin.xyzz) * rcpRayDir.xyzz;
		float distToWall = min(distToBrick.x, distToBrick.y);
		float distToBase = distToBrick.z;
		float distToSurf = distToBrick.w;

		bool aboveSurf = hitPos.z >= brick.z;
		bool belowSurf = hitPos.z < brick.z;
		bool aboveBase = hitPos.z >= brick.w;
		bool belowBase = hitPos.z < brick.w;


 		bool inside = belowSurf && aboveBase;
		bool hitBase = (offset <= distToBase) && (distToBase <= distToWall);

		miss = belowMip0 && (inside);
		hit  = (level == 0) && (hitBase || inside);
		belowMip0 = (level == 0) && belowSurf;

		offset = hitBase ? distToBase : ((level != 0) && belowSurf) ? offset : distToWall;
		hitPos.z = brick.z;

		level += (hitBase || belowSurf) ? -1 : 1;
		level = clamp(level, 0, 7);
		count += 1;
		if (hit || miss || (offset > tMax))
			break;
    }

	miss = miss || hitPos.z <= 0 || hitPos.z >= 1;
	mask = (hit && !miss) ? 1 : 0;
    return mask;
}


float SSR(half3 viewDirWS, half3 normalWS, half3 posWS, float depth, float2 id, half2 blueNoise, half roughness, inout float4 hitInfo)
{
	hitInfo = -1;
	if (depth <= 0)
		return 0;

	half theta = blueNoise.x;
	float phi = lerp(blueNoise.y, 0, _BRDFBias);

	// reflection ray.
	float4 randomDir = ImportanceSampleGGX(float2(theta, phi), roughness);
	half3 reflectDir = reflect(-viewDirWS, TangentToWorld(normalWS.xyz, randomDir.xyz));
	half angle = dot(reflectDir, normalWS);
	if (angle <= 0)
		return 0;

	// calculate marching dir
	float3 offsetedPosSS = GetPositionSS(posWS + reflectDir.xyz);

	if (offsetedPosSS.z <= 0)   // out of boundary
		return 0;
	float3 rayOrigin = float3(float2(id), depth);
	float3 rayDirCS = offsetedPosSS.xyz * float3(_ScaledScreenParams.xy, 1) - rayOrigin;		
	if (length(rayDirCS.xy) == 0)
		return 0;
	// search for hit point
	float3 hitPos = -1;
	half mask = HiZSearchHit(rayOrigin, rayDirCS, hitPos);
	hitPos.xy /= _ScaledScreenParams.xy;
	hitInfo = float4(hitPos, mask);
	return randomDir.w;
}



half4 SampleColorPoint(float2 uv, float2 texelOffset)
{
	return SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_PointClamp, uv + _PostProcessingParams.zw * texelOffset, 0);
}

half4 SampleColorLinear(float2 uv, float2 texelOffset)
{
	return SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_LinearClamp, uv + _PostProcessingParams.zw * texelOffset, 0);
}

void AdjustColorBox(inout half3 boxMin, inout half3 boxMax, float2 uv, float currX, float currY)
{
	half3 color = RGBToYCoCg(SampleColorLinear(uv, float2(currX, currY)).xyz);
	boxMin = min(color, boxMin);
	boxMax = max(color, boxMax);
}

half3 ApplyHistoryColorLerp(half3 workingAccumColor, half3 workingCenterColor, float t)
{
	half3 perceptualAccumColor = WorkingToPerceptual(workingAccumColor);
	half3 perceptualCenterColor = WorkingToPerceptual(workingCenterColor);

	half3 perceptualDstColor = lerp(perceptualAccumColor, perceptualCenterColor, t);
	half3 workingDstColor = PerceptualToWorking(perceptualDstColor);

	return workingDstColor;
}

// for the sake of simplicity and performance, only use lower quality
half4 DoTemporalAA(float2 uv)
{

	half3 colorCenter = RGBToYCoCg(SampleColorPoint(uv, float2(0,0)).xyz);

	half3 boxMax = colorCenter;
	half3 boxMin = colorCenter;
	half3 moment1 = colorCenter;
	half3 moment2 = colorCenter * colorCenter;

	AdjustColorBox(boxMin, boxMax, uv, 0.0f, -1.0f);
	AdjustColorBox(boxMin, boxMax, uv, -1.0f, 0.0f);
	AdjustColorBox(boxMin, boxMax, uv, 1.0f, 0.0f);
	AdjustColorBox(boxMin, boxMax, uv, 0.0f, 1.0f);

	float bestOffsetX = 0.0f;
	float bestOffsetY = 0.0f;
	float bestDepth = 1.0f;
	AdjustBestDepthOffset(bestDepth, bestOffsetX, bestOffsetY, uv, 0.0f, 0.0f);
	AdjustBestDepthOffset(bestDepth, bestOffsetX, bestOffsetY, uv, 1.0f, 0.0f);
	AdjustBestDepthOffset(bestDepth, bestOffsetX, bestOffsetY, uv, 0.0f, -1.0f);
	AdjustBestDepthOffset(bestDepth, bestOffsetX, bestOffsetY, uv, -1.0f, 0.0f);
	AdjustBestDepthOffset(bestDepth, bestOffsetX, bestOffsetY, uv, 0.0f, 1.0f);

	float2 depthOffsetUv = float2(bestOffsetX, bestOffsetY);
	float2 velocity = GetVelocityWithOffset(uv, depthOffsetUv);

	float2 historyUv = uv + velocity;
	half3 accum = RGBToYCoCg(SAMPLE_TEXTURE2D_LOD(_HistoryTexture, sampler_PointClamp, historyUv, 0).xyz);

	half3 clampAccum = clamp(accum, boxMin, boxMax);

	half frameInfluence = any(abs(uv - 0.5 + velocity) > 0.5) ? 1 : _TaaFrameInfluence;

	half3 workingColor = ApplyHistoryColorLerp(clampAccum, colorCenter, frameInfluence);
	half3 dstSceneColor = YCoCgToRGB(workingColor);

	return half4(max(dstSceneColor, 0.0), 1.0);
}

#endif

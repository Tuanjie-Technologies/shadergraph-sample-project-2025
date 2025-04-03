#ifndef SCREENS_SPACE_FEATURE_FUNCTION_INCLUDED
#define SCREENS_SPACE_FEATURE_FUNCTION_INCLUDED

#if _SSR_ON
Texture2D _SSR_Texture;
#endif

TEXTURE2D(_HiZDepthTexture);
#ifndef UNITY_DECLARE_NORMALS_TEXTURE_INCLUDED
TEXTURE2D(_CameraNormalsTexture);
#endif
SAMPLER(s_point_clamp_sampler);

void SampleNormalBuffer_float(float2 uv, out float4 normalBuffer)
{
    normalBuffer = SAMPLE_TEXTURE2D_LOD(_CameraNormalsTexture, s_point_clamp_sampler, uv, 0);
}
void SampleNormalBuffer_half(half2 uv, out half4 normalBuffer)
{
    normalBuffer = SAMPLE_TEXTURE2D_LOD(_CameraNormalsTexture, s_point_clamp_sampler, uv, 0);
}
void LoadNormalBuffer_float(float2 id, out float4 normalBuffer)
{
    normalBuffer = _CameraNormalsTexture[id];
}
void LoadNormalBuffer_half(half2 id, out half4 normalBuffer)
{
    normalBuffer = _CameraNormalsTexture[id];
}
void ToLinearEyeDepth_float(float depth, out float linearEyeDepth)
{
    linearEyeDepth = 1.0 / (_ZBufferParams.z * depth + _ZBufferParams.w);
}
void ToLinearEyeDepth_half(half depth, out half linearEyeDepth)
{
    linearEyeDepth = 1.0 / (_ZBufferParams.z * depth + _ZBufferParams.w);
}
void LoadDepth_float(float2 id, out float depth)
{
    depth = _HiZDepthTexture.Load(uint3(id, 0)).x;
}
void LoadDepth_half(half2 id, out half depth)
{
    depth = _HiZDepthTexture.Load(uint3(id, 0)).x;
}
void LoadDepthLOD_float(float2 id, float LOD, out float depth)
{
    depth = _HiZDepthTexture.Load(uint3(id, LOD)).x;
}
void LoadDepthLOD_half(half2 id, float LOD, out half depth)
{
    depth = _HiZDepthTexture.Load(uint3(id, LOD)).x;
}
void HiZSearchDepth_float(float2 id, float2 rayStep, float level, out float depth, out float2 outputBrickID)
{
    uint2 ID = (uint2)id >> (uint)level;
    depth = _HiZDepthTexture.Load(uint3(ID, level)).x;
    uint2 brickID = (ID + (uint2)rayStep) << (uint)level;
    outputBrickID = brickID;
}
void HiZSearchDepth_half(half2 id, half2 rayStep, half level, out half depth, out half2 outputBrickID)
{
    uint2 ID = (uint2)id >> (uint)level;
    depth = _HiZDepthTexture.Load(uint3(ID, level)).x;
    uint2 brickID = (ID + (uint2)rayStep) << (uint)level;
    outputBrickID = brickID;
}
void WorldToCamera_float(float3 normal, out float3 normalVS)
{
    normalVS = mul(unity_WorldToCamera, half4(normal, 0)).xyz;
}
void WorldToCamera_half(half3 normal, out half3 normalVS)
{
    normalVS = mul(unity_WorldToCamera, half4(normal, 0)).xyz;
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
void GetPoissonDiskSample_float(float index, out float3 sample)
{
    sample = k_PoissonDiskSamples[index];
}
void GetPoissonDiskSample_half(half index, out half3 sample)
{
    sample = k_PoissonDiskSamples[index];
}

static const float offsets_H[] = { -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0 };
static const float weights_H[] = { 0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703, 0.19459459, 0.12162162, 0.05405405, 0.01621622 };
void BlurRadiusH_float(float index, out float weight, out float offset)
{
    index = clamp(index, 0, 8);
    weight = weights_H[index];
    offset = offsets_H[index];
}
void BlurRadiusH_half(half index, out half weight, out half offset)
{
    index = clamp(index, 0, 8);
    weight = weights_H[index];
    offset = offsets_H[index];
}
static const float offsets_V[] = { -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923 };
static const float weights_V[] = { 0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027 };
void BlurRadiusV_float(float index, out float weight, out float offset)
{
    index = clamp(index, 0, 4);
    weight = weights_V[index];
    offset = offsets_V[index];
}
void BlurRadiusV_half(half index, out half weight, out half offset)
{
    index = clamp(index, 0, 4);
    weight = weights_V[index];
    offset = offsets_V[index];
}


void OffsetUV5Tap_float(float2 uv, float2 texelSize, float index, out float2 outsideUV)
{
    const static float2 offsets[5] = 
    {
        float2(0, 0),
        float2(1, 1),
        float2(-1, -1),
        float2(-1, 1),
        float2(1, -1)
    };
    index = clamp(index, 0, 4);
    outsideUV = offsets[index] * texelSize + uv;
}

void OffsetUV5Tap_half(half2 uv, half2 texelSize, half index, out half2 outsideUV)
{
    const static half2 offsets[5] = 
    {
        half2(0, 0),
        half2(1, 1),
        half2(-1, -1),
        half2(-1, 1),
        half2(1, -1)
    };
    index = clamp(index, 0, 4);
    outsideUV = offsets[index] * texelSize + uv;
}
void Offset9Tap_float(float texelSize, float index, out float2 outputOffset)
{
    const float2 offsets[9] = 
    {
        float2(0, 0),
        float2(sqrt(texelSize), sqrt(texelSize)),
        float2(-sqrt(texelSize), -sqrt(texelSize)),
        float2(-sqrt(texelSize), sqrt(texelSize)),
        float2(sqrt(texelSize), -sqrt(texelSize)),
        float2(texelSize, 0),
        float2(-texelSize, 0),
        float2(0, texelSize),
        float2(0, -texelSize)
    };
    index = clamp(index, 0, 8);
    outputOffset = offsets[index];
}

void Offset9Tap_half(half texelSize, half index, out half2 outputOffset)
{
    const half2 offsets[9] = 
    {
        half2(0, 0),
        half2(sqrt(texelSize), sqrt(texelSize)),
        half2(-sqrt(texelSize), -sqrt(texelSize)),
        half2(-sqrt(texelSize), sqrt(texelSize)),
        half2(sqrt(texelSize), -sqrt(texelSize)),
        half2(texelSize, 0),
        half2(-texelSize, 0),
        half2(0, texelSize),
        half2(0, -texelSize)
    };
    index = clamp(index, 0, 8);
    outputOffset = offsets[index];
}

void Offset8Tap_float(float2 texelSize, float index, out float2 outputOffset)
{
    const float2 offsets[8] = 
    {
        float2(0, -texelSize.y),
        float2(0, texelSize.y),
        float2(texelSize.x, 0),
        float2(-texelSize.x, 0),
        float2(-texelSize.x, -texelSize.y),
        float2(texelSize.x, -texelSize.y),
        float2(-texelSize.x, texelSize.y),
        float2(texelSize.x, texelSize.y)
    };
    index = clamp(index, 0, 7);
    outputOffset = offsets[index];
}
void Offset8Tap_half(half2 texelSize, half index, out half2 outputOffset)
{
    const half2 offsets[8] = 
    {
        half2(0, -texelSize.y),
        half2(0, texelSize.y),
        half2(texelSize.x, 0),
        half2(-texelSize.x, 0),
        half2(-texelSize.x, -texelSize.y),
        half2(texelSize.x, -texelSize.y),
        half2(-texelSize.x, texelSize.y),
        half2(texelSize.x, texelSize.y)
    };
    index = clamp(index, 0, 7);
    outputOffset = offsets[index];
}

void Offset8TapWithWeight_float(float2 texelSize, float index, out float2 outputOffset, out float weight)
{
    const float2 offsets[8] = 
    {
        float2(0, -texelSize.y),
        float2(0, texelSize.y),
        float2(texelSize.x, 0),
        float2(-texelSize.x, 0),
        float2(-texelSize.x, -texelSize.y),
        float2(texelSize.x, -texelSize.y),
        float2(-texelSize.x, texelSize.y),
        float2(texelSize.x, texelSize.y)
    };
    static const float spaceWeights[8] = {
        0.1233, 0.1233,
        0.1233, 0.1233,
        0.0778, 0.0778,
        0.0778, 0.0778
    };

    index = clamp(index, 0, 7);
    outputOffset = offsets[index];
    weight = spaceWeights[index];
}
void Offset8TapWithWeight_half(half2 texelSize, half index, out half2 outputOffset, out half weight)
{
    const half2 offsets[8] = 
    {
        half2(0, -texelSize.y),
        half2(0, texelSize.y),
        half2(texelSize.x, 0),
        half2(-texelSize.x, 0),
        half2(-texelSize.x, -texelSize.y),
        half2(texelSize.x, -texelSize.y),
        half2(-texelSize.x, texelSize.y),
        half2(texelSize.x, texelSize.y)
    };
    static const half spaceWeights[8] = {
        0.1233, 0.1233,
        0.1233, 0.1233,
        0.0778, 0.0778,
        0.0778, 0.0778
    };

    index = clamp(index, 0, 7);
    outputOffset = offsets[index];
    weight = spaceWeights[index];
}

void GetScreenSpaceReflection_float(float2 screenPos, UnitySamplerState SS, out float4 indirectSpecular)
{
    #if _SSR_ON
    indirectSpecular = SAMPLE_TEXTURE2D_LOD(_SSR_Texture, SS.samplerstate, screenPos, 0);
    indirectSpecular.w = saturate(indirectSpecular.w);
    #else
    indirectSpecular = float4(0, 0, 0, 0);
    #endif
}
void GetScreenSpaceReflection_half(half2 screenPos, UnitySamplerState SS, out half4 indirectSpecular)
{
    #if _SSR_ON
    indirectSpecular = SAMPLE_TEXTURE2D_LOD(_SSR_Texture, SS.samplerstate , screenPos, 0);
    indirectSpecular.w = saturate(indirectSpecular.w);
    #else
    indirectSpecular = half4(0, 0, 0, 0);
    #endif
}


void GetScreenSpaceOcclusion_float(float2 screenPos, out float indirectAO, out float directAO)
{
    #if defined(_SSAO_ON) && !defined(_SURFACE_TYPE_TRANSPARENT)
        indirectAO = SAMPLE_TEXTURE2D_X(_ScreenSpaceOcclusionTexture, sampler_ScreenSpaceOcclusionTexture, screenPos).x + (1.0 - _AmbientOcclusionParam.x);
        directAO = lerp(1, indirectAO, _AmbientOcclusionParam.w);
    #else
        indirectAO = 1;
        directAO = 1;
    #endif
}
void GetScreenSpaceOcclusion_half(half2 screenPos, out half indirectAO, out half directAO)
{
    #if defined(_SSAO_ON) && !defined(_SURFACE_TYPE_TRANSPARENT)
        indirectAO = SAMPLE_TEXTURE2D_X(_ScreenSpaceOcclusionTexture, sampler_ScreenSpaceOcclusionTexture, screenPos).x + (1.0 - _AmbientOcclusionParam.x);
        directAO = lerp(1, indirectAO, _AmbientOcclusionParam.w);
    #else
        indirectAO = 1;
        directAO = 1;
    #endif    
}

#endif
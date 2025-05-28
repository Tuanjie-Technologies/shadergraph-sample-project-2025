#ifndef SCREENS_SPACE_FEATURE_UTILS_INCLUDED
#define SCREENS_SPACE_FEATURE_UTILS_INCLUDED



void ToLinearEyeDepth_float(float depth, out float linearEyeDepth)
{
    linearEyeDepth = 1.0 / (_ZBufferParams.z * depth + _ZBufferParams.w);
}
void ToLinearEyeDepth_half(half depth, out half linearEyeDepth)
{
    linearEyeDepth = 1.0 / (_ZBufferParams.z * depth + _ZBufferParams.w);
}
void ToLinear01Depth_float(float depth, out float linearEyeDepth)
{
    linearEyeDepth = 1.0 / (_ZBufferParams.x * depth + _ZBufferParams.y);
}
void ToLinear01Depth_half(half depth, out half linearEyeDepth)
{
    linearEyeDepth = 1.0 / (_ZBufferParams.x * depth + _ZBufferParams.y);
}

void ReverseZ_float(float inputDepth, out float depth)
{
    #if UNITY_REVERSED_Z
    depth = inputDepth;
    #else
    depth = 1 - inputDepth;
    #endif
}
void ReverseZ_half(half inputDepth, out half depth)
{
    #if UNITY_REVERSED_Z
    depth = inputDepth;
    #else
    depth = 1 - inputDepth;
    #endif
}
void ReversedZChoose_float(float on, float off, out float output)
{
    #if UNITY_REVERSED_Z
    output = on;
    #else
    output = off;
    #endif
}
void ReversedZChoose_half(half on, half off, out half output)
{
    #if UNITY_REVERSED_Z
    output = on;
    #else
    output = off;
    #endif
}
void StartsAtTop_float(float on, float off, out float output)
{
    #if UNITY_UV_STARTS_AT_TOP
    output = on;
    #else
    output = off;
    #endif
}
void StartsAtTop_half(half on, half off, out half output)
{
    #if UNITY_UV_STARTS_AT_TOP
    output = on;
    #else
    output = off;
    #endif
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

#endif
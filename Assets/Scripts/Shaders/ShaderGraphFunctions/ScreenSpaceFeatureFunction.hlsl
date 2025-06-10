#ifndef SCREENS_SPACE_FEATURE_FUNCTION_INCLUDED
#define SCREENS_SPACE_FEATURE_FUNCTION_INCLUDED

#if _SSR_ON
Texture2D _SSR_Texture;
#endif

TEXTURE2D_FLOAT(_MotionVectorTexture);
TEXTURE2D_FLOAT(_HistoryDepthTexture);
TEXTURE2D_FLOAT(_HiZDepthTexture);
#ifndef UNITY_DECLARE_NORMALS_TEXTURE_INCLUDED
TEXTURE2D(_CameraNormalsTexture);
#endif
SAMPLER(s_point_clamp_sampler);

void SampleMotionVector_float(float2 uv, out float2 motionVector)
{
    motionVector = SAMPLE_TEXTURE2D_LOD(_MotionVectorTexture, s_point_clamp_sampler, uv, 0).xy;
}
void SampleMotionVector_half(half2 uv, out half2 motionVector)
{
    motionVector = SAMPLE_TEXTURE2D_LOD(_MotionVectorTexture, s_point_clamp_sampler, uv, 0).xy;
}
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
void SampleDepth_float(float2 uv, out float depth)
{
    depth = SAMPLE_TEXTURE2D_LOD(_HiZDepthTexture, s_point_clamp_sampler, uv, 0).x;
}
void SampleDepth_half(half2 uv, out half depth)
{
    depth = SAMPLE_TEXTURE2D_LOD(_HiZDepthTexture, s_point_clamp_sampler, uv, 0).x;
}
void LoadHistoryDepth_float(float2 id, out float depth)
{
    depth = _HistoryDepthTexture.Load(uint3(id, 0)).x;
}
void LoadHistoryDepth_half(half2 id, out half depth)
{
    depth = _HistoryDepthTexture.Load(uint3(id, 0)).x;
}
void SampleHistoryDepth_float(float2 uv, out float depth)
{
    depth = SAMPLE_TEXTURE2D_LOD(_HistoryDepthTexture, s_point_clamp_sampler, uv, 0).x;
}
void SampleHistoryDepth_half(half2 uv, out half depth)
{
    depth = SAMPLE_TEXTURE2D_LOD(_HistoryDepthTexture, s_point_clamp_sampler, uv, 0).x;
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
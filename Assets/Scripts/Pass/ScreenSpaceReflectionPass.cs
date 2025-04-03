using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;
using System.ComponentModel;

internal class ScreenSpaceReflectionPass : ScriptableRenderPass
{

    // Settings
    ScreenSpaceRendererFeature.FeatureSettings featureSettings;

    // Materials
    Material m_ResolveLastFrameMat;
    Material m_TraceSSRMat;
    Material m_ResolveMat;
    Material m_TemporalBlendingMat;
    Material m_UnpackIntoAccumMat;
    Material m_UnpackIntoBufferMat;
    Material m_BilateralBlurMat;
    Material m_TemporalClippingMat;

    Material m_BilateralUpsampling;

    FeatureData data;
    Texture2D m_BlueNoise;
    ComputeShader cs;
    bool m_ShouldEscape = false;
    
    static int m_FrontBufferID = Shader.PropertyToID("_FrontBuffer");
    static int m_BackBufferID = Shader.PropertyToID("_BackBuffer");
    static int m_HitBufferID = Shader.PropertyToID("_HitBuffer");
    static int m_AccumBufferID = Shader.PropertyToID("_AccumBuffer");
    static int m_ResolvedBufferID = Shader.PropertyToID("_ResolvedBuffer");
    static int m_UpsampledBufferID = Shader.PropertyToID("_UpsampledBuffer");
    static int m_TempBufferID = Shader.PropertyToID("_TempBuffer");

    GlobalKeyword keyword = GlobalKeyword.Create("_SSR_ON");
    public ScreenSpaceReflectionPass(RenderPassEvent evn, ComputeShader cs)
    {
        renderPassEvent = evn;
        this.cs = cs;
    }

    public void SetMaterials(ref Material resolveLastFrame, ref Material traceSSR, ref Material resolve, ref Material blending, ref Material unpackIntoAccum, ref Material unpackIntoBuffer
    , ref Material bilateralBlur, ref Material temporalClipping, ref Material upsampling)
    {
        m_ResolveLastFrameMat = resolveLastFrame;
        m_TraceSSRMat = traceSSR;
        m_ResolveMat = resolve;
        m_TemporalBlendingMat = blending;
        m_UnpackIntoAccumMat = unpackIntoAccum;
        m_UnpackIntoBufferMat = unpackIntoBuffer;
        m_BilateralBlurMat = bilateralBlur;
        m_TemporalClippingMat = temporalClipping;
        m_BilateralUpsampling = upsampling;
    }
    public void SetResources(ref FeatureData featureData, ref Texture2D blueNoise, ref ScreenSpaceRendererFeature.FeatureSettings featureSettings)
    {
        data = featureData;
        m_BlueNoise = blueNoise;
        this.featureSettings = featureSettings;
    }

    public bool ShouldEscape(ref RenderingData renderingData)
    {        
        m_ShouldEscape = false;
        if (data == null || m_BlueNoise == null || !featureSettings.EnableSSR || renderingData.cameraData.cameraType > CameraType.SceneView)
        {
            m_ShouldEscape = true;
            return m_ShouldEscape; 
        }
        if (data.m_HistoryReflectionAccum == null || data.m_HistoryReflectionResult == null || data.m_HistoryOpaque == null || data.m_HistoryDepth == null)
        {
            m_ShouldEscape = true;
            return m_ShouldEscape; 
        }
        if (!featureSettings.UseComputeShader && (m_ResolveLastFrameMat == null || m_TraceSSRMat==null ||  m_ResolveMat == null || m_TemporalBlendingMat == null || m_UnpackIntoAccumMat == null || m_UnpackIntoBufferMat == null || m_BilateralBlurMat == null || m_TemporalClippingMat == null || m_BilateralUpsampling == null))
            return m_ShouldEscape;
        else if (cs == null)
            return m_ShouldEscape;

        if (!m_ShouldEscape)
            ConfigureInput(ScriptableRenderPassInput.Normal);
        return m_ShouldEscape;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get();

        if (m_ShouldEscape)
        {
            cmd.SetKeyword(keyword, false);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
            return;
        }

        int width = renderingData.cameraData.scaledWidth;
        int height = renderingData.cameraData.scaledHeight;
        int subIndex = (int)(data.frameCount % 4);
        if (featureSettings.DownSample)
        {
            width /= 2;
            height /= 2;
        }
        int dispatchW = (width + 8 - 1) / 8;
        int dispatchH = (height + 8 - 1) / 8;

        if (SystemInfo.supportsAsyncCompute && featureSettings.UseComputeShader)
            cmd.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        cmd.BeginSample("SSR");
        if (featureSettings.UseComputeShader)
        {
            cmd.SetComputeIntParam(cs, "_SubIndex", subIndex);
            cmd.SetComputeIntParam(cs, "_DownSampleTracing", featureSettings.DownSample ? 1 : 0);
            cmd.SetComputeIntParam(cs, "_SingleChannel", 0);
            cmd.SetComputeVectorParam(cs, "_SSRParams", new Vector4(featureSettings.MaxSteps, featureSettings.Thickness, featureSettings.BRDFBias, featureSettings.EdgeFade));
            cmd.SetComputeVectorParam(cs, "_BlueNoiseParams", new Vector4(m_BlueNoise.width, m_BlueNoise.height, 1.0f / m_BlueNoise.width, 1.0f / m_BlueNoise.height));
            cmd.SetComputeVectorParam(cs, "_PrevCamPos", data.prevCameraPos);
            cmd.SetComputeMatrixParam(cs, "_InvPrevVP", data.prevUnjitteredVP.inverse);

            int rotatorIndex = data.frameCount % 32;
            cmd.SetComputeVectorParam(cs, "_PreBlurRotator", EvaluateRotator(k_PreBlurRands[rotatorIndex]));
            cmd.SetComputeVectorParam(cs, "_BlurRotator", EvaluateRotator(k_BlurRands[rotatorIndex]));
        }
        else
        {
            cmd.SetGlobalInt("_SubIndex", subIndex);
            cmd.SetGlobalInt("_DownSampleTracing", featureSettings.DownSample ? 1 : 0);
            cmd.SetGlobalVector("_SSRParams", new Vector4(featureSettings.MaxSteps, featureSettings.Thickness, featureSettings.BRDFBias, featureSettings.EdgeFade));
            cmd.SetGlobalVector("_BlueNoiseParams", new Vector4(m_BlueNoise.width, m_BlueNoise.height, 1.0f / m_BlueNoise.width, 1.0f / m_BlueNoise.height));
            cmd.SetGlobalVector("_PrevCamPos", data.prevCameraPos);
            cmd.SetGlobalMatrix("_InvPrevVP", data.prevUnjitteredVP.inverse);
            cmd.SetGlobalVector("_FullScreenParams", new Vector4(renderingData.cameraData.scaledWidth, renderingData.cameraData.scaledHeight, 1.0f / renderingData.cameraData.scaledWidth, 1.0f / renderingData.cameraData.scaledHeight));
            int rotatorIndex = data.frameCount % 32;
            cmd.SetGlobalVector("_PreBlurRotator", EvaluateRotator(k_PreBlurRands[rotatorIndex]));
            cmd.SetGlobalVector("_BlurRotator", EvaluateRotator(k_BlurRands[rotatorIndex]));
        }


        GraphicsFormat graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
        cmd.GetTemporaryRT(m_FrontBufferID, width, height, 0, FilterMode.Point, graphicsFormat, 1, featureSettings.UseComputeShader);
        cmd.GetTemporaryRT(m_BackBufferID, width, height, 0, FilterMode.Point, graphicsFormat, 1, featureSettings.UseComputeShader);
        cmd.GetTemporaryRT(m_ResolvedBufferID,  width, height, 0, FilterMode.Point, graphicsFormat, 1, featureSettings.UseComputeShader);
        cmd.GetTemporaryRT(m_HitBufferID, width, height, 0, FilterMode.Point, graphicsFormat, 1, featureSettings.UseComputeShader);
        graphicsFormat = SystemInfo.GetCompatibleFormat(GraphicsFormat.R8_UNorm, FormatUsage.LoadStore);
        cmd.GetTemporaryRT(m_AccumBufferID, width, height, 0, FilterMode.Point, graphicsFormat, 1, featureSettings.UseComputeShader);

        int pass = (int)ScreenSpaceRendererFeature.Pass.ResolveLastFrame;

        if (featureSettings.UseComputeShader)
        {
            cmd.SetComputeTextureParam(cs, pass, "_HistoryTexture", data.m_HistoryOpaque);
            cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_ResolvedBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_HistoryDepthTexture", data.m_HistoryDepth);
            cmd.SetComputeVectorParam(cs, "_ResolveParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
        }
        else
        {
            cmd.SetRenderTarget(m_ResolvedBufferID);
            cmd.SetGlobalTexture("_HistoryTexture", data.m_HistoryOpaque);
            cmd.SetGlobalTexture("_HistoryDepthTexture", data.m_HistoryDepth);
            cmd.SetGlobalVector("_ResolveParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.DrawProcedural(Matrix4x4.identity, m_ResolveLastFrameMat, 0, MeshTopology.Triangles, 4);
        }
 

        // trace
        if (featureSettings.UseComputeShader)
        {
            pass = (int)ScreenSpaceRendererFeature.Pass.SSR;
            cmd.SetComputeTextureParam(cs, pass, "_BlueNoise", m_BlueNoise);
            cmd.SetComputeVectorParam(cs, "_SSRTexParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.SetComputeTextureParam(cs, pass, "_TracedResult", m_HitBufferID);
            cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
        }
        else
        {
            cmd.SetRenderTarget(m_HitBufferID);
            cmd.SetGlobalTexture("_BlueNoise", m_BlueNoise);
            cmd.SetGlobalVector("_SSRTexParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.DrawProcedural(Matrix4x4.identity, m_TraceSSRMat, 0, MeshTopology.Triangles, 4);
        }

        // resolve
        if (featureSettings.UseComputeShader)
        {
            pass = (int)ScreenSpaceRendererFeature.Pass.SSR_Resolve;
            cmd.SetComputeTextureParam(cs, pass, "_BlueNoise", m_BlueNoise);
            cmd.SetComputeTextureParam(cs, pass, "_ResolvedOpaqueTex", m_ResolvedBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_InputTexture", m_HitBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_TracedResult", m_FrontBufferID);
            cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
        }
        else
        {
            cmd.SetRenderTarget(m_FrontBufferID);
            cmd.SetGlobalTexture("_ResolvedOpaqueTex", m_ResolvedBufferID);
            cmd.SetGlobalTexture("_InputTexture", m_HitBufferID);
            cmd.DrawProcedural(Matrix4x4.identity, m_ResolveMat, 0, MeshTopology.Triangles, 4);
        }


        // Denoiser
        if (featureSettings.UseComputeShader)
        {
            pass = (int)ScreenSpaceRendererFeature.Pass.SSR_TemporalBlending;
            cmd.SetComputeTextureParam(cs, pass, "_InputTexture", m_FrontBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_ResolvedOpaqueTex", m_ResolvedBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_HistoryTexture", data.m_HistoryReflectionResult);
            cmd.SetComputeTextureParam(cs, pass, "_HistoryDepthTexture", data.m_HistoryDepth);
            cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_BackBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_AccumTexture", data.m_HistoryReflectionAccum);
            cmd.SetComputeTextureParam(cs, pass, "_AccumBuffer", m_AccumBufferID);
            cmd.SetComputeVectorParam(cs, "_DenoiserParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
        }
        else
        {
            cmd.GetTemporaryRT(m_TempBufferID, width, height, 0, FilterMode.Point, GraphicsFormat.R16G16B16A16_SFloat, 1, false);
            cmd.SetRenderTarget(m_TempBufferID);
            cmd.SetGlobalTexture("_InputTexture", m_FrontBufferID);
            cmd.SetGlobalTexture("_ResolvedOpaqueTex", m_ResolvedBufferID);
            cmd.SetGlobalTexture("_HistoryTexture", data.m_HistoryReflectionResult);
            cmd.SetGlobalTexture("_HistoryDepthTexture", data.m_HistoryDepth);
            cmd.SetGlobalTexture("_AccumTexture", data.m_HistoryReflectionAccum);
            cmd.SetGlobalVector( "_DenoiserParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.DrawProcedural(Matrix4x4.identity, m_TemporalBlendingMat, 0, MeshTopology.Triangles, 4);
            cmd.SetRenderTarget(m_AccumBufferID);
            cmd.SetGlobalTexture("_InputTexture", m_TempBufferID);
            cmd.DrawProcedural(Matrix4x4.identity, m_UnpackIntoAccumMat, 0, MeshTopology.Triangles, 4);
            cmd.SetRenderTarget(m_BackBufferID);
            cmd.DrawProcedural(Matrix4x4.identity, m_UnpackIntoBufferMat, 0, MeshTopology.Triangles, 4);
        }


        int dispathBlurW = (width + 32 - 1) / 32;
        int dispathBlurH = (height + 32 - 1) / 32;
        if (featureSettings.UseComputeShader)
        {
            pass = (int)ScreenSpaceRendererFeature.Pass.SSR_BilateralBlur;
            cmd.SetComputeTextureParam(cs, pass, "_AccumTexture",m_AccumBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_InputTexture", m_BackBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_FrontBufferID);
            cmd.DispatchCompute(cs, pass, dispathBlurW, dispathBlurH, 1);
        }
        else
        {
            cmd.SetGlobalTexture("_AccumTexture",m_AccumBufferID);
            cmd.SetGlobalTexture("_InputTexture", m_BackBufferID);
            cmd.SetRenderTarget(m_FrontBufferID);
            cmd.DrawProcedural(Matrix4x4.identity, m_BilateralBlurMat, 0, MeshTopology.Triangles, 4);
        }

        
        if (featureSettings.UseComputeShader)
        {
            pass = (int)ScreenSpaceRendererFeature.Pass.TemporalClipping;
            cmd.SetComputeTextureParam(cs, pass, "_InputTexture", m_FrontBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_BackBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_HistoryTexture", data.m_HistoryReflectionResult);
            cmd.SetComputeTextureParam(cs, pass, "_HistoryDepthTexture", data.m_HistoryDepth);
            cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
        }
        else
        {
            cmd.SetRenderTarget(m_BackBufferID);
            cmd.SetGlobalInt("_SingleChannel", 0);
            cmd.SetGlobalTexture("_InputTexture", m_FrontBufferID);
            cmd.SetGlobalTexture("_HistoryTexture", data.m_HistoryReflectionResult);
            cmd.DrawProcedural(Matrix4x4.identity, m_TemporalClippingMat, 0, MeshTopology.Triangles, 4);
        }


        // Copy History
        if (featureSettings.UseComputeShader)
        {
            pass = (int)ScreenSpaceRendererFeature.Pass.CopyHistory;
            cmd.SetComputeTextureParam(cs, pass, "_SourceTexture", m_BackBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_TargetBuffer", data.m_HistoryReflectionResult);
            cmd.SetComputeVectorParam(cs, "_CopyParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);

            pass = (int)ScreenSpaceRendererFeature.Pass.CopyHistorySingleChannel;
            cmd.SetComputeTextureParam(cs, pass, "_SourceTextureSC", m_AccumBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_TargetBufferSC", data.m_HistoryReflectionAccum);
            cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
        }
        else
        {
            cmd.Blit(m_BackBufferID, data.m_HistoryReflectionResult);
            cmd.Blit(m_AccumBufferID, data.m_HistoryReflectionAccum);
        }

        if (featureSettings.DownSample)
        {
            width = renderingData.cameraData.scaledWidth;
            height = renderingData.cameraData.scaledHeight;
            dispatchW = (width + 8 - 1) / 8;
            dispatchH = (height + 8 - 1) / 8;
            graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            pass = (int)ScreenSpaceRendererFeature.Pass.Upsampling;
            cmd.GetTemporaryRT(m_UpsampledBufferID, width, height, 0, FilterMode.Point, graphicsFormat, 1, featureSettings.UseComputeShader);
            if (featureSettings.UseComputeShader)
            {
                cmd.SetComputeVectorParam(cs, "_DefaultValue", new Vector4(0, 0, 0, 0));
                cmd.SetComputeTextureParam(cs, pass, "_InputTexture", m_BackBufferID);
                cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_UpsampledBufferID);
                cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
            }
            else
            {
                cmd.SetGlobalTexture("_InputTexture", m_BackBufferID);
                cmd.SetRenderTarget(m_UpsampledBufferID);
                cmd.DrawProcedural(Matrix4x4.identity, m_BilateralUpsampling, 0, MeshTopology.Triangles, 4);
            }

        }

        cmd.EndSample("SSR");

        cmd.SetGlobalTexture("_SSR_Texture", featureSettings.DownSample ? m_UpsampledBufferID : m_BackBufferID);
        cmd.SetKeyword(keyword, true);
        cmd.ReleaseTemporaryRT(m_HitBufferID);
        cmd.ReleaseTemporaryRT(m_FrontBufferID);
        cmd.ReleaseTemporaryRT(m_AccumBufferID);
        cmd.ReleaseTemporaryRT(m_ResolvedBufferID);

        data.UpdatePreviousCameraPos(renderingData.cameraData.worldSpaceCameraPos);

        if (SystemInfo.supportsAsyncCompute && featureSettings.UseComputeShader)
            context.ExecuteCommandBufferAsync(cmd, ComputeQueueType.Default);
        else
            context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    float4 EvaluateRotator(float rand)
    {
        float ca = Mathf.Cos(rand);
        float sa = Mathf.Sin(rand);
        return new float4(ca, sa, -sa, ca);
    }
    public static readonly float[] k_PreBlurRands = new float[] { 0.840188f, 0.394383f, 0.783099f, 0.79844f, 0.911647f, 0.197551f, 0.335223f, 0.76823f, 0.277775f, 0.55397f, 0.477397f, 0.628871f, 0.364784f, 0.513401f, 0.95223f, 0.916195f, 0.635712f, 0.717297f, 0.141603f, 0.606969f, 0.0163006f, 0.242887f, 0.137232f, 0.804177f, 0.156679f, 0.400944f, 0.12979f, 0.108809f, 0.998924f, 0.218257f, 0.512932f, 0.839112f };
    public static readonly float[] k_BlurRands = new float[] { 0.61264f, 0.296032f, 0.637552f, 0.524287f, 0.493583f, 0.972775f, 0.292517f, 0.771358f, 0.526745f, 0.769914f, 0.400229f, 0.891529f, 0.283315f, 0.352458f, 0.807725f, 0.919026f, 0.0697553f, 0.949327f, 0.525995f, 0.0860558f, 0.192214f, 0.663227f, 0.890233f, 0.348893f, 0.0641713f, 0.020023f, 0.457702f, 0.0630958f, 0.23828f, 0.970634f, 0.902208f, 0.85092f };

    public override void FrameCleanup(CommandBuffer cmd)
    {
        base.FrameCleanup(cmd);
    }
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(m_BackBufferID);
        if (featureSettings.DownSample)
            cmd.ReleaseTemporaryRT(m_UpsampledBufferID);
    }
}
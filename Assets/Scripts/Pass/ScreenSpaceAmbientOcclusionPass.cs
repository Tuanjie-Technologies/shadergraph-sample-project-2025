using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;

internal class ScreenSpaceAmbientOcclusionPass : ScriptableRenderPass
{

    // Settings
    ScreenSpaceRendererFeature.FeatureSettings featureSettings;

    FeatureData data;
    Texture2D m_BlueNoise;
    ComputeShader cs;
    bool m_ShouldEscape;
    
    Material m_SSAOMat;
    Material m_BilateralBlurMat;
    Material m_TemporalClipping;
    Material m_Upsampling;

    static int m_FrontBufferID = Shader.PropertyToID("_SSAOFrontBuffer");
    static int m_BackBufferID = Shader.PropertyToID("_SSAOBackBuffer");
    static int m_UpsampledBufferID = Shader.PropertyToID("_SSAOUpsampledBuffer");

    GlobalKeyword keyword = GlobalKeyword.Create("_SSAO_ON");
    GlobalKeyword BlurH = GlobalKeyword.Create("_BLUR_H");

    public ScreenSpaceAmbientOcclusionPass(RenderPassEvent evn, ComputeShader cs)
    {
        renderPassEvent = evn;
        this.cs = cs;
    }

    public void SetMaterials(ref Material ssao, ref Material bilateralBlur, ref Material temporal, ref Material upsampling)
    {
        m_SSAOMat = ssao;
        m_BilateralBlurMat = bilateralBlur;
        m_TemporalClipping = temporal;
        m_Upsampling = upsampling;
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
        if (data == null || m_BlueNoise == null || !featureSettings.EnableSSAO || renderingData.cameraData.cameraType > CameraType.SceneView)
        {
            m_ShouldEscape = true;
            return m_ShouldEscape; 
        }
        if (data.m_HistoryOcclusionResult == null || data.m_HistoryDepth == null)
        {
            m_ShouldEscape = true;
            return m_ShouldEscape; 
        }
        if (!featureSettings.UseComputeShader && (m_SSAOMat == null || m_BilateralBlurMat == null || m_TemporalClipping == null || m_Upsampling == null))
        {
            m_ShouldEscape = true;
            return m_ShouldEscape;
        }
        else if(cs == null)
        {
            m_ShouldEscape = true;
            return m_ShouldEscape;
        }
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
        if (featureSettings.SSAO_DownSample)
        {
            width /= 2;
            height /= 2;
        }
        int dispatchW = (width + 8 - 1) / 8;
        int dispatchH = (height + 8 - 1) / 8;
        if (SystemInfo.supportsAsyncCompute && featureSettings.UseComputeShader)
            cmd.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        cmd.BeginSample("SSAO");
        float fovRad = renderingData.cameraData.camera.fieldOfView * Mathf.Deg2Rad;
        float invHalfTanFov = 1 / Mathf.Tan(fovRad * 0.5f);
        Vector2 focalLen = new Vector2(invHalfTanFov * ((float)(height) / (float)(width)), invHalfTanFov);
        Vector2 invFocalLen = new Vector2(1 / focalLen.x, 1 / focalLen.y);
        if (featureSettings.UseComputeShader)
        {
            cmd.SetComputeVectorParam(cs, "_UV2View", new Vector4(2 * invFocalLen.x, 2 * invFocalLen.y, -1 * invFocalLen.x, -1 * invFocalLen.y));
            cmd.SetComputeFloatParam(cs, "_RadiusMultiplier", featureSettings.TracingRadiusScalar);
            cmd.SetComputeIntParam(cs, "_SubIndex", subIndex);
            cmd.SetComputeIntParam(cs, "_SingleChannel", 1);
            cmd.SetComputeIntParam(cs, "_DownSampleTracing", featureSettings.SSAO_DownSample ? 1 : 0);
            cmd.SetComputeVectorParam(cs, "_SSAOParams", new Vector4(featureSettings.Radius, featureSettings.Slices, featureSettings.Steps, featureSettings.Intensity));
            cmd.SetComputeVectorParam(cs, "_BlueNoiseParams", new Vector4(m_BlueNoise.width, m_BlueNoise.height, 1.0f / m_BlueNoise.width, 1.0f / m_BlueNoise.height));
        }
        else
        {
            cmd.SetGlobalVector("_FullScreenParams", new Vector4(renderingData.cameraData.scaledWidth, renderingData.cameraData.scaledHeight, 1.0f / renderingData.cameraData.scaledWidth, 1.0f / renderingData.cameraData.scaledHeight));
            cmd.SetGlobalVector("_UV2View", new Vector4(2 * invFocalLen.x, 2 * invFocalLen.y, -1 * invFocalLen.x, -1 * invFocalLen.y));
            cmd.SetGlobalFloat("_RadiusMultiplier", featureSettings.TracingRadiusScalar);
            cmd.SetGlobalInt("_SubIndex", subIndex);
            cmd.SetGlobalInt("_SingleChannel", 1);
            cmd.SetGlobalInt("_DownSampleTracing", featureSettings.SSAO_DownSample ? 1 : 0);
            cmd.SetGlobalVector("_SSAOParams", new Vector4(featureSettings.Radius, featureSettings.Slices, featureSettings.Steps, featureSettings.Intensity));
            cmd.SetGlobalVector("_BlueNoiseParams", new Vector4(m_BlueNoise.width, m_BlueNoise.height, 1.0f / m_BlueNoise.width, 1.0f / m_BlueNoise.height));
        }

        GraphicsFormat graphicsFormat = SystemInfo.GetCompatibleFormat(GraphicsFormat.R8_UNorm, FormatUsage.LoadStore);
        cmd.GetTemporaryRT(m_FrontBufferID, width, height, 0, FilterMode.Bilinear, graphicsFormat, 1, featureSettings.UseComputeShader);
        cmd.GetTemporaryRT(m_BackBufferID, width, height, 0, FilterMode.Bilinear, graphicsFormat, 1, featureSettings.UseComputeShader);

        int pass = (int)ScreenSpaceRendererFeature.Pass.SSAO;
        if (featureSettings.UseComputeShader)
        {
            cmd.SetComputeTextureParam(cs, pass, "_BlueNoise", m_BlueNoise);
            cmd.SetComputeVectorParam(cs, "_SSAOTexParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.SetComputeTextureParam(cs, pass, "_TracedResult", m_FrontBufferID);
            cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
        }
        else
        {
            cmd.SetGlobalTexture("_BlueNoise", m_BlueNoise);
            cmd.SetGlobalVector("_SSAOTexParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.SetRenderTarget(m_FrontBufferID);
            cmd.DrawProcedural(Matrix4x4.identity, m_SSAOMat, 0, MeshTopology.Triangles, 4);
        }

        if (featureSettings.UseComputeShader)
        {
            pass = (int)ScreenSpaceRendererFeature.Pass.BilateralBlurHorizontal;
            cmd.SetComputeVectorParam(cs, "_DenoiserParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.SetComputeTextureParam(cs, pass, "_BlueNoise", m_BlueNoise);
            cmd.SetComputeTextureParam(cs, pass, "_InputTexture", m_FrontBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_BackBufferID);
            cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
        }
        else
        {
            cmd.SetGlobalVector("_DenoiserParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.SetGlobalTexture("_InputTexture", m_FrontBufferID);
            cmd.SetRenderTarget(m_BackBufferID);
            cmd.SetKeyword(BlurH, true);
            cmd.DrawProcedural(Matrix4x4.identity, m_BilateralBlurMat, 0, MeshTopology.Triangles, 4);
        }

        if (featureSettings.UseComputeShader)
        {
            pass = (int)ScreenSpaceRendererFeature.Pass.BilateralBlurVertical;
            cmd.SetComputeTextureParam(cs, pass, "_BlueNoise", m_BlueNoise);
            cmd.SetComputeTextureParam(cs, pass, "_InputTexture", m_BackBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_FrontBufferID);
            cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
        }
        else
        {
            cmd.SetGlobalTexture("_InputTexture", m_BackBufferID);
            cmd.SetRenderTarget(m_FrontBufferID);
            cmd.SetKeyword(BlurH, false);
            cmd.DrawProcedural(Matrix4x4.identity, m_BilateralBlurMat, 0, MeshTopology.Triangles, 4);
        }

        if (featureSettings.UseComputeShader)
        {
            pass = (int)ScreenSpaceRendererFeature.Pass.TemporalClipping;
            cmd.SetComputeVectorParam(cs, "_DenoiserParams",  new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.SetComputeTextureParam(cs, pass, "_InputTexture", m_FrontBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_BackBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_HistoryTexture", data.m_HistoryOcclusionResult);
            cmd.SetComputeTextureParam(cs, pass, "_HistoryDepthTexture", data.m_HistoryDepth);
            cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
        }
        else
        {
            cmd.SetGlobalVector("_DenoiserParams",  new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.SetGlobalTexture("_InputTexture", m_FrontBufferID);
            cmd.SetGlobalTexture("_HistoryTexture", data.m_HistoryOcclusionResult);
            cmd.SetGlobalTexture("_HistoryDepthTexture", data.m_HistoryDepth);
            cmd.SetRenderTarget(m_BackBufferID);
            cmd.DrawProcedural(Matrix4x4.identity, m_TemporalClipping, 0, MeshTopology.Triangles, 4);
        }


        // Copy History
        if (featureSettings.UseComputeShader)
        {
            pass = (int)ScreenSpaceRendererFeature.Pass.CopyHistory;
            cmd.SetComputeTextureParam(cs, pass, "_SourceTexture", m_BackBufferID);
            cmd.SetComputeTextureParam(cs, pass, "_TargetBuffer", data.m_HistoryOcclusionResult);
            cmd.SetComputeVectorParam(cs, "_CopyParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
            cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
        }
        else
        {
            cmd.Blit(m_BackBufferID, data.m_HistoryOcclusionResult);
        }


        if (featureSettings.DownSample)
        {
            width = renderingData.cameraData.scaledWidth;
            height = renderingData.cameraData.scaledHeight;
            dispatchW = (width + 8 - 1) / 8;
            dispatchH = (height + 8 - 1) / 8;
            graphicsFormat = SystemInfo.GetCompatibleFormat(GraphicsFormat.R8_UNorm, FormatUsage.LoadStore);
            pass = (int)ScreenSpaceRendererFeature.Pass.Upsampling;
            cmd.GetTemporaryRT(m_UpsampledBufferID, width, height, 0, FilterMode.Bilinear, graphicsFormat, 1, featureSettings.UseComputeShader);
            if (featureSettings.UseComputeShader)
            {
                cmd.SetComputeVectorParam(cs, "_DefaultValue", new Vector4(1, 0, 0, 0));
                cmd.SetComputeTextureParam(cs, pass, "_InputTexture", m_BackBufferID);
                cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_UpsampledBufferID);
                cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
            }
            else
            {
                cmd.SetGlobalTexture("_InputTexture", m_BackBufferID);
                cmd.SetRenderTarget(m_UpsampledBufferID);
                cmd.DrawProcedural(Matrix4x4.identity, m_Upsampling, 0, MeshTopology.Triangles, 4);
            }

        }

        cmd.EndSample("SSAO");

        cmd.SetGlobalTexture("_ScreenSpaceOcclusionTexture", featureSettings.DownSample ? m_UpsampledBufferID : m_BackBufferID);
        cmd.SetGlobalVector("_AmbientOcclusionParam", new Vector4(1, 0, 0, featureSettings.DirectionalStrength));
        cmd.SetKeyword(keyword, true);

        if (SystemInfo.supportsAsyncCompute && featureSettings.UseComputeShader)
            context.ExecuteCommandBufferAsync(cmd, ComputeQueueType.Default);
        else
            context.ExecuteCommandBuffer(cmd);

        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        base.FrameCleanup(cmd);
    }
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(m_FrontBufferID);
        cmd.ReleaseTemporaryRT(m_BackBufferID);
        if (featureSettings.DownSample)
            cmd.ReleaseTemporaryRT(m_UpsampledBufferID);
    }
}
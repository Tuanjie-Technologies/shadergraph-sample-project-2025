using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;


namespace UnityEngine.Rendering.Universal
{
    internal class PostProcessingPass : ScriptableRenderPass
    {
        ScreenSpaceRendererFeature.FeatureSettings settings;
        FeatureData data;
        ComputeShader cs;
        Material m_Material;
        Material m_BloomMat;
        Material m_LutPrepareMat;
        RenderTextureDescriptor desc;
        private RTHandle m_CopiedColor;
        private RTHandle m_LUT;
        private CustomRenderTexture m_LUTRT;

        GlobalKeyword prefilter = GlobalKeyword.Create("_PASS_PREFILTER");
        GlobalKeyword blurH = GlobalKeyword.Create("_PASS_BLURH");
        GlobalKeyword blurV = GlobalKeyword.Create("_PASS_BLURV");
        GlobalKeyword upsampling = GlobalKeyword.Create("_PASS_UPSAMPLING");
        GlobalKeyword taa = GlobalKeyword.Create("_TAA");
        GlobalKeyword bloom = GlobalKeyword.Create("_BLOOM");
        GlobalKeyword tonemapping = GlobalKeyword.Create("_TONEMAPPING");
        GlobalKeyword colorAdjustment = GlobalKeyword.Create("_COLOR_ADJUSTMENTS");

        static string samplerName = "Custom Post Processing";
        static int m_TempTAAID = Shader.PropertyToID("_TAATempTexture");
        static int m_FrontID = Shader.PropertyToID("_FrontTempTexture");
        static int m_BackID = Shader.PropertyToID("_BackTempTexture");
        private ProfilingSampler m_ProfilingSampler = new ProfilingSampler(samplerName);


        public PostProcessingPass(RenderPassEvent evn, ComputeShader cs)
        {
            renderPassEvent = evn;
            this.cs = cs;
        }

        public void SetMaterials(ref Material material, ref Material bloom, ref Material lutPrepare)
        {
            m_Material = material;
            m_BloomMat = bloom;
            m_LutPrepareMat = lutPrepare;
        }
        public void SetResources(ref ScreenSpaceRendererFeature.FeatureSettings featureSettings, ref FeatureData featureData)
        {
            settings = featureSettings;
            data = featureData;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ResetTarget();
            desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            desc.enableRandomWrite = settings.UseComputeShader;
            RenderingUtils.ReAllocateIfNeeded(ref m_CopiedColor, desc, name: "_FullscreenPassColorCopy");
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            bool activeAdjustments = (settings.PostExposure != 0) || (settings.ColorFilter != Color.white) || (settings.Contrast != 0) || (settings.HueShift != 0) || (settings.Saturation != 0) || (settings.WhiteBalance != 0);
            bool activeTonemapping = settings.EnableTonemapping && settings.TonemappingLut != null;
            bool activeTAA = data != null && settings.EnableTAA;
            if (activeTAA)
                activeTAA = data.m_HistoryTAAResult != null;
            if ((!settings.EnableBloom && !activeTonemapping && !activeTAA && !activeAdjustments) || renderingData.cameraData.cameraType > CameraType.SceneView)
                return;

            if (!settings.UseComputeShader && (m_Material == null || m_BloomMat == null || m_LutPrepareMat == null))
                return;
            else if(cs == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            int width = renderingData.cameraData.scaledWidth;
            int height = renderingData.cameraData.scaledHeight;
            int dispatchW = (width + 8 - 1) / 8;
            int dispatchH = (height + 8 - 1) / 8;

            if (SystemInfo.supportsAsyncCompute && settings.UseComputeShader)
                cmd.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {            
                if (!settings.UseComputeShader)
                    cmd.SetKeyword(taa, activeTAA);

                if (activeTAA)
                {
                    RenderTextureDescriptor taaDesc = desc;
                    taaDesc.enableRandomWrite = settings.UseComputeShader;
                    cmd.GetTemporaryRT(m_TempTAAID, taaDesc);        
                    int pass = (int)ScreenSpaceRendererFeature.Pass.TAA;
                    if (settings.UseComputeShader)
                    {
                        cmd.SetComputeVectorParam(cs, "_PostProcessingParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
                        cmd.SetComputeFloatParam(cs, "_TaaFrameInfluence", settings.FrameInfluencer);
                        cmd.SetComputeTextureParam(cs, pass, "_SourceTexture", renderingData.cameraData.renderer.cameraColorTargetHandle);
                        cmd.SetComputeTextureParam(cs, pass, "_HistoryTexture", data.m_HistoryTAAResult);
                        cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_TempTAAID);
                        cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
                    }
                    else
                    {
                        cmd.SetKeyword(taa, true);
                        cmd.SetGlobalFloat("_TaaFrameInfluence", settings.FrameInfluencer);
                        cmd.SetGlobalTexture("_SourceTexture", renderingData.cameraData.renderer.cameraColorTargetHandle);
                        cmd.SetGlobalTexture("_HistoryTexture", data.m_HistoryTAAResult);
                        cmd.SetRenderTarget(m_TempTAAID);
                        cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 4);
                    }



                    if (settings.UseComputeShader)
                    {
                        pass = (int)ScreenSpaceRendererFeature.Pass.CopyHistory;
                        cmd.SetComputeVectorParam(cs, "_CopyParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
                        cmd.SetComputeTextureParam(cs, pass, "_SourceTexture", m_TempTAAID);
                        cmd.SetComputeTextureParam(cs, pass, "_TargetBuffer", data.m_HistoryTAAResult);
                        cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
                    }
                    else
                        cmd.Blit(m_TempTAAID, data.m_HistoryTAAResult);
                    cmd.ReleaseTemporaryRT(m_TempTAAID);
                }
                
                bool swapBuffer = false;
                bool activeBloom = false;
                if (settings.EnableBloom)
                {
                    int DownSampledW = width >> 1;
                    int DownSampledH = height >> 1;
                    int maxInteration = Math.Min(Mathf.ClosestPowerOfTwo(Math.Min(DownSampledW, DownSampledH)), settings.MaxIterations);
                    if (maxInteration > 0 && settings.BloomIntensity > 0 && DownSampledH >= 32 && DownSampledW >= 32)
                    {
                        float threshold = Mathf.GammaToLinearSpace(settings.Threshold);
                        float thresholdKnee = 0.5f * threshold;
                        if (settings.UseComputeShader)
                        {
                            cmd.SetComputeVectorParam(cs, "_BloomParams", new Vector4(DownSampledW, DownSampledH, 1.0f / DownSampledW, 1.0f / DownSampledH));
                            cmd.SetComputeVectorParam(cs, "_BloomParams2", new Vector4(settings.Scatter, settings.BloomIntensity, threshold, thresholdKnee));
                        }
                        else
                        {
                            cmd.SetGlobalVector("_BloomParams", new Vector4(DownSampledW, DownSampledH, 1.0f / DownSampledW, 1.0f / DownSampledH));
                            cmd.SetGlobalVector("_BloomParams2", new Vector4(settings.Scatter, settings.BloomIntensity, threshold, thresholdKnee));
                        }


                        RenderTextureDescriptor bloomDesc = desc;
                        bloomDesc.width = DownSampledW;
                        bloomDesc.height = DownSampledH;
                        bloomDesc.useMipMap = true;
                        bloomDesc.autoGenerateMips = false;
                        cmd.GetTemporaryRT(m_FrontID, bloomDesc);
                        cmd.GetTemporaryRT(m_BackID, bloomDesc);

                        // prefilter
                        int pass = (int)ScreenSpaceRendererFeature.Pass.BloomPrefilter;
                        int bloomDispatchW = (DownSampledW + 8 - 1) / 8;
                        int bloomDispatchH = (DownSampledH + 8 - 1) / 8;
                        if (settings.UseComputeShader)
                        {
                            cmd.SetComputeTextureParam(cs, pass, "_SourceTexture", activeTAA ? data.m_HistoryTAAResult : renderingData.cameraData.renderer.cameraColorTargetHandle);
                            cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_FrontID);
                            cmd.DispatchCompute(cs, pass, bloomDispatchW, bloomDispatchH, 1);
                        }
                        else
                        {
                            cmd.SetGlobalTexture("_SourceTexture", activeTAA ? data.m_HistoryTAAResult : renderingData.cameraData.renderer.cameraColorTargetHandle);
                            cmd.SetRenderTarget(m_FrontID);
                            cmd.SetKeyword(prefilter, true);
                            cmd.SetKeyword(blurH, false);
                            cmd.SetKeyword(blurV, false);
                            cmd.SetKeyword(upsampling, false);
                            cmd.DrawProcedural(Matrix4x4.identity, m_BloomMat, 0, MeshTopology.Triangles, 4);
                        }
                        
                        // blur
                        int i = 0;
                        int BloomW = DownSampledW; int BloomH = DownSampledH;
                        for (i = 0; i < maxInteration; i++)
                        {
                            BloomW = BloomW >> 1; BloomH = BloomH >> 1;
                            if (BloomW < 8 || BloomH < 8)
                                break;
                            int BloomDispatchW = (BloomW + 8 - 1) / 8;
                            int BloomDispatchH = (BloomH  + 8 - 1) / 8;
                            if (settings.UseComputeShader)
                            {
                                pass = (int)ScreenSpaceRendererFeature.Pass.BloomBlurH;
                                cmd.SetComputeIntParam(cs, "_MipLevel", i);
                                cmd.SetComputeVectorParam(cs, "_BloomParams", new Vector4(BloomW, BloomH, 1.0f / BloomW, 1.0f / BloomH));
                                cmd.SetComputeTextureParam(cs, pass, "_SourceTexture", m_FrontID);
                                cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_BackID, i + 1);
                                cmd.DispatchCompute(cs, pass, BloomDispatchW, BloomDispatchH, 1);

                                pass = (int)ScreenSpaceRendererFeature.Pass.BloomBlurV;
                                cmd.SetComputeTextureParam(cs, pass, "_SourceTexture", m_BackID);
                                cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", m_FrontID, i + 1);
                                cmd.DispatchCompute(cs, pass, BloomDispatchW, BloomDispatchH, 1);
                            }
                            else
                            {
                                cmd.SetGlobalFloat("_MipLevel", i);
                                cmd.SetGlobalVector("_BloomParams", new Vector4(BloomW, BloomH, 1.0f / BloomW, 1.0f / BloomH));
                                cmd.SetGlobalTexture("_SourceTexture", m_FrontID);
                                cmd.SetRenderTarget(m_BackID, i + 1);
                                cmd.SetKeyword(prefilter, false);
                                cmd.SetKeyword(blurH, true);
                                cmd.SetKeyword(blurV, false);
                                cmd.SetKeyword(upsampling, false);
                                cmd.DrawProcedural(Matrix4x4.identity, m_BloomMat, 0, MeshTopology.Triangles, 4);

                                cmd.SetGlobalTexture("_SourceTexture", m_BackID);
                                cmd.SetRenderTarget(m_FrontID, i + 1);
                                cmd.SetKeyword(prefilter, false);
                                cmd.SetKeyword(blurH, false);
                                cmd.SetKeyword(blurV, true);
                                cmd.SetKeyword(upsampling, false);
                                cmd.DrawProcedural(Matrix4x4.identity, m_BloomMat, 0, MeshTopology.Triangles, 4);
                            }

                        }

                        // upsample
                        for(; i >= 1; i--)
                        {
                            BloomW = DownSampledW >> (i - 1); BloomH = DownSampledH >> (i - 1);
                            int BloomDispatchW = (BloomW + 8 - 1) / 8;
                            int BloomDispatchH = (BloomH  + 8 - 1) / 8;

                            if (settings.UseComputeShader)
                            {
                                pass = (int)ScreenSpaceRendererFeature.Pass.BloomUpsampling;
                                cmd.SetComputeVectorParam(cs, "_BloomParams", new Vector4(BloomW, BloomH, 1.0f / BloomW, 1.0f / BloomH));
                                cmd.SetComputeIntParam(cs, "_MipLevel", i);
                                cmd.SetComputeIntParam(cs, "_SwapBuffer", swapBuffer ? 1 : 0);
                                cmd.SetComputeTextureParam(cs, pass, "_SourceTexture", swapBuffer ? m_BackID : m_FrontID);
                                cmd.SetComputeTextureParam(cs, pass, "_OutputBuffer", swapBuffer ? m_FrontID : m_BackID, i - 1);
                                cmd.DispatchCompute(cs, pass, BloomDispatchW, BloomDispatchH, 1);
                            }
                            else
                            {
                                cmd.SetGlobalVector("_BloomParams", new Vector4(BloomW, BloomH, 1.0f / BloomW, 1.0f / BloomH));
                                cmd.SetGlobalInt("_MipLevel", i);
                                cmd.SetGlobalInt("_SwapBuffer", swapBuffer ? 1 : 0);
                                cmd.SetGlobalTexture("_SourceTexture", swapBuffer ? m_BackID : m_FrontID);
                                cmd.SetRenderTarget(swapBuffer ? m_FrontID : m_BackID, i - 1);
                                cmd.SetKeyword(prefilter, false);
                                cmd.SetKeyword(blurH, false);
                                cmd.SetKeyword(blurV, false);
                                cmd.SetKeyword(upsampling, true);
                                cmd.DrawProcedural(Matrix4x4.identity, m_BloomMat, 0, MeshTopology.Triangles, 4);
                            }
                            
                            swapBuffer = !swapBuffer;
                        }

                        activeBloom = true;
                    }
                }


                if (!settings.UseComputeShader)
                {
                    cmd.SetKeyword(taa, false);
                    cmd.SetKeyword(bloom, activeBloom);
                    cmd.SetKeyword(colorAdjustment, activeAdjustments);
                    cmd.SetKeyword(tonemapping, activeTonemapping);
                }
                if (activeBloom || activeTonemapping || activeAdjustments)
                {
                     int lutHeight = activeTonemapping ? settings.TonemappingLut.height : 32;
                    if (activeAdjustments)
                    {
                        RenderTextureDescriptor lutDesc = new RenderTextureDescriptor(lutHeight, lutHeight)
                        {
                            volumeDepth = lutHeight,
                            dimension = TextureDimension.Tex3D,
                            enableRandomWrite = settings.UseComputeShader,
                            graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm
                        };
                        if (settings.UseComputeShader)
                        {
                            RenderingUtils.ReAllocateIfNeeded(ref m_LUT, lutDesc);
                            int lutDispatch = (lutHeight + 8 - 1) / 8;
                            int lutPass = (int)ScreenSpaceRendererFeature.Pass.LutPrepare;
                            cmd.SetComputeVectorParam(cs, "_LutParams", new Vector4(lutHeight, lutHeight - 1, 1.0f / lutHeight, 0));
                            cmd.SetComputeVectorParam(cs, "_ColorFilter", settings.ColorFilter);
                            cmd.SetComputeVectorParam(cs, "_ColorBalance", ColorUtils.ColorBalanceToLMSCoeffs(settings.WhiteBalance, 1));
                            cmd.SetComputeVectorParam(cs, "_HueSatCon", new Vector4(settings.HueShift / 360f, settings.Saturation / 100f + 1, settings.Contrast / 100f + 1, 0));
                            cmd.SetComputeTextureParam(cs, lutPass, "_LutBuffer", m_LUT);
                            cmd.DispatchCompute(cs, lutPass, lutDispatch, lutDispatch, lutDispatch);
                            if (m_LUTRT != null)
                                m_LUTRT.updateMode = CustomRenderTextureUpdateMode.OnDemand;
                        }
                        else
                        {
                            m_LutPrepareMat.SetVector("_LutParams", new Vector4(lutHeight, lutHeight - 1, 1.0f / lutHeight, 0));
                            m_LutPrepareMat.SetVector("_ColorFilter", settings.ColorFilter);
                            m_LutPrepareMat.SetVector("_ColorBalance", ColorUtils.ColorBalanceToLMSCoeffs(settings.WhiteBalance, 1));
                            m_LutPrepareMat.SetVector("_HueSatCon", new Vector4(settings.HueShift / 360f, settings.Saturation / 100f + 1, settings.Contrast / 100f + 1, 0));
                            if (m_LUTRT == null)
                            {
                                m_LUTRT = new (lutHeight, lutHeight)
                                {
                                    depth = 0,
                                    format = RenderTextureFormat.ARGB32,
                                    dimension = TextureDimension.Tex3D,
                                    volumeDepth = lutHeight,
                                    graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
                                    material = m_LutPrepareMat,
                                    updateMode = CustomRenderTextureUpdateMode.Realtime
                                };
                                m_LUTRT.Create();
                            }
                            else
                                m_LUTRT.updateMode = CustomRenderTextureUpdateMode.Realtime;
                        }

                    }
                    else
                    {
                        if (settings.UseComputeShader)
                            m_LUT?.Release();
                        else
                            m_LUTRT?.Release();
                    }
                

                    if (settings.UseComputeShader)
                    {
                        int compPass = activeBloom && activeTonemapping && activeAdjustments ? (int)ScreenSpaceRendererFeature.Pass.Composite : 
                                        (activeBloom && activeTonemapping) ?  (int)ScreenSpaceRendererFeature.Pass.InactiveAdjustments : 
                                        (activeBloom && activeAdjustments) ? (int)ScreenSpaceRendererFeature.Pass.InactiveTonemapping : 
                                        (activeTonemapping && activeAdjustments) ? (int)ScreenSpaceRendererFeature.Pass.InactiveBloom : 
                                        activeBloom ? (int)ScreenSpaceRendererFeature.Pass.Bloom : 
                                        activeTonemapping ? (int)ScreenSpaceRendererFeature.Pass.ToneMapping : (int)ScreenSpaceRendererFeature.Pass.ColorAdjustment;
                        
                        cmd.SetComputeVectorParam(cs, "_PostProcessingParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
                        cmd.SetComputeTextureParam(cs, compPass, "_SourceTexture", activeTAA ? data.m_HistoryTAAResult : renderingData.cameraData.renderer.cameraColorTargetHandle);
                        cmd.SetComputeTextureParam(cs, compPass, "_OutputBuffer", m_CopiedColor);
                        if (activeTonemapping || activeAdjustments)
                        {
                            cmd.SetComputeVectorParam(cs, "_LutParams", new Vector4(settings.TonemappingLut.height, settings.TonemappingLut.height - 1, 1.0f / settings.TonemappingLut.height, 0));
                            if (activeTonemapping)
                                cmd.SetComputeTextureParam(cs, compPass, "_LutTex", settings.TonemappingLut);
                            if (activeAdjustments)
                            {
                                cmd.SetComputeFloatParam(cs, "_PostExposure", Mathf.Pow(2, settings.PostExposure));
                                cmd.SetComputeTextureParam(cs, compPass, "_InternalLutTex", m_LUT);
                            }
                        }
                        if (activeBloom)
                            cmd.SetComputeTextureParam(cs, compPass, "_InputTexture", swapBuffer ? m_BackID : m_FrontID);
                        cmd.DispatchCompute(cs, compPass, dispatchW, dispatchH, 1);
                    }
                    else
                    {
                        cmd.SetGlobalVector("_PostProcessingParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
                        cmd.SetGlobalTexture("_SourceTexture", activeTAA ? data.m_HistoryTAAResult : renderingData.cameraData.renderer.cameraColorTargetHandle);
                        cmd.SetRenderTarget(m_CopiedColor);
                        if (activeTonemapping || activeAdjustments)
                        {
                            cmd.SetGlobalVector("_LutParams", new Vector4(settings.TonemappingLut.height, settings.TonemappingLut.height - 1, 1.0f / settings.TonemappingLut.height, 0));
                            if (activeTonemapping)
                                cmd.SetGlobalTexture("_LutTex", settings.TonemappingLut);
                            if (activeAdjustments)
                            {
                                cmd.SetGlobalFloat("_PostExposure", Mathf.Pow(2, settings.PostExposure));
                                cmd.SetGlobalTexture("_InternalLutTex", settings.UseComputeShader ? m_LUT : m_LUTRT);
                            }
                        }
                        if (activeBloom)
                            cmd.SetGlobalTexture("_InputTexture", swapBuffer ? m_BackID : m_FrontID);
                        cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 4);
                    }
                }

                cmd.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
                Blitter.BlitTexture(cmd, (!activeBloom && !activeTonemapping) ? data.m_HistoryTAAResult : m_CopiedColor, new Vector4(1, 1, 0, 0), 0, false);
 
                if (activeBloom)
                {
                    cmd.ReleaseTemporaryRT(m_FrontID);
                    cmd.ReleaseTemporaryRT(m_BackID);
                }
            }

            if (SystemInfo.supportsAsyncCompute && settings.UseComputeShader)
                context.ExecuteCommandBufferAsync(cmd, ComputeQueueType.Default);
            else
                context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        
        public void Dispose()
        {
            m_CopiedColor?.Release();
        }
    }
}
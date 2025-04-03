using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    internal class HiZDepthPass : ScriptableRenderPass
    {
        ComputeShader cs;
        Material m_HiZDepthMaterial;
        GlobalKeyword initializeDepth = GlobalKeyword.Create("_INITDEPTH");
        static int m_HiZDepthID = Shader.PropertyToID("_HiZDepthBuffer");
        static int m_TempDepth = Shader.PropertyToID("_TempDepth");
        static string m_SamplerName = "HiZ Depth";
        private ProfilingSampler m_ProfilingSampler = new ProfilingSampler(m_SamplerName);
        FeatureData data;
        ScreenSpaceRendererFeature.FeatureSettings settings;

        public HiZDepthPass(RenderPassEvent evn, ComputeShader cs)
        {
            renderPassEvent = evn;
            this.cs = cs;
        }

        public void SetMaterials(ref Material material)
        {
            m_HiZDepthMaterial = material;
        }
        public void SetResources(ref FeatureData featureData, ref ScreenSpaceRendererFeature.FeatureSettings featureSettings)
        {
            data = featureData;
            settings = featureSettings;
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {

            if (data == null || (!settings.EnableSSAO && !settings.EnableSSR) || renderingData.cameraData.cameraType > CameraType.SceneView)
                return;

            if (!settings.UseComputeShader && m_HiZDepthMaterial == null)
                return;
            else if(cs == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();

            int width = renderingData.cameraData.scaledWidth;
            int height = renderingData.cameraData.scaledHeight;

            RenderTextureDescriptor desc = new RenderTextureDescriptor(width, height)
            {
                graphicsFormat = GraphicsFormatUtility.GetLinearFormat(GraphicsFormat.R32_SFloat),
                enableRandomWrite = settings.UseComputeShader,
                useMipMap = true,
                autoGenerateMips = false,
                sRGB = false
            };
            
            if (SystemInfo.supportsAsyncCompute && settings.UseComputeShader)
                cmd.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                cmd.GetTemporaryRT(m_HiZDepthID, desc);

                // initial
                if (settings.UseComputeShader)
                {                
                    int initDepthW = (width + 8 - 1) / 8;
                    int initDepthH = (height + 8 - 1) / 8;
                    cmd.SetComputeTextureParam(cs, 0, "_HiZDepthBuffer", m_HiZDepthID);
                    cmd.SetComputeVectorParam(cs, "_HiZDepthParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
                    cmd.DispatchCompute(cs, 0, initDepthW, initDepthH, 1);
                }
                else
                {
                    cmd.SetRenderTarget(m_HiZDepthID, 0);
                    cmd.SetKeyword(initializeDepth, true);
                    cmd.SetGlobalVector("_HiZDepthParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
                    cmd.DrawProcedural(Matrix4x4.identity, m_HiZDepthMaterial, 0, MeshTopology.Triangles, 4);
                }

                for (int i = 1; i < 10; i++)
                {
                    width = width / 2; height = height / 2;
                    if (width < 2 || height < 2)
                        break;
                    if (settings.UseComputeShader)
                    {                    
                        int dispatchW = (width + 8 - 1) / 8; int dispatchH = (height + 8 - 1) / 8;
                        cmd.SetComputeTextureParam(cs, 1, "_InputDepthTexture", m_HiZDepthID, i - 1);
                        cmd.SetComputeTextureParam(cs, 1, "_HiZDepthBuffer", m_HiZDepthID, i);
                        cmd.SetComputeVectorParam(cs, "_HiZDepthParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
                        cmd.DispatchCompute(cs, 1, dispatchW, dispatchH, 1);
                    }   
                    else
                    {
                        desc.width = width; desc.height = height; desc.useMipMap = false;
                        cmd.GetTemporaryRT(m_TempDepth, desc);
                        cmd.SetRenderTarget(m_TempDepth);
                        cmd.SetGlobalVector("_HiZDepthParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
                        cmd.SetGlobalInt("_MipLevel", i - 1);
                        cmd.SetGlobalTexture("_InputDepthTexture", m_HiZDepthID);
                        cmd.SetKeyword(initializeDepth, false);
                        cmd.DrawProcedural(Matrix4x4.identity, m_HiZDepthMaterial, 0, MeshTopology.Triangles, 4);
                        cmd.CopyTexture(m_TempDepth, 0, 0, m_HiZDepthID, 0, i);
                        cmd.ReleaseTemporaryRT(m_TempDepth);
                    }
                }
                cmd.SetGlobalTexture("_HiZDepthTexture", m_HiZDepthID);
            }
            if (SystemInfo.supportsAsyncCompute && settings.UseComputeShader)
                context.ExecuteCommandBufferAsync(cmd, ComputeQueueType.Default);
            else
                context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            base.OnCameraCleanup(cmd);
            cmd.ReleaseTemporaryRT(m_HiZDepthID);
        }
    }
}
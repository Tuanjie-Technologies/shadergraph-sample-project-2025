using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace UnityEngine.Rendering.Universal
{
    
    internal class CopyHistoryPass : ScriptableRenderPass
    {
        ComputeShader cs;
        bool useComputeShader = true;
        RTHandle cameraColor;
        RTHandle cameraDepth;
        RTHandle m_HistoryColor;
        RTHandle m_HistoryDepth;
        static string m_SamplerName = "Copy History";

        public CopyHistoryPass(RenderPassEvent evn, ComputeShader cs)
        {
            renderPassEvent = evn;
            this.cs = cs;
        }

        public void SetResources(RTHandle source, RTHandle depthSource, ref RTHandle destination, ref RTHandle depthDest, bool useComputeShader)
        {
            cameraColor = source; 
            cameraDepth = depthSource;

            m_HistoryColor = destination;
            m_HistoryDepth = depthDest;
            this.useComputeShader = useComputeShader;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType > CameraType.SceneView || ((m_HistoryColor == null || cameraColor == null) && (m_HistoryDepth == null || cameraDepth == null)))
                return;

            if (useComputeShader && cs == null)
                return;
            CommandBuffer cmd = CommandBufferPool.Get();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            if (SystemInfo.supportsAsyncCompute && useComputeShader)
                cmd.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

            int width = renderingData.cameraData.scaledWidth;
            int height = renderingData.cameraData.scaledHeight;
            int dispatchW = (width + 8 - 1) / 8;
            int dispatchH = (height + 8 - 1) / 8;
            int pass = (int)ScreenSpaceRendererFeature.Pass.CopyHistory;

            cmd.BeginSample(m_SamplerName);        
            if ((m_HistoryColor != null) && (cameraColor != null))
            {          
                if (useComputeShader)
                {
                    cmd.SetComputeVectorParam(cs, "_CopyParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
                    cmd.SetComputeTextureParam(cs, pass, "_SourceTexture", cameraColor);
                    cmd.SetComputeTextureParam(cs, pass, "_TargetBuffer", m_HistoryColor);
                    cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
                }
                else
                    cmd.Blit(cameraColor, m_HistoryColor);
                cmd.SetGlobalTexture("_CameraColorTex", m_HistoryColor);
            }    

            if ((m_HistoryDepth != null) && (cameraDepth != null))
            {
                if (useComputeShader)
                {
                    pass = (int)ScreenSpaceRendererFeature.Pass.CopyHistorySingleChannel;
                    cmd.SetComputeVectorParam(cs, "_CopyParams", new Vector4(width, height, 1.0f / width, 1.0f / height));
                    cmd.SetComputeTextureParam(cs, pass, "_SourceTextureSC", cameraDepth);
                    cmd.SetComputeTextureParam(cs, pass, "_TargetBufferSC", m_HistoryDepth);
                    cmd.DispatchCompute(cs, pass, dispatchW, dispatchH, 1);
                }
                else
                    cmd.Blit(cameraDepth, m_HistoryDepth);
            }
            cmd.EndSample(m_SamplerName);
            
            if (SystemInfo.supportsAsyncCompute && useComputeShader)
                context.ExecuteCommandBufferAsync(cmd, ComputeQueueType.Default);
            else
                context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

    }

}
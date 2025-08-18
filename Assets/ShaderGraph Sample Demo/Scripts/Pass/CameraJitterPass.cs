using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace UnityEngine.Rendering.Universal
{
    
    internal class CameraJitterPass : ScriptableRenderPass
    {
        ScreenSpaceRendererFeature.FeatureSettings settings;
        FeatureData data;
        static string samplerName = "Jitter Camera";
        public CameraJitterPass(RenderPassEvent evn)
        {
            renderPassEvent = evn;
        }

        public void SetResources(ref FeatureData featureData, ref ScreenSpaceRendererFeature.FeatureSettings featureSettings)
        {
            data = featureData;
            settings = featureSettings;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (data == null || !settings.EnableTAA || renderingData.cameraData.cameraType > CameraType.SceneView)
                return;


            CommandBuffer cmd = CommandBufferPool.Get();

            cmd.BeginSample(samplerName);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CameraData cameraData = renderingData.cameraData;
            cmd.SetViewProjectionMatrices(cameraData.camera.worldToCameraMatrix, data.projOverride);
            cmd.EndSample(samplerName);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

    }

}
using System;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
// using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal class MotionVectorPass : ScriptableRenderPass
    {
        #region Fields
        const string kPreviousViewProjectionNoJitter = "_PrevViewProjMatrix";
        const string kViewProjectionNoJitter = "_NonJitteredViewProjMatrix";

        static int m_MotionVectorID = Shader.PropertyToID("_MotionVectorTexture");
        static int m_MotionVectorDepthID = Shader.PropertyToID("_MotionVectorDepthTexture");
        static readonly string[] s_ShaderTags = new string[] { "MotionVectors" };

        FeatureData data;
        Material m_CameraMaterial;
        Material m_ObjectMaterial;
        readonly FilteringSettings m_FilteringSettings;
        ScreenSpaceRendererFeature.FeatureSettings settings;
        #endregion

        #region Constructors
        internal MotionVectorPass(RenderPassEvent evt,  LayerMask opaqueLayerMask)
        {
            renderPassEvent = evt;
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, opaqueLayerMask);
        }
        #endregion

        #region State
        public void SetResources(ref FeatureData featureData, ref ScreenSpaceRendererFeature.FeatureSettings featureSettings)
        {
            data = featureData;
            settings = featureSettings;
        }
        public void SetMaterials(ref Material cameraMaterial, ref Material objectMaterial)
        {
            m_CameraMaterial = cameraMaterial;
            m_ObjectMaterial = objectMaterial;
        }
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var colorDesc = cameraTextureDescriptor;
            colorDesc.graphicsFormat =GraphicsFormat.R16G16_SFloat;
            colorDesc.depthBufferBits = (int)DepthBits.None;
            colorDesc.msaaSamples = 1;  // Disable MSAA, consider a pixel resolve for half left velocity and half right velocity --> no velocity, which is untrue.
            cmd.GetTemporaryRT(m_MotionVectorID, colorDesc);

            var depthDescriptor = cameraTextureDescriptor;
            depthDescriptor.graphicsFormat = GraphicsFormat.None;
            depthDescriptor.msaaSamples = 1;
            cmd.GetTemporaryRT(m_MotionVectorDepthID, depthDescriptor);

            ConfigureTarget(m_MotionVectorID, m_MotionVectorDepthID);
            ConfigureClear(ClearFlag.Color | ClearFlag.Depth, Color.black);
            ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
        }

        #endregion

        #region Execution
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {

            if (m_CameraMaterial == null || m_ObjectMaterial == null || data == null || (!settings.EnableSSR && !settings.EnableSSAO && !settings.EnableTAA) || renderingData.cameraData.cameraType > CameraType.SceneView)
                return;

            // Get data
            ref var cameraData = ref renderingData.cameraData;
            ref var camera = ref cameraData.camera;
                
            ConfigureInput(ScriptableRenderPassInput.Depth);
            // Profiling command
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd,  new ProfilingSampler("Motion Vector")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                Matrix4x4 VP =  cameraData.GetGPUProjectionMatrixNoJitter() * cameraData.GetViewMatrix();

                cmd.SetGlobalMatrix(kPreviousViewProjectionNoJitter, data.prevUnjitteredVP);
                cmd.SetGlobalMatrix(kViewProjectionNoJitter, VP);

                camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

                DrawCameraMotionVectors(context, cmd, ref renderingData, camera, m_CameraMaterial);
                DrawObjectMotionVectors(context, ref renderingData, camera, m_ObjectMaterial, cmd, m_FilteringSettings);
                data.UpdatePreviousVP(VP);
                //cmd.SetGlobalTexture("_TaaMotionVectorTex", m_MotionVectorID);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(m_MotionVectorID);
            cmd.ReleaseTemporaryRT(m_MotionVectorDepthID);
        }

        private static DrawingSettings GetDrawingSettings(ref RenderingData renderingData, Material objectMaterial)
        {
            var camera = renderingData.cameraData.camera;
            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var drawingSettings = new DrawingSettings(ShaderTagId.none, sortingSettings)
            {
                perObjectData = PerObjectData.MotionVectors,
                enableDynamicBatching = renderingData.supportsDynamicBatching,
                enableInstancing = true,
            };

            for (int i = 0; i < s_ShaderTags.Length; ++i)
            {
                drawingSettings.SetShaderPassName(i, new ShaderTagId(s_ShaderTags[i]));
            }

            // Material that will be used if shader tags cannot be found
            drawingSettings.fallbackMaterial = objectMaterial;

            return drawingSettings;
        }

        private static void DrawCameraMotionVectors(ScriptableRenderContext context, CommandBuffer cmd, ref RenderingData renderingData, Camera camera, Material cameraMaterial)
        {
            // Draw fullscreen quad
            cmd.DrawProcedural(Matrix4x4.identity, cameraMaterial, 0, MeshTopology.Triangles, 3, 1);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private static void DrawObjectMotionVectors(ScriptableRenderContext context, ref RenderingData renderingData, Camera camera, Material objectMaterial, CommandBuffer cmd, FilteringSettings filteringSettings)
        {

            var drawingSettings = GetDrawingSettings(ref renderingData, objectMaterial);
            var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
        }
        #endregion
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [DisallowMultipleRendererFeature]
    internal class ScreenSpaceRendererFeature : ScriptableRendererFeature
    {    
        
        public LayerMask opaqueLayer = -1;
        public ComputeShader screenSpaceCS;

        public Shader HiZDepthShader;
        public Shader ResolveLastFrameShader;
        public Shader TraceSSRShader;
        public Shader SSR_ResolveShader;
        public Shader SSR_TemporalBlendingShader;
        public Shader SSR_UnpackIntoAccumShader;
        public Shader SSR_UnpackIntoBufferShader;
        public Shader SSR_BilateralBlurShader;
        public Shader TemporalClippingShader;
        public Shader postProcessingShader;
        public Shader BilateralUpsamplingShader;
        public Shader SSAOShader;
        public Shader BilateralBlurShader;
        public Shader BloomShader;
        public Shader LutPrepareShader;

    #region Pass
        CameraJitterPass jitterBeforeDepthNormal;
        MotionVectorPass motionVectorPass;
        HiZDepthPass hiZDepthPass;
        ScreenSpaceReflectionPass ssrPass;
        ScreenSpaceAmbientOcclusionPass ssaoPass;
        CameraJitterPass jitterBeforeOpaque;
        CopyHistoryPass copyHistoryPass;
        PostProcessingPass postProcessingPass;
    #endregion

    #region Data
        Dictionary<Camera, FeatureData> m_FeatureDatas;
    #endregion

    #region Resources
        // Motion Vector Related
        [SerializeField]
        [HideInInspector]
        Shader cameraMotionVectorShader = null;
        [SerializeField]
        [HideInInspector]
        Shader objectMotionVectorShader = null;
        Material m_CameraMotionVecMaterial = null;
        Material m_ObjectMotionVecMaterial = null;

        Material m_HiZDepthMateiral = null;
        Material m_ResolveLastFrameMaterial = null;
        Material m_TraceSSRMaterial = null;
        Material m_SSR_ResolveMaterial = null;
        Material m_SSR_TemporalBlendingMaterial = null;
        Material m_SSR_UnpackIntoAccumMaterial = null;
        Material m_SSR_UnpackIntoBufferMaterial = null;
        Material m_SSR_BilateralBlurMaterial = null;
        Material m_TemporalClippingMaterial = null;
        Material m_BilateralUpsamplingMaterial = null;
        Material m_PostProcessingMaterial = null;
        Material m_SSAOMaterial = null;
        Material m_BilateralBlurMaterial = null;
        Material m_BloomMaterial = null;
        Material m_LutPrepareMaterial = null;

        // Featre Settings
        FeatureSettings featureSettings;

        // Blut Noise
        [SerializeField]
        [HideInInspector]
        internal Texture2D[] m_BlueNoiseTextures;
    #endregion

        public override void Create()
        {
            #if UNITY_EDITOR
                bool needsToUpdate = false;
                if (m_BlueNoiseTextures == null)
                    needsToUpdate = true;
                else if (m_BlueNoiseTextures.Length != 64)
                    needsToUpdate = true;
                if (needsToUpdate)
                {
                    m_BlueNoiseTextures = new Texture2D[64];
                    for (int i = 0; i < 64; i++)
                    {
                        string fileName = "LDR_RGBA_" + i.ToString() + ".png";
                        m_BlueNoiseTextures[i] = (Texture2D)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Textures/BlueNoise/" + fileName, typeof(Texture2D));
                    }
                }
                if (cameraMotionVectorShader == null)
                    cameraMotionVectorShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/CameraMotionVectors.shader", typeof(Shader));
                if (objectMotionVectorShader == null)
                    objectMotionVectorShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/ObjectMotionVectors.shader", typeof(Shader));
                if (screenSpaceCS == null)
                    screenSpaceCS = (ComputeShader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/ScreenSpaceFeatures.compute", typeof(ComputeShader));

                if (HiZDepthShader == null)
                    HiZDepthShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/HiZDepth.shadergraph", typeof(Shader));
                if (ResolveLastFrameShader == null)
                    ResolveLastFrameShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/ResolveLastFrame.shadergraph", typeof(Shader));
                if (TraceSSRShader == null)
                    TraceSSRShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/TraceSSR.shadergraph", typeof(Shader));
                if (SSR_ResolveShader == null)
                    SSR_ResolveShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/SSR_Resolve.shadergraph", typeof(Shader));
                if (SSR_TemporalBlendingShader == null)
                    SSR_TemporalBlendingShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/SSR_TemporalBlending.shadergraph", typeof(Shader));
                if (SSR_UnpackIntoAccumShader == null)
                    SSR_UnpackIntoAccumShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/UnpackIntoAccum.shadergraph", typeof(Shader));
                if (SSR_UnpackIntoBufferShader == null)
                    SSR_UnpackIntoBufferShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/UnpackIntoBuffer.shadergraph", typeof(Shader));
                if (SSR_BilateralBlurShader == null)
                    SSR_BilateralBlurShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/SSR_BilateralBlur.shadergraph", typeof(Shader));
                if (TemporalClippingShader == null)
                    TemporalClippingShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/TemporalClipping.shadergraph", typeof(Shader));
                if (BilateralUpsamplingShader == null)
                    BilateralUpsamplingShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/BilateralUpsampling.shadergraph", typeof(Shader));
                if (postProcessingShader == null)
                    postProcessingShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/PostProcessing.shadergraph", typeof(Shader));
                if (SSAOShader == null)
                    SSAOShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/SSAO.shadergraph", typeof(Shader));
                if (BilateralBlurShader == null)
                    BilateralBlurShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/BilateralBlur.shadergraph", typeof(Shader)); 
                if (BloomShader == null)
                    BloomShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/Bloom.shadergraph", typeof(Shader)); 
                if (LutPrepareShader == null)
                    LutPrepareShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/LutPrepare.shadergraph", typeof(Shader)); 
            #endif

            jitterBeforeDepthNormal = new CameraJitterPass(RenderPassEvent.BeforeRenderingPrePasses);
            motionVectorPass = new MotionVectorPass(RenderPassEvent.BeforeRenderingPrePasses + 1, opaqueLayer);
            hiZDepthPass = new HiZDepthPass(RenderPassEvent.BeforeRenderingPrePasses + 1, screenSpaceCS);
            ssrPass = new ScreenSpaceReflectionPass(RenderPassEvent.BeforeRenderingOpaques, screenSpaceCS);
            ssaoPass = new ScreenSpaceAmbientOcclusionPass(RenderPassEvent.AfterRenderingPrePasses + 1, screenSpaceCS);
            jitterBeforeOpaque = new CameraJitterPass(RenderPassEvent.BeforeRenderingOpaques);
            copyHistoryPass = new CopyHistoryPass(RenderPassEvent.AfterRenderingSkybox, screenSpaceCS);
            postProcessingPass = new PostProcessingPass(RenderPassEvent.BeforeRenderingPostProcessing, screenSpaceCS);

            // Datas
            m_FeatureDatas = new Dictionary<Camera, FeatureData>();
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer,
                                in RenderingData renderingData)
        {
        #if UNITY_EDITOR
            bool needsToUpdate = false;
            if (m_BlueNoiseTextures == null)
                needsToUpdate = true;
            else if (m_BlueNoiseTextures.Length != 64)
                needsToUpdate = true;
            if (needsToUpdate)
            {
                m_BlueNoiseTextures = new Texture2D[64];
                for (int i = 0; i < 64; i++)
                {
                    string fileName = "LDR_RGBA_" + i.ToString() + ".png";
                    m_BlueNoiseTextures[i] = (Texture2D)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Textures/BlueNoise/" + fileName, typeof(Texture2D));
                }
            }
            if (cameraMotionVectorShader == null)
                cameraMotionVectorShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/CameraMotionVectors.shader", typeof(Shader));
            if (objectMotionVectorShader == null)
                objectMotionVectorShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/ObjectMotionVectors.shader", typeof(Shader));
            if (screenSpaceCS == null)
                screenSpaceCS = (ComputeShader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/Shaders/ScreenSpaceFeatures.compute", typeof(ComputeShader));
            if (postProcessingShader == null)
                postProcessingShader = (Shader)UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Scripts/ShaderGraphs/PostProcessing.shadergraph", typeof(Shader));
        #endif

            // setup settings
            var stack = VolumeManager.instance.stack;
            var ssComponent = stack.GetComponent<ScreenSpaceComponent>();
            if (ssComponent != null)
            {
                featureSettings.UseComputeShader = ssComponent.UseComputeShader.value;
                featureSettings.EnableSSR = ssComponent.SSR_ON.value;;
                featureSettings.DownSample = ssComponent.DownSample.value;
                featureSettings.MaxSteps = ssComponent.maxSteps.value;
                featureSettings.Thickness = ssComponent.thickness.value;
                featureSettings.EdgeFade = ssComponent.edgeFade.value;
                featureSettings.BRDFBias = ssComponent.brdfBias.value;
                featureSettings.EnableSSAO = ssComponent.SSAO_ON.value;
                featureSettings.SSAO_DownSample = ssComponent.SSAO_DownSample.value;
                featureSettings.Slices = ssComponent.Slices.value;
                featureSettings.Steps = ssComponent.Steps.value;
                featureSettings.TracingRadiusScalar = ssComponent.TracingRadiusScalar.value;
                featureSettings.Radius = ssComponent.Radius.value;
                featureSettings.DirectionalStrength = ssComponent.DirectionalStrength.value;
                featureSettings.Intensity = ssComponent.Intensity.value;
                featureSettings.EnableTAA = ssComponent.TAA_ON.value;
                featureSettings.JitterSize = ssComponent.JitterSize.value;
                featureSettings.FrameInfluencer = ssComponent.FrameInfluencer.value;
                featureSettings.EnableBloom = ssComponent.BLOOM_ON.value;
                featureSettings.Threshold = ssComponent.Threshold.value;
                featureSettings.BloomIntensity = ssComponent.BloomIntensity.value;
                featureSettings.Scatter = ssComponent.Scatter.value;
                featureSettings.MaxIterations = ssComponent.MaxIterations.value;
                featureSettings.EnableTonemapping = ssComponent.Tonemapping_ON.value && ssComponent.LUT != null;
                featureSettings.TonemappingLut = ssComponent.LUT != null ? (Texture3D)ssComponent.LUT.value : null;
                featureSettings.PostExposure = ssComponent.PostExposure.value;
                featureSettings.WhiteBalance = ssComponent.WhiteBalance.value;
                featureSettings.Contrast = ssComponent.Contrast.value;
                featureSettings.ColorFilter = ssComponent.ColorFilter.value;
                featureSettings.HueShift = ssComponent.HueShift.value;
                featureSettings.Saturation = ssComponent.Saturation.value;
            }
            else
            {
                featureSettings.EnableSSR = false;
                featureSettings.EnableSSAO = false;
                featureSettings.EnableTAA = false;
                featureSettings.EnableBloom = false;
                featureSettings.EnableTonemapping = false;
            }

            var camera = renderingData.cameraData.camera;
            FeatureData featureData;
            if (renderingData.cameraData.cameraType <= CameraType.SceneView)
            {
                if (!m_FeatureDatas.TryGetValue(camera, out featureData))
                {
                    featureData = new FeatureData();
                    m_FeatureDatas.Add(camera, featureData);
                }

                featureData.UpdateRTHandles(renderer.cameraColorTargetHandle.rt.descriptor, ref featureSettings);

                if (featureSettings.EnableTAA)
                    featureData.UpdateTAAData(renderingData.cameraData, ref featureSettings);
                
                jitterBeforeDepthNormal.SetResources(ref featureData, ref featureSettings);
                jitterBeforeOpaque.SetResources(ref featureData, ref featureSettings);

                if (featureSettings.EnableSSAO || featureSettings.EnableSSR || featureSettings.EnableTAA)
                {
                    if (m_CameraMotionVecMaterial == null)
                        m_CameraMotionVecMaterial = CoreUtils.CreateEngineMaterial(cameraMotionVectorShader);
                    if (m_ObjectMotionVecMaterial == null)
                        m_ObjectMotionVecMaterial = CoreUtils.CreateEngineMaterial(objectMotionVectorShader);
                }
                motionVectorPass.SetMaterials(ref m_CameraMotionVecMaterial, ref m_ObjectMotionVecMaterial);
                motionVectorPass.SetResources(ref featureData, ref featureSettings);

                if ((featureSettings.EnableSSAO || featureSettings.EnableSSR) && !featureSettings.UseComputeShader)
                {
                    if (m_HiZDepthMateiral == null)
                        m_HiZDepthMateiral = CoreUtils.CreateEngineMaterial(HiZDepthShader);
                    hiZDepthPass.SetMaterials(ref m_HiZDepthMateiral);
                }
                hiZDepthPass.SetResources(ref featureData, ref featureSettings);

                if (featureSettings.EnableSSR && !featureSettings.UseComputeShader)
                {
                    if (m_ResolveLastFrameMaterial == null)
                        m_ResolveLastFrameMaterial = CoreUtils.CreateEngineMaterial(ResolveLastFrameShader);
                    if (m_TraceSSRMaterial == null)
                        m_TraceSSRMaterial = CoreUtils.CreateEngineMaterial(TraceSSRShader);
                    if (m_SSR_ResolveMaterial == null)
                        m_SSR_ResolveMaterial = CoreUtils.CreateEngineMaterial(SSR_ResolveShader);
                    if (m_SSR_TemporalBlendingMaterial == null)
                        m_SSR_TemporalBlendingMaterial = CoreUtils.CreateEngineMaterial(SSR_TemporalBlendingShader);
                    if (m_SSR_UnpackIntoAccumMaterial == null)
                        m_SSR_UnpackIntoAccumMaterial = CoreUtils.CreateEngineMaterial(SSR_UnpackIntoAccumShader);
                    if (m_SSR_UnpackIntoBufferMaterial == null)
                        m_SSR_UnpackIntoBufferMaterial = CoreUtils.CreateEngineMaterial(SSR_UnpackIntoBufferShader);
                    if (m_SSR_BilateralBlurMaterial == null)
                        m_SSR_BilateralBlurMaterial = CoreUtils.CreateEngineMaterial(SSR_BilateralBlurShader);
                    if (m_BilateralUpsamplingMaterial == null)
                        m_BilateralUpsamplingMaterial = CoreUtils.CreateEngineMaterial(BilateralUpsamplingShader);
                    if (m_TemporalClippingMaterial == null)
                        m_TemporalClippingMaterial = CoreUtils.CreateEngineMaterial(TemporalClippingShader);
                    ssrPass.SetMaterials(ref m_ResolveLastFrameMaterial, ref m_TraceSSRMaterial, ref m_SSR_ResolveMaterial, ref m_SSR_TemporalBlendingMaterial, ref m_SSR_UnpackIntoAccumMaterial, ref m_SSR_UnpackIntoBufferMaterial
                    , ref m_SSR_BilateralBlurMaterial, ref m_TemporalClippingMaterial, ref m_BilateralUpsamplingMaterial);
                }
                ssrPass.SetResources(ref featureData, ref m_BlueNoiseTextures[featureData.frameCount % m_BlueNoiseTextures.Length], ref featureSettings);

                if (featureSettings.EnableSSAO && !featureSettings.UseComputeShader)
                {
                    if (m_SSAOMaterial == null)
                        m_SSAOMaterial = CoreUtils.CreateEngineMaterial(SSAOShader);
                    if (m_BilateralBlurMaterial == null)
                        m_BilateralBlurMaterial = CoreUtils.CreateEngineMaterial(BilateralBlurShader);
                    if (m_BilateralUpsamplingMaterial == null)
                        m_BilateralUpsamplingMaterial = CoreUtils.CreateEngineMaterial(BilateralUpsamplingShader);
                    if (m_TemporalClippingMaterial == null)
                        m_TemporalClippingMaterial = CoreUtils.CreateEngineMaterial(TemporalClippingShader);
                    ssaoPass.SetMaterials(ref m_SSAOMaterial, ref m_BilateralBlurMaterial, ref m_TemporalClippingMaterial, ref m_BilateralBlurMaterial);
                }
                ssaoPass.SetResources(ref featureData, ref m_BlueNoiseTextures[featureData.frameCount % m_BlueNoiseTextures.Length], ref featureSettings);
                    
                copyHistoryPass.SetResources(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle, ref featureData.m_HistoryOpaque, ref featureData.m_HistoryDepth, featureSettings.UseComputeShader);

                if (featureSettings.EnableTonemapping || featureSettings.EnableBloom || featureSettings.EnableTAA)
                {
                    if (!featureSettings.UseComputeShader)
                    {
                        if (m_PostProcessingMaterial == null)
                            m_PostProcessingMaterial = CoreUtils.CreateEngineMaterial(postProcessingShader);
                        if (m_BloomMaterial == null)
                            m_BloomMaterial = CoreUtils.CreateEngineMaterial(BloomShader);
                        if (m_LutPrepareMaterial == null)
                            m_LutPrepareMaterial = CoreUtils.CreateEngineMaterial(LutPrepareShader);
                        postProcessingPass.SetMaterials(ref m_PostProcessingMaterial, ref m_BloomMaterial, ref m_LutPrepareMaterial);
                    }
                    postProcessingPass.SetResources(ref featureSettings, ref featureData);
                }
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {   
            var camera = renderingData.cameraData.camera;

            renderer.EnqueuePass(jitterBeforeDepthNormal);
            renderer.EnqueuePass(motionVectorPass);
            renderer.EnqueuePass(hiZDepthPass);

            ssrPass.ShouldEscape(ref renderingData);
            renderer.EnqueuePass(ssrPass);

            ssaoPass.ShouldEscape(ref renderingData);
            renderer.EnqueuePass(ssaoPass);

            renderer.EnqueuePass(copyHistoryPass);
            
            renderer.EnqueuePass(jitterBeforeOpaque);

            if (featureSettings.EnableTonemapping || featureSettings.EnableBloom || featureSettings.EnableTAA)
                renderer.EnqueuePass(postProcessingPass);

            if (camera.cameraType <= CameraType.SceneView && (featureSettings.EnableSSAO || featureSettings.EnableSSR || featureSettings.EnableTAA))
            {
                if (!m_FeatureDatas.TryGetValue(camera, out FeatureData featureData))
                {
                    featureData = new FeatureData();
                    m_FeatureDatas.Add(camera, featureData);
                }
                featureData.UpdateFrame();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            postProcessingPass.Dispose();
            CoreUtils.Destroy(m_CameraMotionVecMaterial);
            CoreUtils.Destroy(m_ObjectMotionVecMaterial);
            CoreUtils.Destroy(m_HiZDepthMateiral);
            CoreUtils.Destroy(m_ResolveLastFrameMaterial);
            CoreUtils.Destroy(m_TraceSSRMaterial);
            CoreUtils.Destroy(m_SSR_ResolveMaterial);
            CoreUtils.Destroy(m_SSR_TemporalBlendingMaterial);
            CoreUtils.Destroy(m_SSR_UnpackIntoAccumMaterial);
            CoreUtils.Destroy(m_SSR_UnpackIntoBufferMaterial);
            CoreUtils.Destroy(m_SSR_BilateralBlurMaterial);
            CoreUtils.Destroy(m_TemporalClippingMaterial);
            CoreUtils.Destroy(m_BilateralUpsamplingMaterial);
            CoreUtils.Destroy(m_PostProcessingMaterial);
            CoreUtils.Destroy(m_SSAOMaterial);
            CoreUtils.Destroy(m_BilateralBlurMaterial);
            CoreUtils.Destroy(m_BloomMaterial);
            CoreUtils.Destroy(m_LutPrepareMaterial);
        }

        public enum Pass
        {
            InitialDepth = 0,
            HiZDepth = 1,
            ResolveLastFrame = 2,
            SSR = 3,
            SSR_Resolve = 4,
            SSR_TemporalBlending = 5,
            SSR_BilateralBlur = 6,
            TemporalClipping = 7,
            Upsampling = 8,
            SSAO = 9,
            BilateralBlurHorizontal = 10,
            BilateralBlurVertical = 11,
            CopyHistory = 12,
            CopyHistorySingleChannel = 13,
            BloomPrefilter = 14,
            BloomBlurH = 15,
            BloomBlurV = 16,
            BloomUpsampling = 17,
            TAA = 18,
            ToneMapping = 19,
            Bloom = 20,
            ColorAdjustment = 21,
            InactiveAdjustments = 22,
            InactiveBloom = 23,
            InactiveTonemapping = 24,
            Composite = 25,
            LutPrepare = 26
        }

        public struct FeatureSettings
        {
            public bool UseComputeShader;

            // SSR
            public bool EnableSSR;
            public bool DownSample;
            public int MaxSteps;
            public float Thickness;
            public float EdgeFade;
            public float BRDFBias;

            // SSAO
            public bool EnableSSAO;
            public bool SSAO_DownSample;
            public int Slices;
            public int Steps;
            public float TracingRadiusScalar;
            public float Radius;
            public float DirectionalStrength;
            public float Intensity;

            // TAA
            public bool EnableTAA;
            public float JitterSize;
            public float FrameInfluencer;

            // Bloom
            public bool EnableBloom;
            public float Threshold;
            public float BloomIntensity;
            public float Scatter;
            public int MaxIterations;

            // Tonemapping
            public bool EnableTonemapping;
            public Texture3D TonemappingLut;


            // Color Adjustments
            public float PostExposure;
            public float WhiteBalance;
            public float Contrast;
            public Color ColorFilter;
            public float HueShift;
            public float Saturation;
        }

    }
}
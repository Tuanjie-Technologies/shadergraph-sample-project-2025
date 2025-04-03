using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class FeatureData 
    {
        internal RTHandle m_HistoryTAAResult;
        internal RTHandle m_HistoryReflectionResult;
        internal RTHandle m_HistoryReflectionAccum;
        internal RTHandle m_HistoryOcclusionResult;
        internal RTHandle m_HistoryOpaque;
        internal RTHandle m_HistoryDepth;

        internal Vector2 jitterSize;
        internal Vector2 sampleOffset;
        internal Vector3 prevCameraPos;
        internal Matrix4x4 projOverride;
        internal Matrix4x4 projectionMatrix;
        internal Matrix4x4 viewMatrix;
        internal Matrix4x4 previousProjection;
        internal Matrix4x4 previousView;
        internal Matrix4x4 prevUnjitteredVP;

        internal int frameCount = 0;
        internal FeatureData()
        {
            projOverride = Matrix4x4.identity;
            previousProjection = Matrix4x4.identity;
            previousView = Matrix4x4.identity;
            projectionMatrix = Matrix4x4.identity;
            viewMatrix = Matrix4x4.identity;
            prevUnjitteredVP = Matrix4x4.identity;
            frameCount = 0;
        }


        public void UpdateRTHandles(RenderTextureDescriptor desc, ref ScreenSpaceRendererFeature.FeatureSettings settings)
        {
            int width = desc.width; int height = desc.height;

            if (settings.EnableTAA || settings.EnableSSR)
            {
                desc = new RenderTextureDescriptor(width, height)
                {
                    graphicsFormat = desc.graphicsFormat,
                    enableRandomWrite = settings.UseComputeShader
                };
            }

            if (settings.EnableTAA)
                RenderingUtils.ReAllocateIfNeeded(ref m_HistoryTAAResult, desc);
            else
                ReleaseTAAHandle();

            // Only need history color for SSR
            if (settings.EnableSSR)
            {
                desc.enableRandomWrite = settings.UseComputeShader;
                RenderingUtils.ReAllocateIfNeeded(ref m_HistoryOpaque, desc);
                if (settings.DownSample)
                {
                    desc.width = width >> 1;
                    desc.height = height >> 1;
                }
                desc.enableRandomWrite = settings.UseComputeShader;
                desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;              
                RenderingUtils.ReAllocateIfNeeded(ref m_HistoryReflectionResult, desc);
                desc.graphicsFormat = SystemInfo.GetCompatibleFormat(GraphicsFormat.R8_UNorm, FormatUsage.LoadStore);
                RenderingUtils.ReAllocateIfNeeded(ref m_HistoryReflectionAccum, desc);
            }
            else
            {
                ReleaseOpaqueHandle();
                ReleaseSSRHandle();
            }


            if (settings.EnableSSAO)
            {
                desc = new RenderTextureDescriptor(width, height)
                {
                    enableRandomWrite = settings.UseComputeShader,
                    graphicsFormat = SystemInfo.GetCompatibleFormat(GraphicsFormat.R8_UNorm, FormatUsage.LoadStore)
                };

                if (settings.SSAO_DownSample)
                {
                    desc.width  = width >> 1;
                    desc.height = height >> 1;
                }
                RenderingUtils.ReAllocateIfNeeded(ref m_HistoryOcclusionResult, desc);
            }
            else
                ReleaseOcclusionHandle();
            

            if (settings.EnableSSR || settings.EnableSSAO)
            {
                desc.width = width; desc.height = height;
                desc.graphicsFormat = GraphicsFormat.R32_SFloat;
                desc.enableRandomWrite = settings.UseComputeShader;
                RenderingUtils.ReAllocateIfNeeded(ref m_HistoryDepth, desc);
            }
            else
                ReleaseDepthHandle();


            if (!settings.EnableSSAO && !settings.EnableTAA && !settings.EnableSSR)
            {
                ResetFrame();
                ReleaseOpaqueHandle();
                ReleaseDepthHandle();
                ReleaseSSRHandle();
                ReleaseOcclusionHandle();
                ReleaseTAAHandle();
            }
        }
        public void ReleaseTAAHandle()
        {
            m_HistoryTAAResult?.Release();
            m_HistoryTAAResult = null;
        }
        public void ReleaseOcclusionHandle()
        {
            m_HistoryOcclusionResult?.Release();
            m_HistoryOcclusionResult = null;
        }
        public void ReleaseSSRHandle()
        {
            m_HistoryReflectionAccum?.Release();
            m_HistoryReflectionAccum = null;
            m_HistoryReflectionResult?.Release();
            m_HistoryReflectionResult = null;
        }
        public void ReleaseOpaqueHandle()
        {
            m_HistoryOpaque?.Release();
            m_HistoryOpaque = null;
        }
        public void ReleaseDepthHandle()
        {
            m_HistoryDepth?.Release();
            m_HistoryDepth = null;
        }
        public void UpdateTAAData(in CameraData cameraData, ref ScreenSpaceRendererFeature.FeatureSettings settings)
        {
            jitterSize = CalculateJitter(frameCount) * settings.JitterSize; // times jitter scale
            previousProjection = projectionMatrix;
            previousView = viewMatrix;
            projOverride = cameraData.camera.orthographic ? GetJitteredOrthographicProjectionMatrix(jitterSize, cameraData) : GetJitteredPerspectiveProjectionMatrix(jitterSize, cameraData);
            sampleOffset = new Vector2(jitterSize.x / cameraData.scaledWidth, jitterSize.y / cameraData.scaledHeight);
            viewMatrix = cameraData.camera.worldToCameraMatrix;
            projectionMatrix = cameraData.camera.projectionMatrix;
        }
        public void UpdatePreviousVP(Matrix4x4 vp)
        {
            prevUnjitteredVP = vp;
        }
        public void UpdatePreviousCameraPos(Vector3 pos)
        {
            prevCameraPos = pos;
        }
        public void UpdateFrame()
        {
            frameCount++;
        }
        public void ResetFrame()
        {
            frameCount = 0;
        }
        static internal Vector2 CalculateJitter(int frameIndex)
        {
            // The variance between 0 and the actual halton sequence values reveals noticeable
            // instability in Unity's shadow maps, so we avoid index 0.
            float jitterX = HaltonSequence.Get((frameIndex & 1023) + 1, 2) - 0.5f;
            float jitterY = HaltonSequence.Get((frameIndex & 1023) + 1, 3) - 0.5f;

            return new Vector2(jitterX, jitterY);
        }
        internal static Matrix4x4 GetJitteredOrthographicProjectionMatrix(Vector2 offset, in CameraData cameraData)
        {
            float vertical = cameraData.camera.orthographicSize;
            float horizontal = vertical * cameraData.camera.aspect;

            offset.x *= horizontal / (0.5f * cameraData.scaledWidth);
            offset.y *= vertical / (0.5f * cameraData.scaledHeight);

            float left = offset.x - horizontal;
            float right = offset.x + horizontal;
            float top = offset.y + vertical;
            float bottom = offset.y - vertical;

            return Matrix4x4.Ortho(left, right, bottom, top, cameraData.camera.nearClipPlane, cameraData.camera.farClipPlane);
        }
        internal static Matrix4x4 GetJitteredPerspectiveProjectionMatrix(Vector2 offset, in CameraData cameraData)
        {
            float near = cameraData.camera.nearClipPlane;
            float far = cameraData.camera.farClipPlane;

            float vertical = Mathf.Tan(0.5f * Mathf.Deg2Rad * cameraData.camera.fieldOfView) * near;
            float horizontal = vertical * cameraData.camera.aspect;

            offset.x *= horizontal / (0.5f * cameraData.scaledWidth);
            offset.y *= vertical / (0.5f * cameraData.scaledHeight);

            var matrix = cameraData.camera.projectionMatrix;

            matrix[0, 2] += offset.x / horizontal;
            matrix[1, 2] += offset.y / vertical;

            return matrix;
        }
    }

}
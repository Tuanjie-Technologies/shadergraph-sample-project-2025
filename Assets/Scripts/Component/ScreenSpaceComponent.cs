using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenuForRenderPipeline("Custom/Screen Space Feature", typeof(UniversalRenderPipeline))]
public class ScreenSpaceComponent : VolumeComponent, IPostProcessComponent
{
    public BoolParameter UseComputeShader = new BoolParameter(true);

    [Header("Screen Space Reflection")]
    [InspectorName("Enable SSR")]
    public BoolParameter SSR_ON = new BoolParameter(false);
    public BoolParameter DownSample = new BoolParameter(false);
    public ClampedIntParameter maxSteps = new ClampedIntParameter(value: 40, min: 1, max: 100, overrideState: false);
    public ClampedFloatParameter thickness = new ClampedFloatParameter(value: 0.00015f, min: 0.00001f, max: 0.2f, overrideState: false);
    public ClampedFloatParameter brdfBias = new ClampedFloatParameter(value: 0.7f, min: 0, max: 1, overrideState: false);
    public ClampedFloatParameter edgeFade = new ClampedFloatParameter(value: 0.2f, min: 0, max: 0.5f, overrideState: false);


    [Header("Screen Space Ambient Occlusion")]
    [InspectorName("Enable SSAO")]
    public BoolParameter SSAO_ON = new BoolParameter(false);
    [InspectorName("Down Sample")]
    public BoolParameter SSAO_DownSample = new BoolParameter(false);
    public ClampedIntParameter Slices = new ClampedIntParameter(value: 2, min: 1, max: 4, overrideState: false);
    public ClampedIntParameter Steps = new ClampedIntParameter(value: 3, min: 1, max: 8, overrideState: false);
    public ClampedFloatParameter TracingRadiusScalar = new ClampedFloatParameter(value: 1.0f, min: 0, max: 100, overrideState: false);
    public ClampedFloatParameter Radius = new ClampedFloatParameter(value: 2.5f, min: 0, max: 10, overrideState: false);
    public ClampedFloatParameter DirectionalStrength = new ClampedFloatParameter(value: 0.0f, min: 0, max: 1, overrideState: false);
    public ClampedFloatParameter Intensity = new ClampedFloatParameter(value: 1, min: 0, max: 5, overrideState: false);

    [Header("Temporal Anti-Aliasing")]
    [InspectorName("Enable TAA")]
    public BoolParameter TAA_ON = new BoolParameter(false);
    public ClampedFloatParameter JitterSize = new ClampedFloatParameter(value: 1, min: 0, max: 1, overrideState: false);
    public ClampedFloatParameter FrameInfluencer = new ClampedFloatParameter(value: 0.05f, min: 0, max:1, overrideState: false);

    [Header("Bloom")]
    [InspectorName("Enable Bloom")]
    public BoolParameter BLOOM_ON = new BoolParameter(false);
    public ClampedFloatParameter Threshold = new ClampedFloatParameter(value:1, min: 0, max: 10, overrideState: false);
    [InspectorName("Intensity")]
    public ClampedFloatParameter BloomIntensity = new ClampedFloatParameter(value:0.35f, min: 0, max: 10, overrideState: false);
    public ClampedFloatParameter Scatter = new ClampedFloatParameter(value:0.7f, min: 0, max: 1, overrideState: false);
    public ClampedIntParameter MaxIterations = new ClampedIntParameter(value: 6, min: 2, max: 8);

    [Header("Tonemapping")]
    [InspectorName("Enable Tonemapping")]
    public BoolParameter Tonemapping_ON = new BoolParameter(false);
    public Texture3DParameter LUT = new Texture3DParameter(null);

    [Header("Color Adjustments")]
    public ClampedFloatParameter PostExposure = new ClampedFloatParameter(value: 0, min: -5, max: 5, overrideState: false);
    public ClampedFloatParameter WhiteBalance = new ClampedFloatParameter(value: 0, min: -100, max: 100, overrideState: false);
    public ClampedFloatParameter Contrast = new ClampedFloatParameter(value: 0, min: -100, max: 100, overrideState: false);
    public ColorParameter ColorFilter = new ColorParameter(Color.white, hdr:true, true, true, overrideState: false );
    public ClampedFloatParameter HueShift = new ClampedFloatParameter(value: 0, min: -180, max: 180, overrideState: false);
    public ClampedFloatParameter Saturation = new ClampedFloatParameter(value: 0, min: -100, max: 100, overrideState: false);


    public bool IsActive() => SSR_ON.value || SSAO_ON.value || TAA_ON.value;

    public bool IsTileCompatible() => true;
}
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class HologramComposer : MonoBehaviour
{
    [Header("Source Cameras")]
    public Camera topCamera;
    public Camera leftCamera;
    public Camera bottomCamera;
    public Camera rightCamera;

    [Header("Output Material")]
    public Material hologramMaterial;

    [Header("Render Texture Settings")]
    [Range(256, 4096)] public int textureSize = 1024;
    public int depthBuffer = 24;

    [Header("Layout")]
    [Range(0.1f, 0.4f)] public float diamondHalfWidth = 0.22f;
    [Range(0.1f, 0.4f)] public float diamondHalfHeight = 0.22f;
    [Range(0.5f, 1.5f)] public float contentScale = 0.9f;

    [Header("Optional extra flips")]
    public bool flipTopX;
    public bool flipTopY;
    public bool flipLeftX;
    public bool flipLeftY;
    public bool flipBottomX;
    public bool flipBottomY;
    public bool flipRightX;
    public bool flipRightY;

    RenderTexture topRT;
    RenderTexture leftRT;
    RenderTexture bottomRT;
    RenderTexture rightRT;

    void Start()
    {
        ConfigureAuxCamera(topCamera);
        ConfigureAuxCamera(leftCamera);
        ConfigureAuxCamera(bottomCamera);
        ConfigureAuxCamera(rightCamera);

        topRT = CreateRT("Holo_TopRT");
        leftRT = CreateRT("Holo_LeftRT");
        bottomRT = CreateRT("Holo_BottomRT");
        rightRT = CreateRT("Holo_RightRT");

        if (topCamera) topCamera.targetTexture = topRT;
        if (leftCamera) leftCamera.targetTexture = leftRT;
        if (bottomCamera) bottomCamera.targetTexture = bottomRT;
        if (rightCamera) rightCamera.targetTexture = rightRT;

        ApplyToMaterial();
    }

    void ConfigureAuxCamera(Camera camera)
    {
        if (!camera) return;

        camera.stereoTargetEye = StereoTargetEyeMask.None;
        camera.allowHDR = false;
        camera.allowMSAA = false;

        if (camera.TryGetComponent<AudioListener>(out var listener))
            listener.enabled = false;

        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.allowXRRendering = false;
        cameraData.renderShadows = false;
        cameraData.requiresColorTexture = false;
        cameraData.requiresDepthTexture = false;
        cameraData.renderPostProcessing = false;
    }

    void OnValidate()
    {
        ApplyToMaterial();
    }

    void ApplyToMaterial()
    {
        if (hologramMaterial == null) return;

        if (topRT) hologramMaterial.SetTexture("_TopTex", topRT);
        if (leftRT) hologramMaterial.SetTexture("_LeftTex", leftRT);
        if (bottomRT) hologramMaterial.SetTexture("_BottomTex", bottomRT);
        if (rightRT) hologramMaterial.SetTexture("_RightTex", rightRT);

        hologramMaterial.SetFloat("_DiamondHalfWidth", diamondHalfWidth);
        hologramMaterial.SetFloat("_DiamondHalfHeight", diamondHalfHeight);
        hologramMaterial.SetFloat("_ContentScale", contentScale);

        hologramMaterial.SetFloat("_FlipTopX", flipTopX ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipTopY", flipTopY ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipLeftX", flipLeftX ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipLeftY", flipLeftY ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipBottomX", flipBottomX ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipBottomY", flipBottomY ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipRightX", flipRightX ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipRightY", flipRightY ? 1f : 0f);
    }

    RenderTexture CreateRT(string rtName)
    {
        RenderTexture rt = new RenderTexture(textureSize, textureSize, depthBuffer, RenderTextureFormat.ARGB32);
        rt.name = rtName;
        rt.Create();
        return rt;
    }

    void OnDestroy()
    {
        ReleaseRT(topRT);
        ReleaseRT(leftRT);
        ReleaseRT(bottomRT);
        ReleaseRT(rightRT);
    }

    void ReleaseRT(RenderTexture rt)
    {
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
        }
    }
}

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

public class HologramFinalOutput : MonoBehaviour
{
    public Camera compositorCamera;
    public int width = 1024;
    public int height = 1024;
    public int depth = 24;

    public RenderTexture OutputTexture { get; private set; }
    public bool IsOutputActive => compositorCamera && compositorCamera.enabled && OutputTexture;

    private bool formatSupported;

    private void Awake()
    {
        const GraphicsFormat streamableFormat = GraphicsFormat.B8G8R8A8_SRGB;

        formatSupported = SystemInfo.IsFormatSupported(
            streamableFormat,
            GraphicsFormatUsage.Render);
        if (!formatSupported)
        {
            Debug.LogError("HologramFinalOutput requires B8G8R8A8_SRGB render support for Unity WebRTC streaming.");
            return;
        }

        if (compositorCamera != null)
        {
            ConfigureCompositorCamera(compositorCamera);
            compositorCamera.enabled = false;
            compositorCamera.targetTexture = null;
        }
    }

    public bool ActivateOutput()
    {
        if (!formatSupported || !compositorCamera)
            return false;

        const GraphicsFormat streamableFormat = GraphicsFormat.B8G8R8A8_SRGB;
        if (!OutputTexture)
        {
            var descriptor = new RenderTextureDescriptor(
                width,
                height,
                streamableFormat,
                depth);

            OutputTexture = new RenderTexture(descriptor);
            OutputTexture.name = "HologramFinalOutput";
            OutputTexture.Create();

            Debug.Log("HologramFinalOutput format: " + OutputTexture.graphicsFormat);
        }

        ConfigureCompositorCamera(compositorCamera);
        compositorCamera.targetTexture = OutputTexture;
        compositorCamera.enabled = true;
        return true;
    }

    public void DeactivateOutput()
    {
        if (compositorCamera)
        {
            compositorCamera.enabled = false;
            if (compositorCamera.targetTexture == OutputTexture)
                compositorCamera.targetTexture = null;
        }

        ReleaseOutputTexture();
    }

    private void ConfigureCompositorCamera(Camera camera)
    {
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

    private void OnDestroy()
    {
        ReleaseOutputTexture();
    }

    private void ReleaseOutputTexture()
    {
        if (OutputTexture)
        {
            OutputTexture.Release();
            Destroy(OutputTexture);
            OutputTexture = null;
        }
    }
}

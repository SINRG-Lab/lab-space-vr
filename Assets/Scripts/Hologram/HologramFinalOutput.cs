using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class HologramFinalOutput : MonoBehaviour
{
    public Camera compositorCamera;
    public int width = 1024;
    public int height = 1024;
    public int depth = 24;

    public RenderTexture OutputTexture { get; private set; }

    private void Awake()
    {
        const GraphicsFormat streamableFormat = GraphicsFormat.B8G8R8A8_SRGB;

        if (!SystemInfo.IsFormatSupported(streamableFormat, GraphicsFormatUsage.Render))
        {
            Debug.LogError("HologramFinalOutput requires B8G8R8A8_SRGB render support for Unity WebRTC streaming.");
            return;
        }

        var descriptor = new RenderTextureDescriptor(width, height, streamableFormat, depth);

        OutputTexture = new RenderTexture(descriptor);
        OutputTexture.name = "HologramFinalOutput";
        OutputTexture.Create();

        Debug.Log("HologramFinalOutput format: " + OutputTexture.graphicsFormat);

        if (compositorCamera != null)
        {
            compositorCamera.targetTexture = OutputTexture;
        }
    }

    private void OnDestroy()
    {
        if (OutputTexture != null)
        {
            OutputTexture.Release();
            Destroy(OutputTexture);
        }
    }
}

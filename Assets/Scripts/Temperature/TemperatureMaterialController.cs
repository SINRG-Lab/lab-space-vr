using UnityEngine;

public class TemperatureMaterialController : MonoBehaviour
{
    [Header("Target")]
    public Renderer targetRenderer;

    [Header("Temperature Range")]
    public float minTemperature = 0f;      // e.g. 0°C
    public float maxTemperature = 1000f;    // e.g. 100°C

    [Header("Visuals")]
    public Gradient temperatureGradient;   // assign in Inspector (blue → red)
    public bool useEmission = true;
    public float emissionStrength = 2f;

    Material _mat;
    
    void Awake()
    {
        if (!targetRenderer)
            targetRenderer = GetComponent<Renderer>();

        // Get a unique material instance
        _mat = targetRenderer.material;
    }

    public void SetTemperature(float temperature)
    {
        // Normalize 0–1 between min and max temp
        float t = Mathf.InverseLerp(minTemperature, maxTemperature, temperature);

        // Get color from gradient
        Color c = temperatureGradient.Evaluate(t);

        // Standard / URP:
        if (_mat.HasProperty("_BaseColor"))
            _mat.SetColor("_BaseColor", c);       // URP Lit
        else if (_mat.HasProperty("_Color"))
            _mat.SetColor("_Color", c);           // Standard

        if (useEmission)
        {
            if (_mat.HasProperty("_EmissionColor"))
            {
                Color emissionColor = c * emissionStrength;
                _mat.SetColor("_EmissionColor", emissionColor);
                _mat.EnableKeyword("_EMISSION");
            }
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

public class TemperatureMaterialController : MonoBehaviour
{
    [Header("Target")]
    public List<Renderer> targetRenderer = new List<Renderer>();

    [Header("Temperature Range")]
    public float minTemperature = 0f;      // e.g. 0°C
    public float maxTemperature = 1000f;    // e.g. 100°C

    [Header("Visuals")]
    public Gradient temperatureGradient;   // assign in Inspector (blue → red)
    public bool useEmission = true;
    public float emissionStrength = 2f;

    private List<Material> _mats = new List<Material>();
    
    void Awake()
    {
        if (targetRenderer == null || targetRenderer.Count == 0)
            targetRenderer = new List<Renderer> { GetComponent<Renderer>() };

        // Get a unique material instance
        foreach (var renderer in targetRenderer)
        {
            _mats.Add(renderer.material);
        }
    }

    public void SetTemperature(float temperature)
    {
        // Normalize 0–1 between min and max temp
        float t = Mathf.InverseLerp(minTemperature, maxTemperature, temperature);

        // Get color from gradient
        Color c = temperatureGradient.Evaluate(t);

        foreach (var m in _mats)
        {
            if (!m) continue;

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); // URP Lit
            else if (m.HasProperty("_Color")) m.SetColor("_Color", c);    // Standard

            if (useEmission && m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", c * emissionStrength);
                m.EnableKeyword("_EMISSION");
            }
        }
    }
}

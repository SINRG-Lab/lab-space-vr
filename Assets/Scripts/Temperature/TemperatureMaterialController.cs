using UnityEngine;
using System.Collections.Generic;

public class TemperatureMaterialController : MonoBehaviour
{
    [Header("Target")]
    public List<Renderer> targetRenderer_zone1 = new List<Renderer>();
    public List<Renderer> targetRenderer_zone2 = new List<Renderer>();
    public List<Renderer> targetRenderer_zone3 = new List<Renderer>();

    [Header("Temperature Range")]
    public float minTemperature = 0f;      // e.g. 0°C
    public float maxTemperature = 1000f;    // e.g. 100°C

    [Header("Visuals")]
    public Gradient temperatureGradient;   // assign in Inspector (blue → red)
    public bool useEmission = true;
    public float emissionStrength = 2f;

    private readonly List<Material> _matsZone1 = new();
    private readonly List<Material> _matsZone2 = new();
    private readonly List<Material> _matsZone3 = new();
    
    void Awake()
    {
        CacheZoneMaterials(targetRenderer_zone1, _matsZone1);
        CacheZoneMaterials(targetRenderer_zone2, _matsZone2);
        CacheZoneMaterials(targetRenderer_zone3, _matsZone3);
    }

    private void CacheZoneMaterials(List<Renderer> renderers, List<Material> mats)
    {
        mats.Clear();
        if (renderers == null) return;

        foreach (var r in renderers)
        {
            if (!r) continue;
            mats.Add(r.material); // unique instance per renderer
        }
    }

    // public void SetTemperature(float temperature)
    // {
    //     // Normalize 0–1 between min and max temp
    //     float t = Mathf.InverseLerp(minTemperature, maxTemperature, temperature);

    //     // Get color from gradient
    //     Color c = temperatureGradient.Evaluate(t);

    //     foreach (var m in _mats)
    //     {
    //         if (!m) continue;

    //         if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); // URP Lit
    //         else if (m.HasProperty("_Color")) m.SetColor("_Color", c);    // Standard

    //         if (useEmission && m.HasProperty("_EmissionColor"))
    //         {
    //             m.SetColor("_EmissionColor", c * emissionStrength);
    //             m.EnableKeyword("_EMISSION");
    //         }
    //     }
    // }

    public void SetTemperatureZone1(float temperature) => ApplyTemperature(_matsZone1, temperature);
    public void SetTemperatureZone2(float temperature) => ApplyTemperature(_matsZone2, temperature);
    public void SetTemperatureZone3(float temperature) => ApplyTemperature(_matsZone3, temperature);

    private void ApplyTemperature(List<Material> mats, float temperature)
    {
        float t = Mathf.InverseLerp(minTemperature, maxTemperature, temperature);
        Color c = temperatureGradient.Evaluate(t);

        foreach (var m in mats)
        {
            if (!m) continue;

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", c);

            if (useEmission && m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", c * emissionStrength);
                m.EnableKeyword("_EMISSION");
            }
        }
    }
}

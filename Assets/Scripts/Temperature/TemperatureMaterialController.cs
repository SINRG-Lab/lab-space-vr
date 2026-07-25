using UnityEngine;
using System.Collections.Generic;

public class TemperatureMaterialController : MonoBehaviour
{
    private sealed class ThermalMaterial
    {
        public readonly Material Material;
        public readonly Color ColdColor;

        public ThermalMaterial(Material material, Color coldColor)
        {
            Material = material;
            ColdColor = coldColor;
        }
    }

    [Header("Target")]
    public List<Renderer> targetRenderer_zone1 = new List<Renderer>();
    public List<Renderer> targetRenderer_zone2 = new List<Renderer>();
    public List<Renderer> targetRenderer_zone3 = new List<Renderer>();

    [Header("Temperature Range")]
    public float minTemperature = 0f;
    public float maxTemperature = 1000f;

    [Header("Visuals")]
    public Gradient temperatureGradient;
    public bool useEmission = true;
    [Min(0f)] public float emissionStartTemperature = 200f;
    [Min(0f)] public float emissionStrength = 2f;

    private readonly List<ThermalMaterial> _matsZone1 = new();
    private readonly List<ThermalMaterial> _matsZone2 = new();
    private readonly List<ThermalMaterial> _matsZone3 = new();
    private readonly HashSet<Material> _instancedMaterials = new();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        CacheZoneMaterials(targetRenderer_zone1, _matsZone1);
        CacheZoneMaterials(targetRenderer_zone2, _matsZone2);
        CacheZoneMaterials(targetRenderer_zone3, _matsZone3);
    }

    private void OnDestroy()
    {
        foreach (Material material in _instancedMaterials)
        {
            if (material)
                Destroy(material);
        }

        _instancedMaterials.Clear();
    }

    private void CacheZoneMaterials(List<Renderer> renderers, List<ThermalMaterial> mats)
    {
        mats.Clear();
        if (renderers == null)
            return;

        var zoneMaterials = new HashSet<Material>();
        foreach (Renderer targetRenderer in renderers)
        {
            if (!targetRenderer)
                continue;

            // Renderer.materials creates runtime instances for every material slot.
            foreach (Material material in targetRenderer.materials)
            {
                if (!material || !zoneMaterials.Add(material))
                    continue;

                _instancedMaterials.Add(material);
                mats.Add(new ThermalMaterial(material, GetBaseColor(material)));
            }
        }
    }

    public void SetTemperatureZone1(float temperature) => ApplyTemperature(_matsZone1, temperature);
    public void SetTemperatureZone2(float temperature) => ApplyTemperature(_matsZone2, temperature);
    public void SetTemperatureZone3(float temperature) => ApplyTemperature(_matsZone3, temperature);

    private void ApplyTemperature(List<ThermalMaterial> mats, float temperature)
    {
        float heatAmount = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(minTemperature, maxTemperature, temperature));
        Color hotColor = temperatureGradient.Evaluate(heatAmount);

        float emissionStart = Mathf.Clamp(emissionStartTemperature, minTemperature, maxTemperature);
        float emissionAmount = maxTemperature > emissionStart
            ? Mathf.InverseLerp(emissionStart, maxTemperature, temperature)
            : heatAmount;
        emissionAmount *= emissionAmount;

        foreach (ThermalMaterial thermalMaterial in mats)
        {
            Material material = thermalMaterial.Material;
            if (!material)
                continue;

            Color surfaceColor = Color.Lerp(thermalMaterial.ColdColor, hotColor, heatAmount);
            surfaceColor.a = thermalMaterial.ColdColor.a;
            SetBaseColor(material, surfaceColor);

            if (!material.HasProperty(EmissionColorId))
                continue;

            if (useEmission)
            {
                material.SetColor(EmissionColorId, hotColor * (emissionStrength * emissionAmount));
                material.EnableKeyword("_EMISSION");
            }
            else
            {
                material.SetColor(EmissionColorId, Color.black);
                material.DisableKeyword("_EMISSION");
            }
        }
    }

    private static Color GetBaseColor(Material material)
    {
        if (material.HasProperty(BaseColorId))
            return material.GetColor(BaseColorId);
        if (material.HasProperty(ColorId))
            return material.GetColor(ColorId);

        return Color.white;
    }

    private static void SetBaseColor(Material material, Color color)
    {
        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, color);
        else if (material.HasProperty(ColorId))
            material.SetColor(ColorId, color);
    }
}

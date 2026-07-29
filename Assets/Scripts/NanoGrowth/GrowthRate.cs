using System;
using UnityEngine;

public class GrowthRate : MonoBehaviour
{
    public GameObject nanoWire;
    public GameObject catalyst;
    public GameObject catalyst_pivot;
    public Setting_Parameter setting_parameter;
    public bool curr_nano_growth_enabled = true;

    [SerializeField, Min(0.0001f)] private float visualUnitsPerNanometer = 0.1f;
    [SerializeField] private bool logDebug;
    [SerializeField] private float logInterval = 1f;

    private Parameters parameters;
    private Vector3 initialNanoWireScale;
    private Vector3 nanoWireScale;
    private bool initialized;
    private float nextLogTime;

    public bool IsComplete { get; private set; }
    public bool HasFinishedGrowth { get; private set; }
    public float Progress01 { get; private set; }

    private void Awake()
    {
        parameters = new Parameters();
        InitializeVisual();
    }

    private void Start()
    {
        if (!setting_parameter)
            setting_parameter = FindFirstObjectByType<Setting_Parameter>();
    }

    private void Update()
    {
        if (!setting_parameter ||
            !setting_parameter.growth_enabled ||
            !curr_nano_growth_enabled ||
            IsComplete)
        {
            return;
        }

        if (!setting_parameter.TryGetGrowthParameters(
                out double radiusNm,
                out double targetHeightNm))
        {
            return;
        }

        double metersPerSecond = ComputeGrowthRate(parameters, radiusNm);
        if (double.IsNaN(metersPerSecond) ||
            double.IsInfinity(metersPerSecond) ||
            metersPerSecond <= 0d)
        {
            return;
        }

        float targetScaleY =
            initialNanoWireScale.y +
            (float)targetHeightNm * visualUnitsPerNanometer;
        float growthDelta =
            (float)metersPerSecond *
            Time.unscaledDeltaTime *
            Mathf.Max(0f, GlobalSimSpeed.GrowthMultiplier) *
            Mathf.Max(0f, GlobalSimSpeed.Multiplier);

        nanoWireScale.y = Mathf.Min(targetScaleY, nanoWireScale.y + growthDelta);
        nanoWire.transform.localScale = nanoWireScale;
        Progress01 = Mathf.InverseLerp(
            initialNanoWireScale.y,
            targetScaleY,
            nanoWireScale.y);

        UpdateCatalystPosition();

        if (logDebug && Time.unscaledTime >= nextLogTime)
        {
            double nanometersPerSecond = metersPerSecond * 1e9;
            Debug.Log(
                $"Growth rate: {metersPerSecond:E6} m/s ({nanometersPerSecond:F4} nm/s), progress={Progress01:P0}",
                this);
            nextLogTime = Time.unscaledTime + logInterval;
        }

        if (nanoWireScale.y >= targetScaleY)
        {
            IsComplete = true;
            HasFinishedGrowth = true;
            Progress01 = 1f;
            curr_nano_growth_enabled = false;
        }
    }

    public void Configure(Setting_Parameter settings)
    {
        setting_parameter = settings;
    }

    public void ResetGrowth()
    {
        InitializeVisual();
        if (!nanoWire)
            return;

        nanoWireScale = initialNanoWireScale;
        nanoWire.transform.localScale = nanoWireScale;
        curr_nano_growth_enabled = true;
        IsComplete = false;
        HasFinishedGrowth = false;
        Progress01 = 0f;
        enabled = true;
        UpdateCatalystPosition();
    }

    public void CompleteGrowthForDevelopment()
    {
        InitializeVisual();
        if (!nanoWire ||
            !setting_parameter ||
            !setting_parameter.TryGetGrowthParameters(
                out _,
                out double targetHeightNm))
        {
            return;
        }

        nanoWireScale.y =
            initialNanoWireScale.y +
            (float)targetHeightNm * visualUnitsPerNanometer;
        nanoWire.transform.localScale = nanoWireScale;
        curr_nano_growth_enabled = false;
        IsComplete = true;
        HasFinishedGrowth = true;
        Progress01 = 1f;
        UpdateCatalystPosition();
    }

    public void StopGrowth()
    {
        curr_nano_growth_enabled = false;
        HasFinishedGrowth = true;
    }

    private void InitializeVisual()
    {
        if (initialized || !nanoWire)
            return;

        initialNanoWireScale = nanoWire.transform.localScale;
        nanoWireScale = initialNanoWireScale;
        initialized = true;
    }

    private void UpdateCatalystPosition()
    {
        if (catalyst && catalyst_pivot)
            catalyst.transform.position = catalyst_pivot.transform.position;
    }

    public static double GetPrefactor(
        double N0,
        double v,
        double omega,
        double C0,
        double QD,
        double k,
        double T,
        double C_C0)
    {
        return N0 * v * omega * C0 * Math.Exp(-QD) * C_C0 * Math.Sqrt(Math.Log(C_C0));
    }

    public static double GetNucleationBarrier(
        double x,
        double a,
        double k,
        double T,
        double C_C0)
    {
        double numerator = -Math.PI * Math.Pow((x * a) / (k * T), 2.0);
        double denominator = Math.Log(C_C0);
        return Math.Exp(numerator / denominator);
    }

    public static double GetNucleationFrequencyPerUnitArea(Parameters p)
    {
        double prefactor = GetPrefactor(
            p.N0_number_of_atomic_sites,
            p.v_vibrational_frequency,
            p.omega_atomic_volume,
            p.C0,
            p.QD_activation_energy,
            p.k_boltzmann_constant,
            p.T_temperature,
            p.C_C0_supersaturation);

        double barrier = GetNucleationBarrier(
            p.x_edge_energy,
            p.a_automic_size,
            p.k_boltzmann_constant,
            p.T_temperature,
            p.C_C0_supersaturation);

        return prefactor * barrier;
    }

    public static double ComputeGrowthRate(Parameters p, double radius)
    {
        double nucleationFrequency = GetNucleationFrequencyPerUnitArea(p);
        double radiusMeters = radius * 1e-9;
        return nucleationFrequency *
               Math.PI *
               Math.Pow(radiusMeters, 2) *
               p.a_automic_size;
    }
}

public class Parameters
{
    public double N0_number_of_atomic_sites = 1e19;
    public double v_vibrational_frequency = 1e13;
    public double omega_atomic_volume = 2e-29;
    public double QD_activation_energy = 10.0;
    public double T_temperature = 900.0 + 273.15;
    public double C_C0_supersaturation = 1.4;
    public double x_edge_energy = 1e-10;
    public double a_automic_size = 2.7e-10;
    public double k_boltzmann_constant = 1.380649e-23;
    public double r_radius = 100 * 1e-8;
    public double C0 = 1.0 / 2e-29;
}

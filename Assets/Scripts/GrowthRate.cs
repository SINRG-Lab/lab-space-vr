using UnityEngine;
using System;
using TMPro;

public class GrowthRate : MonoBehaviour
{
    [Header("Radius (nm)")]
    string radius;
    // public TextMeshProUGUI radius;

    [Header("Temperature (C)")]
    string temperature;

    [Header("Required Height (nm)")]
    string requied_height;

    public GameObject nanoWire;
    Vector3 nanoWireHeight;

    public float simSpeed = 1e7f;

    private Parameters p;

    private bool growth_enabled = false;

    public GameObject catalyst;
    public GameObject catalyst_pivot;

    double total_time;

    string nanoWire_radius;

    public Setting_Parameter setting_parameter;


    void Start()
    {
        p = new Parameters();
        nanoWireHeight = nanoWire.transform.localScale;

        setting_parameter = FindFirstObjectByType<Setting_Parameter>();
    }

    void Update()
    {
        radius = setting_parameter.radius.text;
        temperature = setting_parameter.temperature.text;
        requied_height = setting_parameter.requied_height.text;

        if (radius != null && double.TryParse(radius, out double rNm) && growth_enabled)
        {
            double mPerSec = ComputeGrowthRate(p, rNm);
            double nmPerSec = mPerSec * 1e9;
            Debug.Log($"Growth rate: {mPerSec:E6} m/s ({nmPerSec:F4} nm/s)");

            nanoWireHeight.y += (float)mPerSec * Time.unscaledDeltaTime * simSpeed;

            nanoWire.transform.localScale = nanoWireHeight;
            double.TryParse(requied_height, out double height);
            total_time = height / (mPerSec * 3600);
            Debug.Log(total_time);
        }

        if (requied_height != null && double.TryParse(requied_height, out double targetNm))
        {
            if (nanoWireHeight.y * 1e-9 >= targetNm * 1e-9)
            {
                growth_enabled = false;
            }
        }

        catalyst.transform.position = catalyst_pivot.transform.position;
    }

    public static double GetPrefactor(double N0, double v, double omega, double C0, double QD, double k, double T, double C_C0)
    {
        return N0 * v * omega * C0 * Math.Exp(-QD) * C_C0 * Math.Sqrt(Math.Log(C_C0));
    }

    public static double GetNucleationBarrier(double x, double a, double k, double T, double C_C0)
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
            p.C_C0_supersaturation
        );

        double barrier = GetNucleationBarrier(
            p.x_edge_energy,
            p.a_automic_size,
            p.k_boltzmann_constant,
            p.T_temperature,
            p.C_C0_supersaturation
        );

        return prefactor * barrier;
    }

    public static double ComputeGrowthRate(Parameters p, double radius)
    {
        double J = GetNucleationFrequencyPerUnitArea(p);
        double r = radius * 1e-9;
        return J * Math.PI * Math.Pow(r, 2) * p.a_automic_size;
    }
}

public class Parameters
    {
        // Default values — replace with yours
        public double N0_number_of_atomic_sites = 1e19;     // m^-2
        public double v_vibrational_frequency = 1e13;       // s^-1
        public double omega_atomic_volume = 2e-29;          // m^3/atom
        public double QD_activation_energy = 10.0;           // dimensionless "in kT" if that's what you intend
        public double T_temperature = 900.0 + 273.15;                // Kelvin if that's what you intend
        public double C_C0_supersaturation = 1.4;           // C/C0 (must be > 1)
        public double x_edge_energy = 1e-10;                // J m^-1
        public double a_automic_size = 2.7e-10;             // m
        public double k_boltzmann_constant = 1.380649e-23;  // J/K
        public double r_radius = 100 * 1e-8;                      // nm (note: your Python uses this as-is)
        public double C0 = 1.0/2e-29;                             // baseline concentration (set appropriately)
    }

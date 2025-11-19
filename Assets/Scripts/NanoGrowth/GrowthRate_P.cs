using UnityEngine;
using Random = UnityEngine.Random;
using System;
using TMPro;

public class GrowthRate_P : MonoBehaviour
{
    private Parameters_P parameter_p;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        parameter_p = new Parameters_P();
    }

    // Update is called once per frame
    void Update()
    {
        double rate = Equation(parameter_p);
        Debug.Log("Rate Value: " + rate);
    }

    public static double Equation(Parameters_P p){
        double expTerm = Math.Exp(p.gamma * p.Omega / (p.R * p.Kconst * p.T));
        double ratioSq = (p.Css_over_C0 * p.Css_over_C0) / (p.P_over_P0 * p.P_over_P0);
        double bracket = 1.0 - ratioSq * (expTerm * expTerm);
        return (p.Omega / p.K7) * p.K1 * p.K3 * p.P * bracket;
    }
}

public class Parameters_P
{
    public double Omega = 2.0e-29;
    public double K1 = 1e18; // Qr -> 150 KJ/mol
    public double K3 = 3; // theta = 90
    public double K7 = 1.5; // theta = 90
    public double P = 133; // Pa
    public double Css_over_C0 = 1.1;
    public double P_over_P0 = 1.3;
    public double gamma = 1.5;
    public double R = 5e-8;
    public double Kconst = 1.380649e-23;
    public double T = 900.0 + 273.15;
}
using UnityEngine;
using TMPro;
using System.Collections;

public class IncreaseTemperature : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text maxValueText_zone1;
    public TMP_Text maxValueText_zone2;
    public TMP_Text maxValueText_zone3;

    public TMP_Text currValueText_zone1;
    public TMP_Text currValueText_zone2;
    public TMP_Text currValueText_zone3;

    [Header("Blinking Indicator")]
    public Renderer blinkRenderer;        // the OTHER object that should blink
    public Material blinkMaterialA;       // e.g. normal / idle material
    public Material blinkMaterialB;
    public float blinkInterval = 0.2f; 
    public TemperatureMaterialController tempMat;

    [Header("Value Settings")]
    public float currentValue_zone1 = 0f;
    public float currentValue_zone2 = 0f;
    public float currentValue_zone3 = 0f;

    public float maxValue_zone1 = 870f;
    public float maxValue_zone2 = 870f;
    public float maxValue_zone3 = 870f;
    public float duration = 3600f; 

    Coroutine routineAll;

    public bool isIncreasingTemperature = false;

    public void ReadMaxFromTextZones()
    {
        if (maxValueText_zone1 && float.TryParse(maxValueText_zone1.text, out var v1)) maxValue_zone1 = v1;
        if (maxValueText_zone2 && float.TryParse(maxValueText_zone2.text, out var v2)) maxValue_zone2 = v2;
        if (maxValueText_zone3 && float.TryParse(maxValueText_zone3.text, out var v3)) maxValue_zone3 = v3;
    }

    public void StartAllZones()
    {
        ReadMaxFromTextZones();
        RestartAll(AnimateAllZones(
            currentValue_zone1, maxValue_zone1,
            currentValue_zone2, maxValue_zone2,
            currentValue_zone3, maxValue_zone3
        ));
    }

    public void ResetAllZones()
    {
        RestartAll(AnimateAllZones(
            currentValue_zone1, 0f,
            currentValue_zone2, 0f,
            currentValue_zone3, 0f
        ));
    }

    // Call this from your "Stop / Cool / Reset" button
     void RestartAll(IEnumerator routine)
    {
        if (routineAll != null) StopCoroutine(routineAll);
        routineAll = StartCoroutine(routine);
    }

    IEnumerator AnimateAllZones(
        float from1, float to1,
        float from2, float to2,
        float from3, float to3)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime * GlobalSimSpeed.Multiplier;
            float t = Mathf.Clamp01(elapsed / duration);

            ApplyAll(
                Mathf.Lerp(from1, to1, t),
                Mathf.Lerp(from2, to2, t),
                Mathf.Lerp(from3, to3, t)
            );

            yield return null;
        }

        ApplyAll(to1, to2, to3);
        routineAll = null;
    }

    void ApplyAll(float v1, float v2, float v3)
    {
        currentValue_zone1 = v1;
        currentValue_zone2 = v2;
        currentValue_zone3 = v3;

        if (currValueText_zone1) currValueText_zone1.text = v1.ToString("0.0");
        if (currValueText_zone2) currValueText_zone2.text = v2.ToString("0.0");
        if (currValueText_zone3) currValueText_zone3.text = v3.ToString("0.0");

        if (tempMat)
        {
            tempMat.SetTemperatureZone1(v1);
            tempMat.SetTemperatureZone2(v2);
            tempMat.SetTemperatureZone3(v3);
        }
    }
}

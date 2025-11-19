using UnityEngine;
using TMPro;

public class IncreaseTemperature : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text maxValueText;
    public TMP_Text currValueText;

    [Header("Blinking Indicator")]
    public Renderer blinkRenderer;        // the OTHER object that should blink
    public Material blinkMaterialA;       // e.g. normal / idle material
    public Material blinkMaterialB;
    public float blinkInterval = 0.2f; 
    public TemperatureMaterialController tempMat;

    [Header("Value Settings")]
    public float currentValue = 0f;
    public float maxValue = 870f;
    public float duration = 3600f; 
    Coroutine currentRoutine;

    public bool isIncreasingTemperature = false;

    public void ReadMaxFromText()
    {
        if (maxValueText == null) return;

        string text = maxValueText.text;   // e.g. "5000"

        if (float.TryParse(text, out float parsed))
        {
            maxValue = parsed;
            Debug.Log("Max value set to: " + maxValue);
        }
        else
        {
            Debug.LogWarning("Could not parse max value from: " + text);
        }
    }

    public void StartValueIncrease()
    {
        // Stop any existing animation first
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
        }

        isIncreasingTemperature = true;
        currentRoutine = StartCoroutine(AnimateValue(currentValue, maxValue));
    }

    // Call this from your "Stop / Cool / Reset" button
    public void ResetToZero()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
        }
        
        isIncreasingTemperature = false;
        currentRoutine = StartCoroutine(AnimateValue(currentValue, 0f));
    }

    private System.Collections.IEnumerator AnimateValue(float from, float to)
    {
        float elapsed = 0f;
        bool useA = true;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime * GlobalSimSpeed.Multiplier;
            float t = Mathf.Clamp01(elapsed / duration);

            currentValue = Mathf.Lerp(from, to, t);

            tempMat.SetTemperature(currentValue);
            currValueText.text = currentValue.ToString("0.0");

            // TODO: use currentValue here:
            // - update slider: mySlider.value = currentValue;
            // - update text: valueText.text = currentValue.ToString("F0");
            // - drive material, particles, etc.

            yield return null;
        }

        currentValue = to;
        currentRoutine = null;
    }
}

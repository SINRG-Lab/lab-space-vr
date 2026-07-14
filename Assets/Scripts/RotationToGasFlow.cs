using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RotationToGasFlow : MonoBehaviour
{
    [Header("Target")]
    public TMP_Text valueText;

    [Header("Grabbable / Rotating Object")]
    public Transform target;      // The grabbable object

    [Header("UI")]
    public Slider progressSlider; // Your progress bar (0–1)

    [Header("Rotation Mapping")]
    public float minAngle = -90f;    // angle at which progress = 0
    public float maxAngle = 90f;   // angle at which progress = 1
    public Axis axis = Axis.Z;

    [Header("Output Range")]
    public float minValue = 0f;     // usually 0
    public float maxValue = 5000f;  // your target max

    public float CurrentValue { get; private set; }

    public enum Axis { X, Y, Z }

    void Update()
    {
        if (target == null) return;

        // 1. Get raw local angle (0–360)
        // float rawAngle = GetLocalAxisAngle(target, axis);
        float rawAngle = GetLocalAxisAngle(target, axis);   // 0..360, e.g. 270

        // 2. Optionally convert to -180..180 (so you can use negative ranges)
        float angle = NormalizeAngle(rawAngle);

        // 3. Map angle → 0–1 using Mathf.InverseLerp
        float t = Mathf.Clamp01(Mathf.InverseLerp(minAngle, maxAngle, angle));

        // 4. Clamp and assign to slider
        CurrentValue = Mathf.Lerp(minValue, maxValue, t);

        if (valueText)
            valueText.text = CurrentValue.ToString("0");

        if (progressSlider)
            progressSlider.value = CurrentValue;
    }

    float GetLocalAxisAngle(Transform t, Axis axis)
    {
        Vector3 e = t.localEulerAngles;
        switch (axis)
        {
            case Axis.X: return e.x;
            case Axis.Y: return e.y;
            case Axis.Z: return e.z;
        }
        return 0f;
    }

    float NormalizeAngle(float angle)
    {
        // convert 0–360 → -180..180
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}

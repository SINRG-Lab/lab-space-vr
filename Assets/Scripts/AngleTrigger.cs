using UnityEngine;
using UnityEngine.Events;

public class AngleTrigger : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Which local axis to read (matches Inspector)")]
    public Axis axis = Axis.Z;

    [Header("Targets (degrees)")]
    public float positiveTarget = 30f;
    public float negativeTarget = -30f;

    [Tooltip("How close (±deg) the angle must be to count as a hit")]
    public float tolerance = 2f;

    [Header("Reference (neutral)")]
    [Tooltip("Use current local rotation as zero reference on Awake")]
    public bool captureNeutralOnAwake = true;

    [Header("Events")]
    public UnityEvent OnPositiveHit;  // fires near +30°
    public UnityEvent OnNegativeHit;  // fires near -30°

    float _neutral;       // neutral angle (deg) on the chosen axis
    bool _posLatched;     // prevents spamming while staying near +30
    bool _negLatched;     // prevents spamming while staying near -30

    void Awake()
    {
        if (captureNeutralOnAwake)
            _neutral = GetInspectorAxisAngle();
    }

    void Update()
    {
        float current = SignedAngleRelativeToNeutral();

        // +30° hit
        if (Mathf.Abs(current - positiveTarget) <= tolerance)
        {
            if (!_posLatched)
            {
                OnPositiveHit?.Invoke();
                _posLatched = true;
                _negLatched = false; // optional: clear the other latch
            }
        }
        else _posLatched = false;

        // -30° hit
        if (Mathf.Abs(current - negativeTarget) <= tolerance)
        {
            if (!_negLatched)
            {
                OnNegativeHit?.Invoke();
                _negLatched = true;
                _posLatched = false;
            }
        }
        else _negLatched = false;
    }

    // === helpers ===

    // Raw local euler on chosen axis (Inspector-like, 0..360)
    float GetInspectorAxisAngle()
    {
        var e = transform.localEulerAngles;
        switch (axis)
        {
            case Axis.X: return e.x;
            case Axis.Y: return e.y;
            default:     return e.z;
        }
    }

    // Signed angle in [-180,180] relative to neutral on that axis
    float SignedAngleRelativeToNeutral()
    {
        float raw = GetInspectorAxisAngle();
        return Mathf.DeltaAngle(_neutral, raw);
    }

    // Optional: call from context menu to set the current pose as neutral (0°)
    [ContextMenu("Set Current As Neutral")]
    void CalibrateNeutral() => _neutral = GetInspectorAxisAngle();
}


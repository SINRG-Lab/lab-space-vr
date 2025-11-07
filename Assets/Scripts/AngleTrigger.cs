using UnityEngine;
using UnityEngine.Events;

public class AngleTrigger : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Read this local axis (matches Inspector)")]
    public Axis axis = Axis.Z;

    [Header("Targets")]
    [Tooltip("Trigger at +target and -target (degrees)")]
    public float target = 30f;
    [Tooltip("How close you must get to count as hit")]
    public float tolerance = 3f;

    [Header("Orientation")]
    [Tooltip("Invert sign if clockwise reads negative, etc.")]
    public bool flipSign = false;

    [Header("Neutral")]
    [Tooltip("Capture current Inspector angle as 0 on Awake")]
    public bool captureNeutralOnAwake = true;

    [Header("Events")]
    public UnityEvent OnPositiveHit;  // near +target
    public UnityEvent OnNegativeHit;  // near -target

    float _neutralDeg;
    bool _posLatched, _negLatched; // prevent spamming while within band

    void Awake()
    {
        if (captureNeutralOnAwake)
            _neutralDeg = GetAxisInspectorDeg();
    }

    void Update()
    {
        float a = SignedRelativeDeg();  // [-180..180], relative to neutral
        if (flipSign) a = -a;

        // +target band
        if (a >= (target - tolerance))
        {
            if (!_posLatched)
            {
                OnPositiveHit?.Invoke();
                _posLatched = true;
                _negLatched = false; // optional
            }
        }
        else _posLatched = false;

        // -target band
        if (a <= -(target - tolerance))
        {
            if (!_negLatched)
            {
                OnNegativeHit?.Invoke();
                _negLatched = true;
                _posLatched = false; // optional
            }
        }
        else _negLatched = false;
    }

    float GetAxisInspectorDeg()
    {
        var e = transform.localEulerAngles;
        switch (axis)
        {
            case Axis.X: return e.x;
            case Axis.Y: return e.y;
            default:     return e.z;
        }
    }

    float SignedRelativeDeg()
    {
        float raw = GetAxisInspectorDeg();       // 0..360 from Inspector
        return Mathf.DeltaAngle(_neutralDeg, raw); // −180..180 around neutral
    }

    [ContextMenu("Set Current As Neutral")]
    void CalibrateNeutral() => _neutralDeg = GetAxisInspectorDeg();
}

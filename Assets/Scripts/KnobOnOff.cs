using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;

public class KnobOnOff : MonoBehaviour
{
    [Header("Interaction")]
    public Grabbable grabbable;           // auto-get if left empty

    [Header("Axis & Angles (degrees)")]
    public Vector3 localAxis = Vector3.up; // knob’s hinge axis in LOCAL space
    public float offAngle = -30f;          // final snapped angle for OFF
    public float onAngle  = +30f;          // final snapped angle for ON
    public float actuationAngle = 20f;     // must exceed this to qualify

    [Header("Timing")]
    public float holdTime = 0.20f;         // dwell time past threshold
    public bool snapWhenSet = true;        // snap to exact on/off

    [Header("Events")]
    public UnityEvent<bool> OnStateChanged; // true=ON, false=OFF

    [Header("Debug")]
    public bool logDebug = false;
    public Color gizmoColor = Color.cyan;

    public bool IsOn { get; private set; }

    // Internals
    Quaternion _refLocalRot;                // neutral rotation (center)
    Vector3 _axisWorld;                     // world-space hinge axis
    float _timerOn, _timerOff;
    bool _wasGrabbed;

    void Awake()
    {
        if (!grabbable) grabbable = GetComponent<Grabbable>();
        localAxis = localAxis.normalized;
        _refLocalRot = transform.localRotation;           // center as reference
    }

    void Update()
    {
        // Keep world axis updated from current transform basis
        _axisWorld = transform.TransformDirection(localAxis);

        bool grabbed = grabbable && (grabbable.SelectingPointsCount > 0);

        // Compute signed angle around the chosen axis relative to neutral
        float angle = GetSignedAngleAroundAxisWorld();

        if (grabbed)
        {
            // dwell timers
            if (angle >= Mathf.Abs(actuationAngle))
            {
                _timerOn += Time.deltaTime;
                _timerOff = 0f;
                if (!IsOn && _timerOn >= holdTime) SetState(true);
            }
            else if (angle <= -Mathf.Abs(actuationAngle))
            {
                _timerOff += Time.deltaTime;
                _timerOn = 0f;
                if (IsOn && _timerOff >= holdTime) SetState(false);
            }
            else
            {
                _timerOn = _timerOff = 0f; // back in neutral band
            }
        }
        else
        {
            // on release, ensure it snaps to the latched state
            if (_wasGrabbed && snapWhenSet) SnapToState();
        }

        if (logDebug)
            Debug.Log($"[Knob] angle={angle:F1} onTimer={_timerOn:F2} offTimer={_timerOff:F2} IsOn={IsOn} grabbed={grabbed}");

        _wasGrabbed = grabbed;
    }

    void SetState(bool on)
    {
        if (IsOn == on) return;
        IsOn = on;
        if (snapWhenSet) SnapToState();
        OnStateChanged?.Invoke(IsOn);
    }

    void SnapToState()
    {
        float target = IsOn ? onAngle : offAngle;
        transform.localRotation = _refLocalRot * Quaternion.AngleAxis(target, localAxis);
    }

    // Robust signed-angle around an arbitrary local axis
    float GetSignedAngleAroundAxisWorld()
    {
        // current and reference “forward” directions orthogonal to axis
        // pick a basis vector that’s not parallel to axis to construct a plane
        Vector3 basis = Mathf.Abs(Vector3.Dot(_axisWorld, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;

        // Project two perpendicular vectors from current & reference onto the plane orthogonal to axis
        Vector3 refDir = (_refLocalRot * Vector3.forward);                // local forward at neutral
        refDir = transform.TransformDirection(refDir);                    // to world
        refDir = Vector3.ProjectOnPlane(refDir, _axisWorld).normalized;

        Vector3 curDir = transform.forward;                               // current world forward
        curDir = Vector3.ProjectOnPlane(curDir, _axisWorld).normalized;

        // Signed angle from refDir to curDir around axis
        float signed = Vector3.SignedAngle(refDir, curDir, _axisWorld);
        return signed;
    }

    // Optional: quick calibration in editor
    [ContextMenu("Set Current As Neutral")]
    void CalibrateNeutral()
    {
        _refLocalRot = transform.localRotation;
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawLine(transform.position, transform.position + _axisWorld * 0.2f);
    }
}

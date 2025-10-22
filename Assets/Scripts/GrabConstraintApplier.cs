using UnityEngine;
using Oculus.Interaction;

[DisallowMultipleComponent]
public class GrabConstraintApplier : MonoBehaviour
{
    public enum Space { World, RelativeToReference, RelativeToGrabStart }

    [Header("Target")]
    [SerializeField] private Grabbable grabbable;   // assign the same GO’s Grabbable
    [SerializeField] private Space space = Space.World;
    [SerializeField] private Transform reference;   // used if RelativeToReference

    [Header("Position limits (in chosen space)")]
    public bool limitX, limitY, limitZ;
    public Vector3 minOffset;  // per-axis mins
    public Vector3 maxOffset;  // per-axis maxs

    [Header("Rotation limits (optional, in chosen space)")]
    public bool lockRotation;
    public Vector3 minEulerOffset;
    public Vector3 maxEulerOffset;

    // runtime
    private bool _enabledRuntime;
    private Pose _spacePoseAtBegin;

    void Reset()
    {
        grabbable = GetComponent<Grabbable>();
    }

    void OnEnable()
    {
        // subscribe to grabbable pointer events via PointableElement
        if (grabbable != null) grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    void OnDisable()
    {
        if (grabbable != null) grabbable.WhenPointerEventRaised -= OnPointerEvent;
        _enabledRuntime = false;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            _spacePoseAtBegin = GetSpacePose();
            _enabledRuntime = true;
        }
        else if (evt.Type == PointerEventType.Unselect || evt.Type == PointerEventType.Cancel)
        {
            _enabledRuntime = false;
        }
    }

    void LateUpdate()
    {
        // Let GrabFreeTransformer move first, then clamp here
        if (!_enabledRuntime || grabbable == null) return;

        Transform t = grabbable.Transform;
        Pose spacePose = _spacePoseAtBegin;
        Quaternion inv = Quaternion.Inverse(spacePose.rotation);

        // To local of constraint space
        Vector3 lp = inv * (t.position - spacePose.position);
        Vector3 le = (inv * t.rotation).eulerAngles;
        le = new Vector3(Norm180(le.x), Norm180(le.y), Norm180(le.z));

        if (limitX) lp.x = Mathf.Clamp(lp.x, minOffset.x, maxOffset.x);
        if (limitY) lp.y = Mathf.Clamp(lp.y, minOffset.y, maxOffset.y);
        if (limitZ) lp.z = Mathf.Clamp(lp.z, minOffset.z, maxOffset.z);

        if (lockRotation)
        {
            le.x = Mathf.Clamp(le.x, minEulerOffset.x, maxEulerOffset.x);
            le.y = Mathf.Clamp(le.y, minEulerOffset.y, maxEulerOffset.y);
            le.z = Mathf.Clamp(le.z, minEulerOffset.z, maxEulerOffset.z);
            t.rotation = spacePose.rotation * Quaternion.Euler(le);
        }

        // Back to world
        t.position = spacePose.position + spacePose.rotation * lp;
    }

    private Pose GetSpacePose()
    {
        switch (space)
        {
            case Space.World:
                return new Pose(Vector3.zero, Quaternion.identity);
            case Space.RelativeToReference:
                if (reference) return new Pose(reference.position, reference.rotation);
                // fallback to parent of target
                var p = grabbable?.Transform?.parent;
                return p ? new Pose(p.position, p.rotation) : new Pose(Vector3.zero, Quaternion.identity);
            case Space.RelativeToGrabStart:
                // use the target’s pose at grab start
                var tr = grabbable?.Transform;
                return tr ? new Pose(tr.position, tr.rotation) : new Pose(Vector3.zero, Quaternion.identity);
        }
        return new Pose(Vector3.zero, Quaternion.identity);
    }

    private static float Norm180(float a) => Mathf.Repeat(a + 180f, 360f) - 180f;

    // Public API so presets / events can drive this
    public void ApplyFrom(GrabConstraintPreset preset)
    {
        if (!preset) return;
        space = (Space)preset.space;
        reference = preset.reference;

        limitX = preset.limitX; limitY = preset.limitY; limitZ = preset.limitZ;
        minOffset = preset.minOffset; maxOffset = preset.maxOffset;

        lockRotation = preset.lockRotation;
        minEulerOffset = preset.minEulerOffset; maxEulerOffset = preset.maxEulerOffset;
    }

    public void EnableConstraints()  => _enabledRuntime = true;
    public void DisableConstraints() => _enabledRuntime = false;
}

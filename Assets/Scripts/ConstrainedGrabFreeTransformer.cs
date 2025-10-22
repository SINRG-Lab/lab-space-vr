using UnityEngine;
using Oculus.Interaction;

[DisallowMultipleComponent]
public class ConstrainedGrabFreeTransformer : MonoBehaviour, ITransformer
{
    public enum ConstraintSpace { World, RelativeToReference, RelativeToGrabStart }

    [Header("Space")]
    public ConstraintSpace space = ConstraintSpace.World;
    public Transform reference; // optional; used if RelativeToReference

    [Header("Position constraints (in selected space)")]
    public bool limitX;
    public Vector3 minOffset;   // interpreted per-axis as min
    public bool limitY;
    public Vector3 maxOffset;   // interpreted per-axis as max
    public bool limitZ;

    [Header("Rotation constraints (optional, in selected space)")]
    public bool lockRotation;          // if true, clamp Euler
    public Vector3 minEulerOffset;     // degrees relative to space
    public Vector3 maxEulerOffset;     // degrees relative to space

    private GrabFreeTransformer _inner;
    private IGrabbable _grabbable;

    // Captured frames for relative clamping
    private Pose _spacePoseAtBegin;    // world-space pose of the chosen space origin at BeginTransform
    private Pose _targetPoseAtBegin;   // world-space pose of target at BeginTransform

    void Awake()
    {
        _inner = GetComponent<GrabFreeTransformer>();
        if (_inner == null) _inner = gameObject.AddComponent<GrabFreeTransformer>();
    }

    // ITransformer ------------------------------------------------------------
    public void Initialize(IGrabbable grabbable)
    {
        _grabbable = grabbable;
        _inner.Initialize(grabbable);
    }

    public void BeginTransform()
    {
        _inner.BeginTransform();

        // Space origin to use for “localization”
        _spacePoseAtBegin = GetSpacePose();

        // Remember where the target started (for RelativeToGrabStart eulers)
        Transform t = _grabbable.Transform;
        _targetPoseAtBegin = new Pose(t.position, t.rotation);
    }

    public void UpdateTransform()
    {
        // Let free move run first
        _inner.UpdateTransform();

        // Then clamp in selected space and push back to world
        Transform t = _grabbable.Transform;

        // Convert current world pose into "space" local
        Pose space = _spacePoseAtBegin;
        Quaternion invSpaceRot = Quaternion.Inverse(space.rotation);

        Vector3 localPos = invSpaceRot * (t.position - space.position);
        Vector3 localEuler = (invSpaceRot * t.rotation).eulerAngles;

        // Normalize to signed eulers for sensible clamping
        localEuler = new Vector3(Norm180(localEuler.x), Norm180(localEuler.y), Norm180(localEuler.z));

        // --- Clamp position (per-axis toggles use minOffset/maxOffset's respective axes) ---
        if (limitX) localPos.x = Mathf.Clamp(localPos.x, minOffset.x, maxOffset.x);
        if (limitY) localPos.y = Mathf.Clamp(localPos.y, minOffset.y, maxOffset.y);
        if (limitZ) localPos.z = Mathf.Clamp(localPos.z, minOffset.z, maxOffset.z);

        // --- Clamp rotation (optional) ---
        if (lockRotation)
        {
            localEuler.x = Mathf.Clamp(localEuler.x, minEulerOffset.x, maxEulerOffset.x);
            localEuler.y = Mathf.Clamp(localEuler.y, minEulerOffset.y, maxEulerOffset.y);
            localEuler.z = Mathf.Clamp(localEuler.z, minEulerOffset.z, maxEulerOffset.z);
        }

        // Convert back to world
        t.position = space.position + (space.rotation * localPos);
        if (lockRotation)
        {
            t.rotation = space.rotation * Quaternion.Euler(localEuler);
        }
    }

    public void EndTransform()
    {
        _inner.EndTransform();
    }

    // Helpers -----------------------------------------------------------------
    private Pose GetSpacePose()
    {
        switch (space)
        {
            case ConstraintSpace.World:
                return new Pose(Vector3.zero, Quaternion.identity);

            case ConstraintSpace.RelativeToReference:
                if (reference != null) return new Pose(reference.position, reference.rotation);
                // Fall back to parent if no reference
                if (_grabbable?.Transform?.parent != null)
                {
                    var p = _grabbable.Transform.parent;
                    return new Pose(p.position, p.rotation);
                }
                return new Pose(Vector3.zero, Quaternion.identity);

            case ConstraintSpace.RelativeToGrabStart:
                // Use the target’s pose *at the moment the grab begins* as the local frame
                return _grabbable != null
                    ? new Pose(_grabbable.Transform.position, _grabbable.Transform.rotation)
                    : new Pose(Vector3.zero, Quaternion.identity);
        }
        return new Pose(Vector3.zero, Quaternion.identity);
    }

    private static float Norm180(float a)
    {
        a = Mathf.Repeat(a + 180f, 360f) - 180f;
        return a;
    }
}

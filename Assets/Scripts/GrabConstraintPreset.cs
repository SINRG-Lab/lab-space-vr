using UnityEngine;

public class GrabConstraintPreset : MonoBehaviour
{
    public GrabConstraintApplier target;

    public GrabConstraintApplier.Space space = GrabConstraintApplier.Space.World;
    public Transform reference;

    public bool limitX, limitY, limitZ;
    public Vector3 minOffset, maxOffset;

    public bool lockRotation;
    public Vector3 minEulerOffset, maxEulerOffset;

    // Call from OnClick or Interactor Unity Event Wrapper
    public void Apply() { if (target) target.ApplyFrom(this); }
}

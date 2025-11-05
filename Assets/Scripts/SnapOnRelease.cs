using UnityEngine;
using Oculus.Interaction;

public class SnapOnRelease : MonoBehaviour
{
    [Header("Trigger zone to snap within")]
    public Collider targetTrigger;            // Must be IsTrigger = true

    [SerializeField] private Grabbable grabbable; // Auto-get if left empty
    [SerializeField] private Transform snapTarget;
    [SerializeField] private Material highlightMaterial;

    Rigidbody rb;
    Renderer rend;
    Material originalMat;

    bool inside;
    bool wasGrabbed;
    bool pendingSnap;
    Vector3 queuedPos; 
    Quaternion queuedRot;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!grabbable) grabbable = GetComponent<Grabbable>();

        rend = GetComponentInChildren<Renderer>();
        originalMat = rend.material; // runtime instance (safe to swap)
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other != targetTrigger) return;
        inside = true;
        if (highlightMaterial) rend.material = highlightMaterial;
    }

    void OnTriggerExit(Collider other)
    {
        if (other != targetTrigger) return;
        inside = false;
        rend.material = originalMat;
    }

    void Update()
    {
        bool isGrabbed = grabbable.SelectingPointsCount > 0;

        rb.constraints = isGrabbed ? RigidbodyConstraints.None : (RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation);

        // Just released while inside -> queue a snap for physics step
        if (inside && wasGrabbed && !isGrabbed && snapTarget)
        {
            queuedPos = snapTarget.position;
            queuedRot = snapTarget.rotation;
            pendingSnap = true;

            // restore material immediately
            rend.material = originalMat;
        }

        wasGrabbed = isGrabbed;
    }

    void FixedUpdate()
    {
        if (!pendingSnap) return;

        // Small nudge so we don't end up buried in the trigger
        Vector3 pos = queuedPos;
        // Vector3 closest = targetTrigger.ClosestPoint(pos);
        // Vector3 normal = pos - closest;
        // if (normal.sqrMagnitude < 1e-8f) normal = targetTrigger.transform.up;
        // normal.Normalize();
        // pos += normal * 0.005f; // 5 mm

        bool wasKinematic = rb.isKinematic;

        // Freeze for the teleport, zero motion
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.position = pos;
        rb.rotation = queuedRot;

        Physics.SyncTransforms();   // make colliders update now

        // Restore prior kinematic state and wake
        rb.isKinematic = wasKinematic;
        rb.WakeUp();

        pendingSnap = false;
    }
}

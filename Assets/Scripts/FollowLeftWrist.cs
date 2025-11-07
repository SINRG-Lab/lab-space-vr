using UnityEngine;

// Follow the LEFT wrist (OVR hand-tracking) with optional face-to-head and smoothing.
public class FollowLeftWrist : MonoBehaviour
{
    [Header("Refs")]
    public OVRHand leftHand;              // assign your Left OVRHand
    public OVRSkeleton leftSkeleton;      // assign Left OVRSkeleton (or leave blank; we'll auto-find)
    public Transform head;                // CenterEyeAnchor or Camera.main.transform
    public Transform contentRoot;         // the thing to move (your menu root)

    [Header("Offsets (local to wrist)")]
    public Vector3 localPosition = new Vector3(0.05f, 0.02f, 0.06f); // x:right, y:up, z:forward from wrist
    public Vector3 localEuler    = new Vector3(0, 90, 0);            // rotate to face user

    [Header("Follow")]
    public float posLerp = 12f;
    public float rotLerp = 12f;
    public bool faceHead = true;          // billboard toward head after applying localEuler

    Transform wrist;                      // cached wrist transform

    void Awake()
    {
        if (!contentRoot) contentRoot = transform;
        if (!head && Camera.main) head = Camera.main.transform;
        if (!leftSkeleton && leftHand) leftSkeleton = leftHand.GetComponentInParent<OVRSkeleton>();
    }

    void OnEnable() { TryCacheWrist(); }

    void Update()
    {
        // (re)cache if skeleton initialized later
        if (!wrist) TryCacheWrist();
        if (!wrist || !contentRoot) return;

        // target pose from wrist + offset
        Vector3 targetPos = wrist.TransformPoint(localPosition);
        Quaternion targetRot = wrist.rotation * Quaternion.Euler(localEuler);

        // optionally orient toward head for readability
        if (faceHead && head)
        {
            Vector3 toHead = (head.position - targetPos).normalized;
            if (toHead.sqrMagnitude > 1e-6f)
                targetRot = Quaternion.Slerp(targetRot, Quaternion.LookRotation(toHead, Vector3.up), 0.7f);
        }

        // smooth follow
        float tPos = 1f - Mathf.Exp(-posLerp * Time.deltaTime);
        float tRot = 1f - Mathf.Exp(-rotLerp * Time.deltaTime);
        contentRoot.position = Vector3.Lerp(contentRoot.position, targetPos, tPos);
        contentRoot.rotation = Quaternion.Slerp(contentRoot.rotation, targetRot, tRot);
    }

    void TryCacheWrist()
    {
        if (!leftSkeleton) return;
        if (!leftSkeleton.IsDataValid || leftSkeleton.Bones == null || leftSkeleton.Bones.Count == 0) return;

        foreach (var b in leftSkeleton.Bones)
        {
            if (b.Id == OVRSkeleton.BoneId.Hand_WristRoot) { wrist = b.Transform; break; }
        }
        // Fallback if wrist not found yet: use the hand transform itself
        if (!wrist && leftHand) wrist = leftHand.transform;
    }
}

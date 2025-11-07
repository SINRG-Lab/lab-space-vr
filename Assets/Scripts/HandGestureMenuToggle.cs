using UnityEngine;

public class HandGestureMenuToggle : MonoBehaviour
{
    [Header("Refs")]
    public OVRHand leftHand;            // assign LeftHandAnchor's OVRHand (hand-tracking)
    public Transform head;              // CenterEyeAnchor or Camera.main.transform
    public GameObject menuRoot;         // your left-hand menu (Canvas root)

    [Header("Gesture")]
    public bool requirePalmFacingHead = true;
    [Range(0f, 1f)] public float palmFacingThreshold = 0.6f; // cos(angle) ~ 0.6 ≈ 53°
    public float holdTimeToToggle = 0.2f;   // pinch must be held this long

    [Header("Debounce")]
    public float toggleCooldown = 0.4f;     // prevent rapid re-toggles

    float pinchTimer = 0f;
    float nextAllowedToggleTime = 0f;

    void Reset()
    {
        if (!head && Camera.main) head = Camera.main.transform;
    }

    void Update()
    {
        if (!leftHand || !leftHand.IsTracked || !menuRoot || !head) return;

        // 1) Check palm facing (optional)
        if (requirePalmFacingHead && !IsPalmFacingHead(leftHand, head, palmFacingThreshold))
        {
            // reset timer if palm not in pose
            pinchTimer = 0f;
            return;
        }

        // 2) Pinch detection (index–thumb)
        bool pinching = leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        if (pinching)
        {
            pinchTimer += Time.deltaTime;

            // 3) Toggle when pinch held for long enough and cooldown passed
            if (pinchTimer >= holdTimeToToggle && Time.time >= nextAllowedToggleTime)
            {
                menuRoot.SetActive(!menuRoot.activeSelf);
                nextAllowedToggleTime = Time.time + toggleCooldown;
                pinchTimer = 0f; // reset so we don't keep toggling while still pinching
            }
        }
        else
        {
            pinchTimer = 0f;
        }
    }

    // Heuristic: use the hand's transform to approximate palm normal.
    // For OVRHand prefabs, Transform.up is a decent approximation of palm normal.
    // We consider palm "facing head" when the normal points toward the head.
    static bool IsPalmFacingHead(OVRHand hand, Transform head, float cosThreshold)
    {
        Transform t = hand.transform;
        Vector3 palmNormal = t.up;                        // heuristic; flip if your hand is inverted
        Vector3 toHead     = (head.position - t.position).normalized;
        float cos = Vector3.Dot(palmNormal.normalized, toHead);
        return cos >= cosThreshold;
    }
}

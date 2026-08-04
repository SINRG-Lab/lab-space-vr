using System.Collections;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class FeedRailController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody ownerRb;
    [SerializeField] private Grabbable grabbable;
    [SerializeField] private AutoConnectEnd connectionEnd;
    [SerializeField] private Transform railStart;
    [SerializeField] private Transform railEnd;
    [Tooltip("Exact final position for the delivered substrate plate. Defaults to Rail End.")]
    [SerializeField] private Transform deliveredBodyEndTarget;

    [Header("Rail Behavior")]
    [Tooltip("Aligns the rod's connector axis with the insertion rail during connection.")]
    [SerializeField] private bool alignConnectionToRail = true;
    [SerializeField] private bool reverseConnectionAxis;
    [SerializeField] private bool lockRotation = true;
    [SerializeField, Min(0f)] private float movementSmoothTime = 0.045f;
    [SerializeField, Min(0f)] private float endpointTolerance = 0.0015f;
    [SerializeField, Range(0f, 1f)] private float completionThreshold = 0.96f;
    [SerializeField, Range(0f, 1f)] private float withdrawalCompletionThreshold = 0.04f;
    [SerializeField, Range(0f, 1f)] private float resetThreshold = 0.9f;
    [SerializeField] private bool resetCompletionWhenRetracted = true;
    [SerializeField] private bool lockAtCompletion = true;
    [SerializeField] private bool disconnectAtCompletion = true;
    [SerializeField] private bool lockDeliveredBody = true;

    [Header("Guidance")]
    [SerializeField] private bool showGuideWhileGrabbed = true;
    [SerializeField] private Material guideBaseMaterial;
    [SerializeField] private Color guideColor = new(0.15f, 0.75f, 1f, 0.35f);
    [SerializeField] private Color completeGuideColor = new(0.2f, 1f, 0.4f, 0.65f);
    [SerializeField, Min(0.001f)] private float guideWidth = 0.008f;
    [SerializeField, Min(0.001f)] private float endMarkerSize = 0.04f;

    [Header("Procedure")]
    [SerializeField] private FurnaceProcedureManager procedureManager;
    [SerializeField] private FurnaceProcedureManager.Gate procedureGate =
        FurnaceProcedureManager.Gate.SubstrateFedIntoTube;
    [SerializeField] private FurnaceProcedureManager.Gate withdrawalGate =
        FurnaceProcedureManager.Gate.SubstrateWithdrawn;
    [SerializeField] private bool restrictInteractionToCurrentStep = true;

    [Header("Events")]
    public UnityEvent OnRailActivated;
    public UnityEvent<float> OnProgressChanged;
    public UnityEvent OnFeedCompleted;
    public UnityEvent OnFeedReset;
    public UnityEvent OnWithdrawalStarted;
    public UnityEvent OnWithdrawalCompleted;

    private GameObject guideRoot;
    private GameObject endMarker;
    private LineRenderer guideLine;
    private Renderer endMarkerRenderer;
    private Material runtimeGuideMaterial;
    private Material runtimeCompleteMaterial;
    private Vector3 ownerStartPosition;
    private Quaternion ownerStartRotation;
    private bool railActive;
    private bool wasConnected;
    private bool feedCompleted;
    private bool withdrawalUnlocked;
    private bool withdrawalActive;
    private bool settlingToEnd;
    private bool settlingToStart;
    private bool deliveredBodyControlled;
    private bool deliveredBodyLocked;
    private float progress;
    private float constrainedDistance;
    private float distanceVelocity;
    private Rigidbody deliveredBody;
    private RigidbodyConstraints deliveredBodyConstraints;
    private bool deliveredBodyKinematicState;
    private Vector3 deliveredBodyStartPosition;
    private Quaternion deliveredBodyStartRotation;
    private Vector3 initialOwnerPosition;
    private Quaternion initialOwnerRotation;
    private bool initialOwnerKinematicState;
    private RigidbodyConstraints initialOwnerConstraints;
    private bool hasDeliveredBodyPose;
    private Vector3 withdrawalInnerOwnerPosition;
    private Quaternion withdrawalInnerOwnerRotation;
    private Coroutine developmentFeedRoutine;
    private Coroutine developmentWithdrawalRoutine;

    public float Progress => progress;
    public bool FeedCompleted => feedCompleted;
    public bool WithdrawalActive => withdrawalActive;
    public float DeliveredBodyEndpointError =>
        deliveredBody && DeliveredBodyTarget
            ? Vector3.Distance(deliveredBody.position, DeliveredBodyTarget.position)
            : 0f;

    private Transform DeliveredBodyTarget =>
        deliveredBodyEndTarget ? deliveredBodyEndTarget : railEnd;

    private void Reset()
    {
        ownerRb = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();
        connectionEnd = GetComponentInChildren<AutoConnectEnd>();
    }

    private void Awake()
    {
        if (!ownerRb)
        {
            ownerRb = GetComponent<Rigidbody>();
        }

        if (!grabbable)
        {
            grabbable = GetComponent<Grabbable>();
        }

        if (!connectionEnd)
        {
            connectionEnd = GetComponentInChildren<AutoConnectEnd>();
        }

        if (!deliveredBodyEndTarget)
        {
            deliveredBodyEndTarget = railEnd;
        }

        if (!ownerRb || !grabbable || !connectionEnd || !railStart || !railEnd)
        {
            Debug.LogError(
                $"{nameof(FeedRailController)} on {name} requires a Rigidbody, Grabbable, connection end, and rail endpoints.",
                this);
            enabled = false;
            return;
        }

        if (!TryGetRail(out _, out _))
        {
            Debug.LogError($"{nameof(FeedRailController)} on {name} has overlapping rail endpoints.", this);
            enabled = false;
            return;
        }

        SetupGuide();
        ResolveProcedureManager();
        initialOwnerPosition = ownerRb.position;
        initialOwnerRotation = ownerRb.rotation;
        initialOwnerKinematicState = ownerRb.isKinematic;
        initialOwnerConstraints = ownerRb.constraints;
    }

    private void Update()
    {
        bool isConnected = connectionEnd.IsConnected;

        if (isConnected && !wasConnected)
        {
            if (IsWithdrawalStep())
                ActivateWithdrawalRail();
            else
                ActivateRail();
        }
        else if (!isConnected && wasConnected)
        {
            if (withdrawalActive)
                CancelWithdrawal();
            else
                DeactivateRail(true);
        }

        bool isGrabbed = grabbable.SelectingPointsCount > 0;
        bool procedureAvailable = IsProcedureAvailable();
        SetGuideVisible(
            procedureAvailable && railActive && (!showGuideWhileGrabbed || isGrabbed));

        if (guideRoot && guideRoot.activeSelf)
        {
            UpdateGuidePose();
        }

        wasConnected = isConnected;
    }

    private void LateUpdate()
    {
        if (!railActive || !connectionEnd.IsConnected)
        {
            return;
        }

        if (withdrawalActive)
        {
            UpdateWithdrawalRail();
            return;
        }

        UpdateFeedRail();
    }

    private void UpdateFeedRail()
    {
        bool isGrabbed = grabbable.SelectingPointsCount > 0;
        if ((!IsProcedureAvailable() && !settlingToEnd) || (!isGrabbed && !settlingToEnd))
        {
            return;
        }

        if (!TryGetRail(out Vector3 railAxis, out float railLength))
        {
            return;
        }

        float targetDistance = settlingToEnd
            ? railLength
            : Vector3.Dot(ownerRb.position - ownerStartPosition, railAxis);
        targetDistance = Mathf.Clamp(targetDistance, 0f, railLength);
        if (feedCompleted && lockAtCompletion && !withdrawalUnlocked)
        {
            targetDistance = railLength;
        }

        constrainedDistance = movementSmoothTime > 0f
            ? Mathf.SmoothDamp(
                constrainedDistance,
                targetDistance,
                ref distanceVelocity,
                movementSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime)
            : targetDistance;

        ApplyRailPose(railAxis, constrainedDistance);
        float normalizedProgress = constrainedDistance / railLength;
        SetProgress(normalizedProgress);

        if (!feedCompleted && !settlingToEnd && normalizedProgress >= completionThreshold)
        {
            settlingToEnd = true;
        }

        if (settlingToEnd && railLength - constrainedDistance <= endpointTolerance)
        {
            constrainedDistance = railLength;
            distanceVelocity = 0f;
            ApplyRailPose(railAxis, railLength);
            CompleteFeed();
        }
    }

    private void UpdateWithdrawalRail()
    {
        bool isGrabbed = grabbable.SelectingPointsCount > 0;
        if ((!IsProcedureAvailable() && !settlingToStart) ||
            (!isGrabbed && !settlingToStart))
        {
            return;
        }

        if (!TryGetRail(out Vector3 railAxis, out float railLength))
            return;

        float targetDistance = settlingToStart
            ? 0f
            : railLength +
              Vector3.Dot(ownerRb.position - withdrawalInnerOwnerPosition, railAxis);
        targetDistance = Mathf.Clamp(targetDistance, 0f, railLength);

        constrainedDistance = movementSmoothTime > 0f
            ? Mathf.SmoothDamp(
                constrainedDistance,
                targetDistance,
                ref distanceVelocity,
                movementSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime)
            : targetDistance;

        ApplyWithdrawalPose(constrainedDistance, railLength);
        float normalizedProgress = constrainedDistance / railLength;
        SetProgress(normalizedProgress);

        if (!settlingToStart &&
            normalizedProgress <= withdrawalCompletionThreshold)
        {
            settlingToStart = true;
        }

        if (settlingToStart && constrainedDistance <= endpointTolerance)
        {
            constrainedDistance = 0f;
            distanceVelocity = 0f;
            ApplyWithdrawalPose(0f, railLength);
            CompleteWithdrawal();
        }
    }

    public void ActivateRail()
    {
        if (!connectionEnd.IsConnected)
        {
            return;
        }

        ownerStartPosition = ownerRb.position;
        ownerStartRotation = ownerRb.rotation;
        constrainedDistance = 0f;
        distanceVelocity = 0f;
        railActive = true;
        feedCompleted = false;
        withdrawalUnlocked = false;
        withdrawalActive = false;
        settlingToEnd = false;
        settlingToStart = false;
        CaptureDeliveredBody();
        SetProgress(0f, true);
        SetGuideState(false);
        OnRailActivated?.Invoke();
    }

    private void ActivateWithdrawalRail()
    {
        if (!connectionEnd.IsConnected ||
            !feedCompleted ||
            !deliveredBody ||
            !DeliveredBodyTarget ||
            !hasDeliveredBodyPose ||
            !TryGetRail(out _, out float railLength))
        {
            return;
        }

        withdrawalInnerOwnerPosition = ownerRb.position;
        withdrawalInnerOwnerRotation = ownerRb.rotation;
        constrainedDistance = railLength;
        distanceVelocity = 0f;
        railActive = true;
        withdrawalUnlocked = true;
        withdrawalActive = true;
        settlingToEnd = false;
        settlingToStart = false;

        RestoreDeliveredBodyConstraints();
        StopDynamicMotion(deliveredBody);
        deliveredBody.isKinematic = true;
        deliveredBodyControlled = true;

        SetProgress(1f, true);
        SetGuideState(false);
        OnWithdrawalStarted?.Invoke();
    }

    public void ResetRail()
    {
        bool wasComplete = feedCompleted;
        ownerStartPosition = ownerRb.position;
        ownerStartRotation = ownerRb.rotation;
        constrainedDistance = 0f;
        distanceVelocity = 0f;
        feedCompleted = false;
        withdrawalUnlocked = false;
        withdrawalActive = false;
        settlingToEnd = false;
        settlingToStart = false;
        RestoreDeliveredBodyConstraints();
        SetProgress(0f, true);
        SetGuideState(false);

        if (wasComplete)
        {
            OnFeedReset?.Invoke();
        }
    }

    private void DeactivateRail(bool resetCompletion)
    {
        railActive = false;
        withdrawalActive = false;
        settlingToEnd = false;
        settlingToStart = false;
        distanceVelocity = 0f;
        SetGuideVisible(false);
        RestoreDeliveredBodyConstraints();

        if (resetCompletion && feedCompleted)
        {
            feedCompleted = false;
            OnFeedReset?.Invoke();
        }

        SetProgress(0f, true);
    }

    public void UnlockForWithdrawal()
    {
        withdrawalUnlocked = true;
        RestoreDeliveredBodyConstraints();
    }

    public void CompleteFeedForDevelopment(bool instant)
    {
        if (!connectionEnd.IsConnected)
            return;

        if (!railActive)
            ActivateRail();

        if (!deliveredBody || !DeliveredBodyTarget)
            return;

        if (developmentFeedRoutine != null)
            StopCoroutine(developmentFeedRoutine);

        developmentFeedRoutine = StartCoroutine(
            TranslateSubstrateToCenterForDevelopment(instant ? 0f : 0.8f));
    }

    public void CompleteWithdrawalForDevelopment(bool instant)
    {
        if (developmentWithdrawalRoutine != null)
            StopCoroutine(developmentWithdrawalRoutine);

        developmentWithdrawalRoutine = StartCoroutine(
            WithdrawForDevelopment(instant ? 0f : 1.2f));
    }

    public void ResetForDevelopment()
    {
        if (developmentFeedRoutine != null)
        {
            StopCoroutine(developmentFeedRoutine);
            developmentFeedRoutine = null;
        }

        if (developmentWithdrawalRoutine != null)
        {
            StopCoroutine(developmentWithdrawalRoutine);
            developmentWithdrawalRoutine = null;
        }

        bool wasComplete = feedCompleted;
        if (connectionEnd.IsConnected)
            connectionEnd.Disconnect();

        RestoreDeliveredBodyConstraints();
        if (deliveredBody && hasDeliveredBodyPose)
        {
            StopDynamicMotion(deliveredBody);
            deliveredBody.isKinematic = true;
            deliveredBody.position = deliveredBodyStartPosition;
            deliveredBody.rotation = deliveredBodyStartRotation;
            deliveredBody.constraints = deliveredBodyConstraints;
            deliveredBody.isKinematic = deliveredBodyKinematicState;
            StopDynamicMotion(deliveredBody);
            if (!deliveredBody.isKinematic)
            {
                deliveredBody.WakeUp();
            }
        }

        StopDynamicMotion(ownerRb);
        ownerRb.isKinematic = true;
        ownerRb.position = initialOwnerPosition;
        ownerRb.rotation = initialOwnerRotation;
        ownerRb.constraints = initialOwnerConstraints;
        ownerRb.isKinematic = initialOwnerKinematicState;
        StopDynamicMotion(ownerRb);
        if (!ownerRb.isKinematic)
        {
            ownerRb.WakeUp();
        }

        railActive = false;
        wasConnected = false;
        feedCompleted = false;
        withdrawalUnlocked = false;
        withdrawalActive = false;
        settlingToEnd = false;
        settlingToStart = false;
        deliveredBodyControlled = false;
        deliveredBodyLocked = false;
        constrainedDistance = 0f;
        distanceVelocity = 0f;
        SetProgress(0f, true);
        SetGuideState(false);
        SetGuideVisible(false);
        Physics.SyncTransforms();

        if (wasComplete)
            OnFeedReset?.Invoke();

        ResolveProcedureManager();
        procedureManager?.SetSubstrateFedIntoTube(false);
        procedureManager?.SetSubstrateWithdrawn(false);
    }

    private IEnumerator TranslateSubstrateToCenterForDevelopment(float motionDuration)
    {
        Vector3 startPosition = deliveredBody.position;
        Quaternion startRotation = deliveredBody.rotation;
        StopDynamicMotion(deliveredBody);
        deliveredBody.isKinematic = true;

        float elapsed = 0f;
        while (elapsed < motionDuration)
        {
            elapsed += Time.unscaledDeltaTime * Mathf.Max(0.01f, GlobalSimSpeed.Multiplier);
            float t = motionDuration <= 0f
                ? 1f
                : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / motionDuration));
            deliveredBody.position = Vector3.Lerp(
                startPosition,
                DeliveredBodyTarget.position,
                t);
            deliveredBody.rotation = startRotation;
            yield return null;
        }

        deliveredBody.position = DeliveredBodyTarget.position;
        deliveredBody.rotation = startRotation;
        developmentFeedRoutine = null;
        Physics.SyncTransforms();
        CompleteFeed();
    }

    private IEnumerator WithdrawForDevelopment(float motionDuration)
    {
        UnlockForWithdrawal();
        Vector3 rodStartPosition = ownerRb.position;
        Quaternion rodStartRotation = ownerRb.rotation;

        Rigidbody substrateBody = deliveredBody;
        Vector3 substrateStartPosition = substrateBody
            ? substrateBody.position
            : Vector3.zero;
        Quaternion substrateStartRotation = substrateBody
            ? substrateBody.rotation
            : Quaternion.identity;

        StopDynamicMotion(ownerRb);
        ownerRb.isKinematic = true;
        if (substrateBody)
        {
            StopDynamicMotion(substrateBody);
            substrateBody.isKinematic = true;
        }

        float elapsed = 0f;
        while (elapsed < motionDuration)
        {
            elapsed += Time.unscaledDeltaTime * Mathf.Max(0.01f, GlobalSimSpeed.Multiplier);
            float t = motionDuration <= 0f
                ? 1f
                : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / motionDuration));

            ownerRb.position = Vector3.Lerp(rodStartPosition, ownerStartPosition, t);
            ownerRb.rotation = Quaternion.Slerp(rodStartRotation, ownerStartRotation, t);
            if (substrateBody && hasDeliveredBodyPose)
            {
                substrateBody.position =
                    Vector3.Lerp(substrateStartPosition, deliveredBodyStartPosition, t);
                substrateBody.rotation =
                    Quaternion.Slerp(substrateStartRotation, deliveredBodyStartRotation, t);
            }

            if (motionDuration > 0f)
                yield return null;
        }

        ownerRb.position = ownerStartPosition;
        ownerRb.rotation = ownerStartRotation;
        ownerRb.constraints = initialOwnerConstraints;
        ownerRb.isKinematic = initialOwnerKinematicState;
        StopDynamicMotion(ownerRb);
        if (!ownerRb.isKinematic)
        {
            ownerRb.WakeUp();
        }

        if (substrateBody && hasDeliveredBodyPose)
        {
            substrateBody.position = deliveredBodyStartPosition;
            substrateBody.rotation = deliveredBodyStartRotation;
            substrateBody.constraints = deliveredBodyConstraints;
            substrateBody.isKinematic = deliveredBodyKinematicState;
            StopDynamicMotion(substrateBody);
            if (!substrateBody.isKinematic)
            {
                substrateBody.WakeUp();
            }
        }

        LockDeliveredBodyAtCurrentPose();
        developmentWithdrawalRoutine = null;
        feedCompleted = false;
        withdrawalUnlocked = false;
        withdrawalActive = false;
        settlingToStart = false;
        railActive = false;
        Physics.SyncTransforms();
        ResolveProcedureManager();
        procedureManager?.SetSubstrateFedIntoTube(false);
        procedureManager?.MarkSubstrateWithdrawn();
        OnWithdrawalCompleted?.Invoke();
        FurnaceInteractionFeedback.PlayActionConfirmed();
    }

    private void SetProgress(float value, bool forceEvent = false)
    {
        value = Mathf.Clamp01(value);
        if (!forceEvent && Mathf.Abs(progress - value) < 0.001f)
        {
            return;
        }

        progress = value;
        OnProgressChanged?.Invoke(progress);

        if (!withdrawalActive &&
            feedCompleted &&
            resetCompletionWhenRetracted &&
            progress < resetThreshold)
        {
            feedCompleted = false;
            SetGuideState(false);
            OnFeedReset?.Invoke();
        }
    }

    public bool TryCalculateConnectionPose(
        Vector3 targetConnectorPosition,
        out Vector3 targetPosition,
        out Quaternion targetRotation)
    {
        targetPosition = ownerRb ? ownerRb.position : Vector3.zero;
        targetRotation = ownerRb ? ownerRb.rotation : Quaternion.identity;

        if (!alignConnectionToRail || !ownerRb || !connectionEnd ||
            !TryGetRail(out Vector3 railAxis, out _))
        {
            return false;
        }

        Vector3 connectorOffset = connectionEnd.snapPoint.position - ownerRb.position;
        if (connectorOffset.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        Vector3 desiredAxis = reverseConnectionAxis ? -railAxis : railAxis;
        Quaternion rotationDelta = Quaternion.FromToRotation(
            connectorOffset.normalized,
            desiredAxis);
        targetRotation = rotationDelta * ownerRb.rotation;
        targetPosition = targetConnectorPosition - rotationDelta * connectorOffset;
        return true;
    }

    private void ApplyRailPose(Vector3 railAxis, float distance)
    {
        ownerRb.position = ownerStartPosition + railAxis * distance;
        if (lockRotation)
        {
            ownerRb.rotation = ownerStartRotation;
        }

        if (deliveredBody && connectionEnd.IsConnected)
        {
            float railLength = Vector3.Distance(railStart.position, railEnd.position);
            float normalizedDistance = railLength > 0.0001f
                ? Mathf.Clamp01(distance / railLength)
                : 1f;
            deliveredBody.position = Vector3.Lerp(
                deliveredBodyStartPosition,
                DeliveredBodyTarget.position,
                normalizedDistance);
            deliveredBody.rotation = deliveredBodyStartRotation;
        }
    }

    private void ApplyWithdrawalPose(float distance, float railLength)
    {
        float normalizedDistance = railLength > 0.0001f
            ? Mathf.Clamp01(distance / railLength)
            : 0f;

        ownerRb.position = Vector3.Lerp(
            ownerStartPosition,
            withdrawalInnerOwnerPosition,
            normalizedDistance);
        if (lockRotation)
        {
            ownerRb.rotation = Quaternion.Slerp(
                ownerStartRotation,
                withdrawalInnerOwnerRotation,
                normalizedDistance);
        }

        if (!deliveredBody)
            return;

        deliveredBody.position = Vector3.Lerp(
            deliveredBodyStartPosition,
            DeliveredBodyTarget.position,
            normalizedDistance);
        deliveredBody.rotation = deliveredBodyStartRotation;
    }

    private void CompleteWithdrawal()
    {
        if (!withdrawalActive)
            return;

        SetProgress(0f, true);
        SetGuideState(true);

        connectionEnd.Disconnect();
        wasConnected = false;
        railActive = false;
        feedCompleted = false;
        withdrawalUnlocked = false;
        withdrawalActive = false;
        settlingToStart = false;
        LockDeliveredBodyAtCurrentPose();
        SetGuideVisible(false);

        ResolveProcedureManager();
        procedureManager?.SetSubstrateFedIntoTube(false);
        procedureManager?.MarkSubstrateWithdrawn();
        OnWithdrawalCompleted?.Invoke();
        FurnaceInteractionFeedback.PlayActionConfirmed();
    }

    private void CancelWithdrawal()
    {
        SnapDeliveredBodyToEndpoint();
        if (TryGetRail(out _, out float railLength))
        {
            constrainedDistance = railLength;
            SetProgress(1f, true);
        }

        railActive = false;
        withdrawalUnlocked = false;
        withdrawalActive = false;
        settlingToStart = false;
        distanceVelocity = 0f;
        LockDeliveredBodyAtCurrentPose();
        SetGuideState(false);
        SetGuideVisible(false);
    }

    private void CompleteFeed()
    {
        if (feedCompleted)
        {
            return;
        }

        feedCompleted = true;
        settlingToEnd = false;
        SnapDeliveredBodyToEndpoint();
        SetProgress(1f, true);
        SetGuideState(true);
        FurnaceInteractionFeedback.PlayActionConfirmed();

        // Keep the rod-connected gate true until the feed step has advanced.
        OnFeedCompleted?.Invoke();

        if (disconnectAtCompletion)
        {
            DetachDeliveredBody();
        }
    }

    private void SnapDeliveredBodyToEndpoint()
    {
        if (!deliveredBody || !DeliveredBodyTarget)
            return;

        deliveredBody.position = DeliveredBodyTarget.position;
        deliveredBody.rotation = deliveredBodyStartRotation;
        Physics.SyncTransforms();
    }

    private void CaptureDeliveredBody()
    {
        AutoConnectEnd otherEnd = connectionEnd.ConnectedEnd;
        deliveredBody = otherEnd ? otherEnd.ownerRb : null;
        deliveredBodyLocked = false;

        if (!deliveredBody)
        {
            return;
        }

        deliveredBodyConstraints = deliveredBody.constraints;
        deliveredBodyKinematicState = deliveredBody.isKinematic;
        deliveredBodyStartPosition = deliveredBody.position;
        deliveredBodyStartRotation = deliveredBody.rotation;
        hasDeliveredBodyPose = true;
        StopDynamicMotion(deliveredBody);
        deliveredBody.isKinematic = true;
        deliveredBodyControlled = true;
    }

    private void DetachDeliveredBody()
    {
        connectionEnd.Disconnect();
        wasConnected = false;
        railActive = false;
        SetGuideVisible(false);

        if (!deliveredBody)
        {
            return;
        }

        LockDeliveredBodyAtCurrentPose();
    }

    private void LockDeliveredBodyAtCurrentPose()
    {
        if (!deliveredBody)
            return;

        deliveredBody.constraints = lockDeliveredBody
            ? RigidbodyConstraints.FreezeAll
            : deliveredBodyConstraints;
        deliveredBody.isKinematic = deliveredBodyKinematicState;
        StopDynamicMotion(deliveredBody);
        if (!deliveredBody.isKinematic)
        {
            deliveredBody.WakeUp();
        }
        deliveredBodyControlled = false;
        deliveredBodyLocked = lockDeliveredBody;
    }

    private void RestoreDeliveredBodyConstraints()
    {
        if (!deliveredBody || (!deliveredBodyControlled && !deliveredBodyLocked))
        {
            return;
        }

        deliveredBody.constraints = deliveredBodyConstraints;
        deliveredBody.isKinematic = deliveredBodyKinematicState;
        if (!deliveredBody.isKinematic)
        {
            deliveredBody.WakeUp();
        }

        deliveredBodyControlled = false;
        deliveredBodyLocked = false;
    }

    private static void StopDynamicMotion(Rigidbody body)
    {
        if (!body || body.isKinematic)
        {
            return;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private bool TryGetRail(out Vector3 axis, out float length)
    {
        Vector3 railVector = railEnd.position - railStart.position;
        length = railVector.magnitude;
        axis = length > 0.0001f ? railVector / length : Vector3.zero;
        return length > 0.0001f;
    }

    private void SetupGuide()
    {
        guideRoot = new GameObject($"{name} Feed Rail Guide");
        guideRoot.layer = 2;

        guideLine = guideRoot.AddComponent<LineRenderer>();
        guideLine.useWorldSpace = true;
        guideLine.positionCount = 2;
        guideLine.startWidth = guideWidth;
        guideLine.endWidth = guideWidth;
        guideLine.numCapVertices = 4;
        guideLine.shadowCastingMode = ShadowCastingMode.Off;
        guideLine.receiveShadows = false;

        endMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        endMarker.name = "Feed Rail End Marker";
        endMarker.layer = 2;
        endMarker.transform.SetParent(guideRoot.transform, true);
        endMarker.transform.localScale = Vector3.one * endMarkerSize;

        Collider markerCollider = endMarker.GetComponent<Collider>();
        if (markerCollider)
        {
            Destroy(markerCollider);
        }

        endMarkerRenderer = endMarker.GetComponent<Renderer>();
        endMarkerRenderer.shadowCastingMode = ShadowCastingMode.Off;
        endMarkerRenderer.receiveShadows = false;

        Material sourceMaterial = guideBaseMaterial
            ? guideBaseMaterial
            : endMarkerRenderer.sharedMaterial;
        runtimeGuideMaterial = CreateTransparentMaterial(sourceMaterial, guideColor);
        runtimeCompleteMaterial = CreateTransparentMaterial(sourceMaterial, completeGuideColor);

        SetGuideState(false);
        UpdateGuidePose();
        SetGuideVisible(false);
    }

    private void UpdateGuidePose()
    {
        guideLine.SetPosition(0, railStart.position);
        guideLine.SetPosition(1, railEnd.position);
        endMarker.transform.position = withdrawalActive
            ? railStart.position
            : railEnd.position;
    }

    private void SetGuideState(bool complete)
    {
        Material material = complete ? runtimeCompleteMaterial : runtimeGuideMaterial;
        if (!material)
        {
            return;
        }

        guideLine.sharedMaterial = material;
        endMarkerRenderer.sharedMaterial = material;
    }

    private void SetGuideVisible(bool visible)
    {
        if (guideRoot && guideRoot.activeSelf != visible)
        {
            guideRoot.SetActive(visible);
        }
    }

    private void ResolveProcedureManager()
    {
        if (!procedureManager)
            procedureManager = FindFirstObjectByType<FurnaceProcedureManager>();
    }

    private bool IsProcedureAvailable()
    {
        return !restrictInteractionToCurrentStep ||
               !procedureManager ||
               procedureManager.IsGateRequiredByCurrentStep(procedureGate) ||
               procedureManager.IsGateRequiredByCurrentStep(withdrawalGate);
    }

    private bool IsWithdrawalStep()
    {
        return procedureManager &&
               procedureManager.IsGateRequiredByCurrentStep(withdrawalGate);
    }

    private static Material CreateTransparentMaterial(Material source, Color color)
    {
        Material material = new(source)
        {
            name = $"{source.name} (Feed Rail Guide)",
            renderQueue = (int)RenderQueue.Transparent
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    private void OnDisable()
    {
        if (withdrawalActive)
        {
            CancelWithdrawal();
            wasConnected = false;
            return;
        }

        railActive = false;
        wasConnected = false;
        settlingToEnd = false;
        settlingToStart = false;
        RestoreDeliveredBodyConstraints();
        SetGuideVisible(false);
    }

    private void OnDestroy()
    {
        if (guideRoot)
        {
            Destroy(guideRoot);
        }

        if (runtimeGuideMaterial)
        {
            Destroy(runtimeGuideMaterial);
        }

        if (runtimeCompleteMaterial)
        {
            Destroy(runtimeCompleteMaterial);
        }
    }
}

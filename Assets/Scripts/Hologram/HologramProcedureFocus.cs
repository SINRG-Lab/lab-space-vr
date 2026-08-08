using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class HologramProcedureFocus : MonoBehaviour
{
    public enum StreamMode
    {
        ProcedureFocus,
        OperatorView,
        HandFollow,
        HeadFollow
    }

    [Header("Procedure")]
    [SerializeField] private FurnaceProcedureManager procedureManager;

    [Header("Hologram")]
    [SerializeField] private HologramComposer composer;
    [SerializeField] private HologramSender sender;
    [SerializeField] private bool streamFromFirstStep = true;
    [SerializeField, Min(0.05f)] private float framingRefreshInterval = 0.1f;

    [Header("Stream Mode")]
    [SerializeField] private StreamMode streamMode = StreamMode.ProcedureFocus;

    [Header("Operator Tracking")]
    [SerializeField] private Transform centerEye;
    [SerializeField] private Transform leftHandAnchor;
    [SerializeField] private Transform rightHandAnchor;
    [SerializeField] private Transform leftHandCaptureRoot;
    [SerializeField] private Transform rightHandCaptureRoot;
    [SerializeField] private OVRHand leftHandTracking;
    [SerializeField] private OVRHand rightHandTracking;
    [SerializeField] private Vector3 operatorHeadOffset = new(0f, -0.25f, 0f);
    [SerializeField, Range(0f, 1f)] private float operatorHandWeight = 0.65f;
    [SerializeField, Min(0f)] private float operatorLookSmoothing = 8f;

    [Header("Hand Follow")]
    [SerializeField, Min(0f)] private float handMovementThreshold = 0.1f;
    [SerializeField, Min(0f)] private float handPositionSmoothing = 7f;

    [Header("Head Follow")]
    [SerializeField, Min(0f)] private float headPositionSmoothing = 8f;
    [SerializeField, Min(0f)] private float headRotationSmoothing = 8f;

    private Coroutine focusRoutine;
    private float nextFramingRefresh;
    private int appliedSettingsHash;
    private bool subscribed;
    private bool started;
    private Vector3 trackingReferenceCenter;
    private Quaternion trackingReferenceRotation;
    private Vector3 headReferencePosition;
    private float headReferenceYaw;
    private Vector3 acceptedHandFocus;
    private bool hasAcceptedHandFocus;
    private readonly List<Transform> trackingCaptureSubjects = new();

    public StreamMode CurrentStreamMode => streamMode;
    public event Action<StreamMode> StreamModeChanged;

    private IEnumerator Start()
    {
        ResolveReferences();
        Subscribe();

        // Let the procedure manager activate its first step before framing it.
        yield return null;
        ApplySelectedMode();

        if (streamFromFirstStep)
            sender?.BeginStreaming();

        started = true;
    }

    private void OnEnable()
    {
        if (!started)
            return;

        ResolveReferences();
        Subscribe();
        ScheduleModeRefresh();
        if (streamFromFirstStep)
            sender?.BeginStreaming();
    }

    private void OnDisable()
    {
        if (focusRoutine != null)
        {
            StopCoroutine(focusRoutine);
            focusRoutine = null;
        }

        Unsubscribe();
        composer?.DeactivateCapture();
    }

    private void LateUpdate()
    {
        if (!composer)
            return;

        if (streamMode != StreamMode.ProcedureFocus)
        {
            UpdateTrackingMode();
            return;
        }

        if (Time.unscaledTime < nextFramingRefresh)
            return;

        nextFramingRefresh = Time.unscaledTime + framingRefreshInterval;
        int currentSettingsHash = CalculateCurrentSettingsHash();
        if (currentSettingsHash != appliedSettingsHash)
        {
            ApplyCurrentFocus();
            nextFramingRefresh = Time.unscaledTime + framingRefreshInterval;
            return;
        }

        composer.RefreshFocusFraming();
    }

    private void OnProcedureStepEntered(
        int stepIndex,
        FurnaceProcedureManager.ProcedureStep step)
    {
        if (streamMode == StreamMode.ProcedureFocus)
            ScheduleModeRefresh();
    }

    private void OnProcedureCompleted()
    {
        if (streamMode == StreamMode.ProcedureFocus)
            ScheduleModeRefresh();
    }

    public void SetStreamMode(StreamMode mode)
    {
        bool changed = streamMode != mode;
        streamMode = mode;
        ApplySelectedMode();

        if (changed)
            StreamModeChanged?.Invoke(streamMode);
    }

    public void SetStreamMode(int modeIndex)
    {
        int lastMode = Enum.GetValues(typeof(StreamMode)).Length - 1;
        SetStreamMode((StreamMode)Mathf.Clamp(modeIndex, 0, lastMode));
    }

    public void UseProcedureFocus() => SetStreamMode(StreamMode.ProcedureFocus);
    public void UseOperatorView() => SetStreamMode(StreamMode.OperatorView);
    public void UseHandFollow() => SetStreamMode(StreamMode.HandFollow);
    public void UseHeadFollow() => SetStreamMode(StreamMode.HeadFollow);

    private void ScheduleModeRefresh()
    {
        if (focusRoutine != null)
            StopCoroutine(focusRoutine);

        focusRoutine = StartCoroutine(RefreshModeAfterPresentation());
    }

    private IEnumerator RefreshModeAfterPresentation()
    {
        yield return null;
        ApplySelectedMode();
        focusRoutine = null;
    }

    private void ApplySelectedMode()
    {
        ResolveReferences();
        if (!composer)
            return;

        if (streamMode == StreamMode.ProcedureFocus)
        {
            ApplyCurrentFocus();
            return;
        }

        PrepareTrackingMode();
    }

    private void PrepareTrackingMode()
    {
        BuildTrackingCaptureSubjects();
        composer.ShowTrackingSubjects(
            trackingCaptureSubjects,
            includeApparatus: true);

        trackingReferenceCenter = composer.SourceCameraFocusCenter;
        trackingReferenceRotation = composer.SourceCameraRigRotation;
        hasAcceptedHandFocus = TryGetHandFocus(out acceptedHandFocus);

        if (centerEye)
        {
            headReferencePosition = centerEye.position;
            headReferenceYaw = GetYaw(centerEye.forward);
        }

        UpdateTrackingMode(true);
        nextFramingRefresh = 0f;
    }

    private void UpdateTrackingMode(bool immediate = false)
    {
        if (!composer)
            return;

        float deltaTime = immediate
            ? 1f
            : Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        switch (streamMode)
        {
            case StreamMode.OperatorView:
                if (TryGetOperatorFocus(out Vector3 operatorFocus))
                {
                    composer.AimSourceCamerasAt(
                        operatorFocus,
                        immediate ? 0f : operatorLookSmoothing,
                        deltaTime);
                }
                break;

            case StreamMode.HandFollow:
                UpdateHandFollow(immediate, deltaTime);
                break;

            case StreamMode.HeadFollow:
                UpdateHeadFollow(immediate, deltaTime);
                break;
        }
    }

    private void UpdateHandFollow(bool immediate, float deltaTime)
    {
        if (!TryGetHandFocus(out Vector3 currentHandFocus))
            return;

        if (!hasAcceptedHandFocus ||
            Vector3.Distance(currentHandFocus, acceptedHandFocus) >=
            handMovementThreshold)
        {
            acceptedHandFocus = currentHandFocus;
            hasAcceptedHandFocus = true;
        }

        composer.MoveSourceCameraRig(
            acceptedHandFocus,
            trackingReferenceRotation,
            immediate ? 0f : handPositionSmoothing,
            0f,
            deltaTime);
    }

    private void UpdateHeadFollow(bool immediate, float deltaTime)
    {
        if (!centerEye)
            return;

        Vector3 headDisplacement =
            centerEye.position - headReferencePosition;
        Vector3 targetCenter =
            trackingReferenceCenter + headDisplacement;
        float yawDelta = Mathf.DeltaAngle(
            headReferenceYaw,
            GetYaw(centerEye.forward));
        Quaternion targetRotation =
            Quaternion.AngleAxis(yawDelta, Vector3.up) *
            trackingReferenceRotation;

        composer.MoveSourceCameraRig(
            targetCenter,
            targetRotation,
            immediate ? 0f : headPositionSmoothing,
            immediate ? 0f : headRotationSmoothing,
            deltaTime);
    }

    private bool TryGetOperatorFocus(out Vector3 focus)
    {
        bool hasHands = TryGetHandFocus(out Vector3 handFocus);
        if (centerEye)
        {
            Vector3 headFocus =
                centerEye.TransformPoint(operatorHeadOffset);
            focus = hasHands
                ? Vector3.Lerp(headFocus, handFocus, operatorHandWeight)
                : headFocus;
            return true;
        }

        focus = handFocus;
        return hasHands;
    }

    private bool TryGetHandFocus(out Vector3 focus)
    {
        bool hasLeft = IsHandTracked(leftHandTracking, leftHandAnchor);
        bool hasRight = IsHandTracked(rightHandTracking, rightHandAnchor);

        if (hasLeft && hasRight)
        {
            focus = (leftHandAnchor.position + rightHandAnchor.position) * 0.5f;
            return true;
        }

        if (hasLeft)
        {
            focus = leftHandAnchor.position;
            return true;
        }

        if (hasRight)
        {
            focus = rightHandAnchor.position;
            return true;
        }

        focus = default;
        return false;
    }

    private static bool IsHandTracked(OVRHand hand, Transform anchor)
    {
        if (!anchor || !anchor.gameObject.activeInHierarchy)
            return false;

        if (hand && hand.IsTracked)
            return true;

#if UNITY_EDITOR
        return true;
#else
        return false;
#endif
    }

    private void BuildTrackingCaptureSubjects()
    {
        trackingCaptureSubjects.Clear();
        AddTrackingSubject(leftHandCaptureRoot);
        AddTrackingSubject(rightHandCaptureRoot);
    }

    private void AddTrackingSubject(Transform subject)
    {
        if (subject && !trackingCaptureSubjects.Contains(subject))
            trackingCaptureSubjects.Add(subject);
    }

    private static float GetYaw(Vector3 forward)
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(
            forward,
            Vector3.up);
        if (planarForward.sqrMagnitude < 0.000001f)
            return 0f;

        planarForward.Normalize();
        return Mathf.Atan2(planarForward.x, planarForward.z) *
               Mathf.Rad2Deg;
    }

    private void ApplyCurrentFocus()
    {
        ResolveReferences();
        if (!procedureManager || !composer)
            return;

        FurnaceProcedureManager.ProcedureStep step = procedureManager.CurrentStep;
        if (step == null)
        {
            Transform completionTarget = procedureManager.CurrentIndicatorTarget;
            composer.ShowContextFocus(
                completionTarget ? new[] { completionTarget } : null,
                0.75f);
            appliedSettingsHash = CalculateCurrentSettingsHash();
            return;
        }

        bool useCaptureLayerBounds = step.hologramUseCaptureLayerBounds;
        Transform focusTarget = useCaptureLayerBounds
            ? null
            : GetPrimaryFocusTarget(step);
        float distanceMultiplier = step.hologramDistanceMultiplier > 0f
            ? step.hologramDistanceMultiplier
            : 1f;
        composer.ShowContextFocus(
            focusTarget ? new[] { focusTarget } : null,
            step.hologramContextRadius,
            distanceMultiplier,
            includeApparatus: !step.hologramHideApparatus,
            useAuthoredCameraSpacing: step.hologramUseAuthoredCameraSpacing,
            fieldOfViewOverride: step.hologramFieldOfView,
            cameraYOffset: step.hologramCameraYOffset,
            cameraRotationOffset: step.hologramCameraRotationOffset,
            centerCamerasOnFocus: step.hologramCenterCamerasOnFocus);
        appliedSettingsHash = CalculateCurrentSettingsHash();
        nextFramingRefresh = 0f;
    }

    private int CalculateCurrentSettingsHash()
    {
        if (!procedureManager)
            return 0;

        FurnaceProcedureManager.ProcedureStep step = procedureManager.CurrentStep;
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + procedureManager.CurrentStepIndex;
            if (step == null)
            {
                Transform completionTarget = procedureManager.CurrentIndicatorTarget;
                return hash * 31 + (completionTarget ? completionTarget.GetInstanceID() : 0);
            }

            hash = hash * 31 + (step.hologramUseCaptureLayerBounds ? 1 : 0);
            hash = hash * 31 + (step.hologramHideApparatus ? 1 : 0);
            hash = hash * 31 + step.hologramContextRadius.GetHashCode();
            hash = hash * 31 + step.hologramDistanceMultiplier.GetHashCode();
            hash = hash * 31 + step.hologramFieldOfView.GetHashCode();
            hash = hash * 31 + step.hologramCameraYOffset.GetHashCode();
            hash = hash * 31 + step.hologramCameraRotationOffset.GetHashCode();
            hash = hash * 31 + (step.hologramCenterCamerasOnFocus ? 1 : 0);
            hash = hash * 31 + (step.hologramUseAuthoredCameraSpacing ? 1 : 0);

            if (step.hologramFocusTargets != null)
            {
                hash = hash * 31 + step.hologramFocusTargets.Length;
                for (int i = 0; i < step.hologramFocusTargets.Length; i++)
                {
                    Transform target = step.hologramFocusTargets[i];
                    hash = hash * 31 + (target ? target.GetInstanceID() : 0);
                }
            }

            return hash;
        }
    }

    private static Transform GetPrimaryFocusTarget(
        FurnaceProcedureManager.ProcedureStep step)
    {
        if (step.hologramFocusTargets != null)
        {
            for (int i = 0; i < step.hologramFocusTargets.Length; i++)
            {
                if (step.hologramFocusTargets[i])
                    return step.hologramFocusTargets[i];
            }
        }

        return step.indicatorTarget;
    }

    private void ResolveReferences()
    {
        if (!procedureManager)
            procedureManager = FurnaceProcedureManager.Instance;
        if (!composer)
            composer = FindFirstObjectByType<HologramComposer>(FindObjectsInactive.Include);
        if (!sender)
            sender = FindFirstObjectByType<HologramSender>(FindObjectsInactive.Include);
        if (!centerEye && Camera.main)
            centerEye = Camera.main.transform;
        if (!leftHandTracking && leftHandAnchor)
            leftHandTracking = leftHandAnchor.GetComponentInChildren<OVRHand>(true);
        if (!rightHandTracking && rightHandAnchor)
            rightHandTracking = rightHandAnchor.GetComponentInChildren<OVRHand>(true);
        if (!leftHandCaptureRoot && leftHandTracking)
            leftHandCaptureRoot = leftHandTracking.transform;
        if (!rightHandCaptureRoot && rightHandTracking)
            rightHandCaptureRoot = rightHandTracking.transform;
    }

    private void Subscribe()
    {
        if (subscribed || !procedureManager)
            return;

        procedureManager.StepEntered += OnProcedureStepEntered;
        procedureManager.ProcedureCompleted += OnProcedureCompleted;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || !procedureManager)
            return;

        procedureManager.StepEntered -= OnProcedureStepEntered;
        procedureManager.ProcedureCompleted -= OnProcedureCompleted;
        subscribed = false;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }
}

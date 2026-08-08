using System.Collections;
using UnityEngine;

public sealed class HologramProcedureFocus : MonoBehaviour
{
    [Header("Procedure")]
    [SerializeField] private FurnaceProcedureManager procedureManager;

    [Header("Hologram")]
    [SerializeField] private HologramComposer composer;
    [SerializeField] private HologramSender sender;
    [SerializeField] private bool streamFromFirstStep = true;
    [SerializeField, Min(0.05f)] private float framingRefreshInterval = 0.1f;

    private Coroutine focusRoutine;
    private float nextFramingRefresh;
    private int appliedSettingsHash;
    private bool subscribed;
    private bool started;

    private IEnumerator Start()
    {
        ResolveReferences();
        Subscribe();

        // Let the procedure manager activate its first step before framing it.
        yield return null;
        ApplyCurrentFocus();

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
        ScheduleFocusRefresh();
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
        if (!composer || Time.unscaledTime < nextFramingRefresh)
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
        ScheduleFocusRefresh();
    }

    private void OnProcedureCompleted()
    {
        ScheduleFocusRefresh();
    }

    private void ScheduleFocusRefresh()
    {
        if (focusRoutine != null)
            StopCoroutine(focusRoutine);

        focusRoutine = StartCoroutine(RefreshFocusAfterPresentation());
    }

    private IEnumerator RefreshFocusAfterPresentation()
    {
        yield return null;
        ApplyCurrentFocus();
        focusRoutine = null;
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

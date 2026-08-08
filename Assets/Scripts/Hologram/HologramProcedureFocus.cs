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
            fieldOfViewOverride: step.hologramFieldOfView);
        nextFramingRefresh = 0f;
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

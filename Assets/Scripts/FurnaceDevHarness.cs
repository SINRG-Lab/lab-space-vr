using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public sealed class FurnaceDevHarness : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private FurnaceProcedureManager procedureManager;
    [SerializeField] private FurnaceStationReset stationReset;

    [Header("Development Values")]
    [SerializeField, Min(0f)] private float temperatureSetpoint = 500f;
    [SerializeField, Min(0f)] private float gasFlowSetpoint = 1000f;
    [SerializeField, Min(0f)] private float stepDelay = 0.25f;
    [SerializeField] private bool showPanelOnPlay = true;

    [Header("Resolved Furnace Components")]
    [SerializeField] private AngleTrigger powerControl;
    [SerializeField] private SnapOnRelease substrateSnap;
    [SerializeField] private AutoConnectEnd rodConnector;
    [SerializeField] private AutoConnectEnd substrateConnector;
    [SerializeField] private FeedRailController feedRail;
    [SerializeField] private FurnaceLidState lidState;
    [SerializeField] private RotationToGasFlow gasFlow;
    [SerializeField] private IncreaseTemperature heater;
    [SerializeField] private Setting_Parameter growthSettings;
    [SerializeField] private GrowthManager growthController;
    [SerializeField] private GlobalSimSpeed simulationSpeed;

    private Rect windowRect = new(16f, 48f, 410f, 720f);
    private Vector2 scrollPosition;
    private Coroutine activeRoutine;
    private bool panelVisible;
    private bool autoRunning;
    private bool autoPaused;
    private int selectedStepIndex;
    private string status = "Ready.";

    private static readonly FurnaceProcedureManager.Gate[] AllGates =
        (FurnaceProcedureManager.Gate[])System.Enum.GetValues(
            typeof(FurnaceProcedureManager.Gate));

    private void Awake()
    {
#if !UNITY_EDITOR
        enabled = false;
        return;
#else
        ResolveReferences();
        panelVisible = showPanelOnPlay;
#endif
    }

#if UNITY_EDITOR
    private void Start()
    {
        selectedStepIndex = procedureManager
            ? Mathf.Clamp(procedureManager.CurrentStepIndex, 0, Mathf.Max(0, procedureManager.StepCount - 1))
            : 0;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard[Key.Backquote].wasPressedThisFrame)
            panelVisible = !panelVisible;

        bool shift = keyboard[Key.LeftShift].isPressed ||
                     keyboard[Key.RightShift].isPressed;
        if (!shift)
            return;

        if (keyboard[Key.R].wasPressedThisFrame)
            ResetFromUi();
        else if (keyboard[Key.Enter].wasPressedThisFrame)
            BeginAction(CompleteCurrentStepRoutine(false), false);
        else if (keyboard[Key.N].wasPressedThisFrame)
            BeginAction(CompleteCurrentStepRoutine(true), false);
        else if (keyboard[Key.J].wasPressedThisFrame)
            BeginAction(JumpToStepRoutine(selectedStepIndex), false);
        else if (keyboard[Key.A].wasPressedThisFrame)
            BeginAction(AutoRunRoutine(false), true);
        else if (keyboard[Key.P].wasPressedThisFrame)
            ToggleAutoPause();
        else if (keyboard[Key.LeftBracket].wasPressedThisFrame)
            AdjustSimulationSpeed(-0.5f);
        else if (keyboard[Key.RightBracket].wasPressedThisFrame)
            AdjustSimulationSpeed(0.5f);
        else if (keyboard[Key.Digit1].wasPressedThisFrame)
            TogglePowerFault();
        else if (keyboard[Key.Digit2].wasPressedThisFrame)
            ToggleLidFault();
        else if (keyboard[Key.Digit3].wasPressedThisFrame)
            ToggleGasFault();
    }

    private void OnGUI()
    {
        if (!panelVisible || !Application.isPlaying)
            return;

        windowRect.height = Mathf.Min(720f, Screen.height - 64f);
        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Furnace Development Driver");
    }

    private void DrawWindow(int windowId)
    {
        if (!procedureManager)
        {
            GUILayout.Label("FurnaceProcedureManager was not found.");
            if (GUILayout.Button("Resolve References"))
                ResolveReferences();
            GUI.DragWindow();
            return;
        }

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("Editor only. Quest hand interactions are unchanged.");
        GUILayout.Space(4f);
        GUILayout.Label(
            procedureManager.IsComplete
                ? "Current: Procedure Complete"
                : $"Current: {procedureManager.CurrentStepIndex + 1}. {procedureManager.CurrentStepTitle}");
        GUILayout.Label($"Status: {status}");
        GUILayout.Label($"Simulation speed: {CurrentSimulationSpeed:0.0}x");
        if (growthController)
        {
            GUILayout.Label(
                $"Growth: {growthController.State} ({growthController.Progress01:P0})");
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset"))
            ResetFromUi();
        if (GUILayout.Button("Simulate Current"))
            BeginAction(CompleteCurrentStepRoutine(false), false);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Complete Instant"))
            BeginAction(CompleteCurrentStepRoutine(true), false);
        if (GUILayout.Button("Auto Run"))
            BeginAction(AutoRunRoutine(false), true);
        if (GUILayout.Button("Instant Run"))
            BeginAction(AutoRunRoutine(true), true);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(autoPaused ? "Resume Auto" : "Pause Auto"))
            ToggleAutoPause();
        if (GUILayout.Button("Speed -"))
            AdjustSimulationSpeed(-0.5f);
        if (GUILayout.Button("Speed +"))
            AdjustSimulationSpeed(0.5f);
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("Jump To Step");
        for (int i = 0; i < procedureManager.StepCount; i++)
        {
            FurnaceProcedureManager.ProcedureStep step = procedureManager.GetStepAt(i);
            bool selected = selectedStepIndex == i;
            string marker = selected ? "> " : "  ";
            if (GUILayout.Button($"{marker}{i + 1}. {step?.title ?? step?.id ?? "Unnamed"}"))
                selectedStepIndex = i;
        }

        if (GUILayout.Button("Jump To Selected"))
            BeginAction(JumpToStepRoutine(selectedStepIndex), false);

        GUILayout.Space(8f);
        GUILayout.Label("Safety Faults");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1 Power"))
            TogglePowerFault();
        if (GUILayout.Button("2 Lid"))
            ToggleLidFault();
        if (GUILayout.Button("3 Gas"))
            ToggleGasFault();
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("Stable Gates");
        for (int i = 0; i < AllGates.Length; i++)
        {
            FurnaceProcedureManager.Gate gate = AllGates[i];
            bool satisfied = procedureManager.GetGate(gate);
            Color oldColor = GUI.contentColor;
            GUI.contentColor = satisfied
                ? new Color(0.35f, 1f, 0.55f)
                : new Color(1f, 0.62f, 0.35f);
            GUILayout.Label($"{(satisfied ? "[x]" : "[ ]")} {gate}");
            GUI.contentColor = oldColor;
        }

        GUILayout.Space(8f);
        GUILayout.Label("Cooldown, lid opening, and withdrawal use the Phase 8 flow.");
        GUILayout.Label("Shortcuts: ` panel, Shift+R reset, Shift+Enter simulate, Shift+N instant");
        GUILayout.Label("Shift+J jump, Shift+A auto, Shift+P pause, Shift+[ / ] speed");

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
    }

    private void ResolveReferences()
    {
        if (!procedureManager)
            procedureManager = GetComponent<FurnaceProcedureManager>();
        if (!procedureManager)
            procedureManager = FindFirstObjectByType<FurnaceProcedureManager>(FindObjectsInactive.Include);
        if (!stationReset)
            stationReset = FindFirstObjectByType<FurnaceStationReset>(FindObjectsInactive.Include);
        if (!powerControl)
            powerControl = FindFirstObjectByType<AngleTrigger>(FindObjectsInactive.Include);
        if (!substrateSnap)
            substrateSnap = FindFirstObjectByType<SnapOnRelease>(FindObjectsInactive.Include);
        if (!feedRail)
            feedRail = FindFirstObjectByType<FeedRailController>(FindObjectsInactive.Include);
        if (!lidState)
            lidState = FindFirstObjectByType<FurnaceLidState>(FindObjectsInactive.Include);
        if (!gasFlow)
            gasFlow = FindFirstObjectByType<RotationToGasFlow>(FindObjectsInactive.Include);
        if (!heater)
            heater = FindFirstObjectByType<IncreaseTemperature>(FindObjectsInactive.Include);
        if (!growthSettings)
            growthSettings = FindFirstObjectByType<Setting_Parameter>(FindObjectsInactive.Include);
        if (!growthController)
            growthController = FindFirstObjectByType<GrowthManager>(FindObjectsInactive.Include);
        if (!simulationSpeed)
            simulationSpeed = FindFirstObjectByType<GlobalSimSpeed>(FindObjectsInactive.Include);

        if (!rodConnector || !substrateConnector)
        {
            AutoConnectEnd[] connectors = FindObjectsByType<AutoConnectEnd>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < connectors.Length; i++)
            {
                if (connectors[i].CanInitiateConnection && !rodConnector)
                    rodConnector = connectors[i];
                else if (!connectors[i].CanInitiateConnection && !substrateConnector)
                    substrateConnector = connectors[i];
            }
        }
    }

    private void BeginAction(IEnumerator routine, bool isAutoRun)
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        autoRunning = isAutoRun;
        autoPaused = false;
        activeRoutine = StartCoroutine(RunAction(routine));
    }

    private IEnumerator RunAction(IEnumerator routine)
    {
        yield return routine;
        activeRoutine = null;
        autoRunning = false;
        autoPaused = false;
    }

    private IEnumerator CompleteCurrentStepRoutine(bool instant)
    {
        ResolveReferences();
        if (!procedureManager || procedureManager.IsComplete)
        {
            status = "Procedure is already complete.";
            yield break;
        }

        FurnaceProcedureManager.ProcedureStep step = procedureManager.CurrentStep;
        int startingIndex = procedureManager.CurrentStepIndex;
        status = $"{(instant ? "Completing" : "Simulating")} {step.title}.";

        yield return DriveGates(step.prerequisiteGates, instant);
        if (procedureManager.CurrentStepIndex != startingIndex)
        {
            status = $"Completed {step.title}.";
            yield break;
        }

        yield return DriveGates(step.requiredGates, instant);
        procedureManager.EvaluateCurrentStep();

        if (procedureManager.CurrentStepIndex == startingIndex)
        {
            status = $"Blocked on {FirstMissingGate(step)}.";
            yield break;
        }

        status = $"Completed {step.title}.";
        selectedStepIndex = Mathf.Clamp(
            procedureManager.CurrentStepIndex,
            0,
            Mathf.Max(0, procedureManager.StepCount - 1));
    }

    private IEnumerator DriveGates(
        FurnaceProcedureManager.Gate[] gates,
        bool instant)
    {
        if (gates == null)
            yield break;

        for (int i = 0; i < gates.Length; i++)
        {
            FurnaceProcedureManager.Gate gate = gates[i];
            if (procedureManager.GetGate(gate))
                continue;

            yield return DriveGate(gate, instant);
            if (!procedureManager.GetGate(gate))
                yield break;
        }
    }

    private IEnumerator DriveGate(
        FurnaceProcedureManager.Gate gate,
        bool instant)
    {
        switch (gate)
        {
            case FurnaceProcedureManager.Gate.GrowthParametersSet:
                if (!growthSettings)
                {
                    status = "Growth parameter settings reference is missing.";
                    yield break;
                }
                if (!growthSettings.ConfirmParametersForDevelopment())
                {
                    status = "Growth parameters could not be confirmed.";
                    yield break;
                }
                status = "Growth parameters confirmed.";
                break;

            case FurnaceProcedureManager.Gate.PowerOn:
                if (!powerControl)
                {
                    status = "Power control reference is missing.";
                    yield break;
                }
                powerControl.SetStateForDevelopment(true);
                break;

            case FurnaceProcedureManager.Gate.SubstrateLoaded:
                if (!substrateSnap)
                {
                    status = "Substrate snap reference is missing.";
                    yield break;
                }
                substrateSnap.CompleteForDevelopment(instant);
                break;

            case FurnaceProcedureManager.Gate.RodConnected:
                if (!rodConnector || !substrateConnector)
                {
                    status = "Rod connector references are missing.";
                    yield break;
                }
                rodConnector.ConnectForDevelopment(substrateConnector, instant);
                break;

            case FurnaceProcedureManager.Gate.SubstrateFedIntoTube:
                if (!feedRail)
                {
                    status = "Feed rail reference is missing.";
                    yield break;
                }
                feedRail.CompleteFeedForDevelopment(instant);
                break;

            case FurnaceProcedureManager.Gate.GasFlowReady:
                if (!gasFlow)
                {
                    status = "Gas-flow reference is missing.";
                    yield break;
                }
                gasFlow.SetValueForDevelopment(
                    Mathf.Max(gasFlowSetpoint, procedureManager.MinimumGasFlow));
                break;

            case FurnaceProcedureManager.Gate.TemperatureZonesSet:
                if (!heater)
                {
                    status = "Temperature controller reference is missing.";
                    yield break;
                }
                heater.SetAllSetpointsForDevelopment(temperatureSetpoint);
                break;

            case FurnaceProcedureManager.Gate.HeatSoakComplete:
                if (!heater)
                {
                    status = "Heater reference is missing.";
                    yield break;
                }
                if (instant)
                {
                    if (!heater.CompleteHeatSoakForDevelopment())
                    {
                        status = "Heating safety prerequisites are not satisfied.";
                        yield break;
                    }
                }
                else
                {
                    heater.StartAllZones();
                }
                break;

            case FurnaceProcedureManager.Gate.GrowthStarted:
                if (!growthController)
                {
                    status = "Growth controller reference is missing.";
                    yield break;
                }
                if (!growthController.TryStartGrowth())
                {
                    status = "Growth prerequisites are not satisfied.";
                    yield break;
                }
                status = "Nanowire growth started.";
                break;

            case FurnaceProcedureManager.Gate.GrowthComplete:
                if (!growthController)
                {
                    status = "Growth controller reference is missing.";
                    yield break;
                }
                if (instant && !growthController.CompleteGrowthForDevelopment())
                {
                    status = "Growth could not be completed.";
                    yield break;
                }
                status = instant
                    ? "Nanowire growth completed instantly."
                    : "Waiting for nanowires to reach the target height.";
                break;

            case FurnaceProcedureManager.Gate.CooldownComplete:
                if (!heater)
                {
                    status = "Heater reference is missing.";
                    yield break;
                }
                heater.StartCooldownForDevelopment(instant);
                status = instant
                    ? "Furnace cooled to the safe withdrawal state."
                    : "Waiting for all zones to reach the safe withdrawal temperature.";
                break;

            case FurnaceProcedureManager.Gate.FurnaceOpen:
                if (!lidState)
                {
                    status = "Furnace lid reference is missing.";
                    yield break;
                }
                lidState.SetClosedForDevelopment(false);
                status = "Furnace lid opened.";
                break;

            case FurnaceProcedureManager.Gate.SubstrateWithdrawn:
                if (feedRail)
                {
                    feedRail.CompleteWithdrawalForDevelopment(instant);
                }
                else
                {
                    procedureManager.MarkSubstrateWithdrawn();
                }
                status = instant
                    ? "Substrate withdrawn instantly."
                    : "Withdrawing substrate along the return path.";
                break;

            case FurnaceProcedureManager.Gate.FurnaceClosed:
                if (!lidState)
                {
                    status = "Furnace lid reference is missing.";
                    yield break;
                }
                lidState.SetClosedForDevelopment(true);
                break;
        }

        float timeout = instant ? 2f : GateTimeout(gate);
        float elapsed = 0f;
        while (!procedureManager.GetGate(gate) && elapsed < timeout)
        {
            if (!autoPaused)
                elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!procedureManager.GetGate(gate))
            status = $"Timed out waiting for {gate}.";
        else if (gate == FurnaceProcedureManager.Gate.SubstrateFedIntoTube &&
                 feedRail)
            status = $"Substrate centered ({feedRail.DeliveredBodyEndpointError * 1000f:0.0} mm error).";
    }

    private IEnumerator AutoRunRoutine(bool instant)
    {
        ResetState();
        yield return null;

        status = instant ? "Running instant full flow." : "Running full flow.";
        while (procedureManager && !procedureManager.IsComplete)
        {
            while (autoPaused)
                yield return null;

            int startingIndex = procedureManager.CurrentStepIndex;
            yield return CompleteCurrentStepRoutine(instant);
            if (procedureManager.CurrentStepIndex == startingIndex)
            {
                status = $"Auto-run stopped at {procedureManager.CurrentStepTitle}.";
                yield break;
            }

            if (!instant && stepDelay > 0f)
            {
                float elapsed = 0f;
                while (elapsed < stepDelay)
                {
                    if (!autoPaused)
                        elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        status = "Full procedure completed.";
    }

    private IEnumerator JumpToStepRoutine(int targetStepIndex)
    {
        ResolveReferences();
        if (!procedureManager)
            yield break;

        int target = Mathf.Clamp(targetStepIndex, 0, procedureManager.StepCount);
        ResetState();
        yield return null;

        status = $"Preparing step {target + 1}.";
        while (!procedureManager.IsComplete &&
               procedureManager.CurrentStepIndex < target)
        {
            int startingIndex = procedureManager.CurrentStepIndex;
            yield return CompleteCurrentStepRoutine(true);
            if (procedureManager.CurrentStepIndex == startingIndex)
            {
                status = $"Jump stopped at {procedureManager.CurrentStepTitle}.";
                yield break;
            }

            yield return null;
        }

        status = procedureManager.IsComplete
            ? "Selected step was already satisfied; procedure is complete."
            : $"Ready at {procedureManager.CurrentStepTitle}.";
    }

    private void ResetFromUi()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        autoRunning = false;
        autoPaused = false;
        ResetState();
        status = "Reset complete.";
    }

    private void ResetState()
    {
        ResolveReferences();
        if (stationReset)
        {
            stationReset.ResetStation();
        }
        else
        {
            growthController?.ResetForDevelopment();
            growthSettings?.ResetParameterConfirmationForDevelopment();
            heater?.ResetForDevelopment();
            feedRail?.ResetForDevelopment();
            rodConnector?.ResetForDevelopment();
            substrateSnap?.ResetForDevelopment();
            lidState?.ResetForDevelopment();
            gasFlow?.ResetForDevelopment();
            powerControl?.SetStateForDevelopment(false);
            procedureManager?.ResetProcedure();
            Physics.SyncTransforms();
        }

        selectedStepIndex = 0;
    }

    private void ToggleAutoPause()
    {
        if (!autoRunning)
        {
            status = "Pause is available while auto-run is active.";
            return;
        }

        autoPaused = !autoPaused;
        status = autoPaused ? "Auto-run paused." : "Auto-run resumed.";
    }

    private void AdjustSimulationSpeed(float delta)
    {
        float next = Mathf.Clamp(CurrentSimulationSpeed + delta, 0.5f, 5f);
        if (simulationSpeed)
            simulationSpeed.inspectorMultiplier = next;
        GlobalSimSpeed.Multiplier = next;
        status = $"Simulation speed set to {next:0.0}x.";
    }

    private void TogglePowerFault()
    {
        ResolveReferences();
        if (!powerControl || !procedureManager)
            return;

        bool next = !procedureManager.GetGate(FurnaceProcedureManager.Gate.PowerOn);
        powerControl.SetStateForDevelopment(next);
        status = next ? "Power restored." : "Power fault injected.";
    }

    private void ToggleLidFault()
    {
        ResolveReferences();
        if (!lidState)
            return;

        bool nextClosed = !lidState.IsClosed;
        lidState.SetClosedForDevelopment(nextClosed);
        status = nextClosed ? "Lid closed." : "Open-lid fault injected.";
    }

    private void ToggleGasFault()
    {
        ResolveReferences();
        if (!gasFlow || !procedureManager)
            return;

        bool currentlyReady =
            procedureManager.GetGate(FurnaceProcedureManager.Gate.GasFlowReady);
        gasFlow.SetValueForDevelopment(
            currentlyReady
                ? 0f
                : Mathf.Max(gasFlowSetpoint, procedureManager.MinimumGasFlow));
        status = currentlyReady ? "Gas-flow fault injected." : "Gas flow restored.";
    }

    private string FirstMissingGate(FurnaceProcedureManager.ProcedureStep step)
    {
        FurnaceProcedureManager.Gate? missing = FindMissingGate(step?.prerequisiteGates);
        if (!missing.HasValue)
            missing = FindMissingGate(step?.requiredGates);
        return missing?.ToString() ?? "an unknown condition";
    }

    private FurnaceProcedureManager.Gate? FindMissingGate(
        FurnaceProcedureManager.Gate[] gates)
    {
        if (gates == null)
            return null;

        for (int i = 0; i < gates.Length; i++)
        {
            if (!procedureManager.GetGate(gates[i]))
                return gates[i];
        }

        return null;
    }

    private static float GateTimeout(FurnaceProcedureManager.Gate gate)
    {
        return gate == FurnaceProcedureManager.Gate.HeatSoakComplete ||
               gate == FurnaceProcedureManager.Gate.GrowthComplete ||
               gate == FurnaceProcedureManager.Gate.CooldownComplete
            ? 120f
            : 10f;
    }

    private float CurrentSimulationSpeed =>
        simulationSpeed
            ? simulationSpeed.inspectorMultiplier
            : GlobalSimSpeed.Multiplier;
#endif
}

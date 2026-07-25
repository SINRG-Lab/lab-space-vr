using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class FurnaceProcedureManager : MonoBehaviour
{
    public static FurnaceProcedureManager Instance { get; private set; }

    public enum Gate
    {
        PowerOn,
        SubstrateLoaded,
        RodConnected,
        SubstrateFedIntoTube,
        GasFlowReady,
        TemperatureZonesSet,
        HeatSoakComplete,
        GrowthStarted,
        CooldownComplete,
        SubstrateWithdrawn,
        FurnaceClosed
    }

    [Serializable]
    public class ProcedureStep
    {
        public string id;
        public string title;
        [TextArea(2, 4)] public string instruction;
        [Tooltip("Stable conditions that must remain true before this step's controls are enabled or the step can complete.")]
        public Gate[] prerequisiteGates;
        public Gate[] requiredGates;
        [Tooltip("Objects shown only while this is the current procedure step.")]
        public GameObject[] activeObjects;
        [Tooltip("Components enabled only while this is the current procedure step.")]
        public Behaviour[] activeBehaviours;
        [Tooltip("UI controls made interactable only while this is the current procedure step.")]
        public Selectable[] activeSelectables;
        [Tooltip("Optional scene component highlighted by the world-space step indicator.")]
        public Transform indicatorTarget;
        [Tooltip("World-space offset from the indicator target.")]
        public Vector3 indicatorOffset = new(0f, 0.16f, 0f);
        public UnityEvent onEnter = new UnityEvent();
        public UnityEvent onComplete = new UnityEvent();

        public ProcedureStep()
        {
        }

        public ProcedureStep(string id, string title, string instruction, params Gate[] requiredGates)
        {
            this.id = id;
            this.title = title;
            this.instruction = instruction;
            this.requiredGates = requiredGates;
        }
    }

    [Header("UI")]
    [SerializeField] private TMP_Text stepTitleText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Color activeColor = new(0.2f, 0.78f, 1f, 1f);
    [SerializeField] private Color completeColor = new(0.28f, 0.95f, 0.5f, 1f);
    [SerializeField] private bool showStepNumber = true;

    [Header("Configurable Procedure")]
    [SerializeField] private List<ProcedureStep> steps = new List<ProcedureStep>
    {
        new ProcedureStep(
            "power_on",
            "Power On",
            "Turn on the main furnace power.",
            Gate.PowerOn
        ),
        new ProcedureStep(
            "load_substrate",
            "Load Substrate",
            "Place the substrate on the feed mechanism.",
            Gate.SubstrateLoaded
        ),
        new ProcedureStep(
            "connect_rod",
            "Connect Feed Rod",
            "Attach the feed rod to the loaded substrate holder.",
            Gate.RodConnected
        ),
        new ProcedureStep(
            "feed_substrate",
            "Feed Substrate",
            "Push the connected substrate into the quartz tube.",
            Gate.RodConnected,
            Gate.SubstrateFedIntoTube
        ),
        new ProcedureStep(
            "close_furnace",
            "Close Furnace",
            "Lower the furnace lid fully before continuing.",
            Gate.FurnaceClosed
        ),
        new ProcedureStep(
            "set_gas_flow",
            "Set Gas Flow",
            "Open the gas-flow valve to the required flow.",
            Gate.GasFlowReady
        ),
        new ProcedureStep(
            "set_temperature_zones",
            "Set Temperature Zones",
            "Set the three furnace temperature zones.",
            Gate.TemperatureZonesSet
        ),
        new ProcedureStep(
            "heat_and_soak",
            "Heat and Soak",
            "Start heating and wait until the target soak is complete.",
            Gate.HeatSoakComplete
        )
        {
            prerequisiteGates = new[]
            {
                Gate.FurnaceClosed,
                Gate.GasFlowReady,
                Gate.TemperatureZonesSet
            }
        },
        new ProcedureStep(
            "start_growth",
            "Start Growth",
            "Start the nanowire growth sequence.",
            Gate.GrowthStarted
        ),
        new ProcedureStep(
            "cool_down",
            "Cool Down",
            "Cool the furnace to the safe withdrawal state.",
            Gate.CooldownComplete
        ),
        new ProcedureStep(
            "withdraw_substrate",
            "Withdraw Substrate",
            "Withdraw the substrate from the quartz tube.",
            Gate.SubstrateWithdrawn
        )
    };

    [Header("Gas Flow")]
    [SerializeField, Min(0f)] private float minimumGasFlow = 1000f;
    [SerializeField, Min(0f)] private float gasFlowHysteresis = 100f;

    [Header("Runtime")]
    [SerializeField] private int currentStepIndex;
    [SerializeField] private bool powerOn;
    [SerializeField] private bool substrateLoaded;
    [SerializeField] private bool rodConnected;
    [SerializeField] private bool substrateFedIntoTube;
    [SerializeField] private bool gasFlowReady;
    [SerializeField] private bool temperatureZonesSet;
    [SerializeField] private bool heatSoakComplete;
    [SerializeField] private bool growthStarted;
    [SerializeField] private bool cooldownComplete;
    [SerializeField] private bool substrateWithdrawn;
    [SerializeField] private bool furnaceClosed;

    public int CurrentStepIndex => currentStepIndex;
    public int StepCount => steps != null ? steps.Count : 0;
    public bool IsComplete => currentStepIndex >= StepCount;
    public float MinimumGasFlow => minimumGasFlow;

    public string CurrentStepId => GetCurrentStep()?.id ?? string.Empty;
    public string CurrentStepTitle => IsComplete ? "Procedure Complete" : GetStepTitle(GetCurrentStep());
    public string CurrentStepInstruction => IsComplete ? "Reset the station or prepare for the next run." : GetStepInstruction(GetCurrentStep());
    public Transform CurrentIndicatorTarget => GetCurrentStep()?.indicatorTarget;
    public Vector3 CurrentIndicatorOffset => GetCurrentStep()?.indicatorOffset ?? Vector3.zero;
    public ProcedureStep CurrentStep => GetCurrentStep();

    public event Action<int, ProcedureStep> StepEntered;
    public event Action<int, ProcedureStep> StepCompleted;
    public event Action ProcedureCompleted;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogWarning(
                $"Only one {nameof(FurnaceProcedureManager)} should be active. Disabling {name}.",
                this);
            enabled = false;
            return;
        }

        Instance = this;
        ConfigureProgressSlider();
    }

    private void Start()
    {
        EnsureValidStepIndex();
        RefreshUi();
        InvokeEnterEvent(GetCurrentStep());
        EvaluateCurrentStep();
    }

    public void ResetProcedure()
    {
        powerOn = false;
        substrateLoaded = false;
        rodConnected = false;
        substrateFedIntoTube = false;
        gasFlowReady = false;
        temperatureZonesSet = false;
        heatSoakComplete = false;
        growthStarted = false;
        cooldownComplete = false;
        substrateWithdrawn = false;
        furnaceClosed = false;

        currentStepIndex = 0;
        RefreshUi();
        InvokeEnterEvent(GetCurrentStep());
    }

    public void AdvanceCurrentStep()
    {
        if (IsComplete)
            return;

        CompleteCurrentStep();
        EvaluateCurrentStep();
    }

    public void SetCurrentStepIndex(int stepIndex)
    {
        int clampedIndex = Mathf.Clamp(stepIndex, 0, StepCount);
        if (currentStepIndex == clampedIndex)
        {
            RefreshUi();
            return;
        }

        currentStepIndex = clampedIndex;
        RefreshUi();
        InvokeEnterEvent(GetCurrentStep());
        EvaluateCurrentStep();
    }

    public void EvaluateCurrentStep()
    {
        EnsureValidStepIndex();
        RefreshUi();

        while (!IsComplete && IsStepReady(GetCurrentStep()))
        {
            CompleteCurrentStep();
        }
    }

    public void MarkPowerOn() => SetPowerOn(true);
    public void MarkSubstrateLoaded() => SetSubstrateLoaded(true);
    public void MarkRodConnected() => SetRodConnected(true);
    public void MarkSubstrateFedIntoTube() => SetSubstrateFedIntoTube(true);
    public void MarkGasFlowReady() => SetGasFlowReady(true);
    public void MarkTemperatureZonesSet() => SetTemperatureZonesSet(true);
    public void MarkHeatSoakComplete() => SetHeatSoakComplete(true);
    public void MarkGrowthStarted() => SetGrowthStarted(true);
    public void MarkCooldownComplete() => SetCooldownComplete(true);
    public void MarkSubstrateWithdrawn() => SetSubstrateWithdrawn(true);
    public void MarkFurnaceClosed() => SetFurnaceClosed(true);

    public void SetPowerOn(bool value) => SetGate(Gate.PowerOn, value);
    public void SetSubstrateLoaded(bool value) => SetGate(Gate.SubstrateLoaded, value);
    public void SetRodConnected(bool value) => SetGate(Gate.RodConnected, value);
    public void SetSubstrateFedIntoTube(bool value) => SetGate(Gate.SubstrateFedIntoTube, value);
    public void SetGasFlowReady(bool value) => SetGate(Gate.GasFlowReady, value);
    public void SetTemperatureZonesSet(bool value) => SetGate(Gate.TemperatureZonesSet, value);
    public void SetHeatSoakComplete(bool value) => SetGate(Gate.HeatSoakComplete, value);
    public void SetGrowthStarted(bool value) => SetGate(Gate.GrowthStarted, value);
    public void SetCooldownComplete(bool value) => SetGate(Gate.CooldownComplete, value);
    public void SetSubstrateWithdrawn(bool value) => SetGate(Gate.SubstrateWithdrawn, value);
    public void SetFurnaceClosed(bool value) => SetGate(Gate.FurnaceClosed, value);

    public void SetGasFlowValue(float value)
    {
        float threshold = gasFlowReady
            ? Mathf.Max(0f, minimumGasFlow - gasFlowHysteresis)
            : minimumGasFlow;
        bool isReady = value >= threshold;
        if (isReady != gasFlowReady)
        {
            SetGate(Gate.GasFlowReady, isReady);
        }
    }

    public void SetGate(Gate gate, bool value)
    {
        switch (gate)
        {
            case Gate.PowerOn:
                powerOn = value;
                break;
            case Gate.SubstrateLoaded:
                substrateLoaded = value;
                break;
            case Gate.RodConnected:
                rodConnected = value;
                break;
            case Gate.SubstrateFedIntoTube:
                substrateFedIntoTube = value;
                break;
            case Gate.GasFlowReady:
                gasFlowReady = value;
                break;
            case Gate.TemperatureZonesSet:
                temperatureZonesSet = value;
                break;
            case Gate.HeatSoakComplete:
                heatSoakComplete = value;
                break;
            case Gate.GrowthStarted:
                growthStarted = value;
                break;
            case Gate.CooldownComplete:
                cooldownComplete = value;
                break;
            case Gate.SubstrateWithdrawn:
                substrateWithdrawn = value;
                break;
            case Gate.FurnaceClosed:
                furnaceClosed = value;
                break;
        }

        EvaluateCurrentStep();
    }

    public bool GetGate(Gate gate)
    {
        switch (gate)
        {
            case Gate.PowerOn:
                return powerOn;
            case Gate.SubstrateLoaded:
                return substrateLoaded;
            case Gate.RodConnected:
                return rodConnected;
            case Gate.SubstrateFedIntoTube:
                return substrateFedIntoTube;
            case Gate.GasFlowReady:
                return gasFlowReady;
            case Gate.TemperatureZonesSet:
                return temperatureZonesSet;
            case Gate.HeatSoakComplete:
                return heatSoakComplete;
            case Gate.GrowthStarted:
                return growthStarted;
            case Gate.CooldownComplete:
                return cooldownComplete;
            case Gate.SubstrateWithdrawn:
                return substrateWithdrawn;
            case Gate.FurnaceClosed:
                return furnaceClosed;
            default:
                return false;
        }
    }

    public bool IsGateRequiredByCurrentStep(Gate gate)
    {
        ProcedureStep step = GetCurrentStep();
        if (step?.requiredGates == null)
            return false;

        for (int i = 0; i < step.requiredGates.Length; i++)
        {
            if (step.requiredGates[i] == gate)
                return true;
        }

        return false;
    }

    public ProcedureStep GetStepAt(int stepIndex)
    {
        if (steps == null || stepIndex < 0 || stepIndex >= steps.Count)
            return null;

        return steps[stepIndex];
    }

    public int FindStepIndex(string stepId)
    {
        if (steps == null || string.IsNullOrWhiteSpace(stepId))
            return -1;

        return steps.FindIndex(step =>
            step != null &&
            string.Equals(step.id, stepId, StringComparison.Ordinal));
    }

    private ProcedureStep GetCurrentStep()
    {
        if (steps == null || currentStepIndex < 0 || currentStepIndex >= steps.Count)
            return null;

        return steps[currentStepIndex];
    }

    private bool IsStepReady(ProcedureStep step)
    {
        if (step == null || step.requiredGates == null || step.requiredGates.Length == 0)
            return false;

        return AreGatesSatisfied(step.prerequisiteGates) && AreGatesSatisfied(step.requiredGates);
    }

    private bool AreGatesSatisfied(Gate[] gates)
    {
        if (gates == null)
            return true;

        for (int i = 0; i < gates.Length; i++)
        {
            if (!GetGate(gates[i]))
                return false;
        }

        return true;
    }

    private void CompleteCurrentStep()
    {
        ProcedureStep completedStep = GetCurrentStep();
        int completedStepIndex = currentStepIndex;
        InvokeCompleteEvent(completedStep);
        StepCompleted?.Invoke(completedStepIndex, completedStep);

        currentStepIndex = Mathf.Clamp(currentStepIndex + 1, 0, StepCount);

        RefreshUi();
        if (IsComplete)
        {
            ProcedureCompleted?.Invoke();
            FurnaceInteractionFeedback.PlayProcedureComplete();
        }
        else
        {
            InvokeEnterEvent(GetCurrentStep());
        }
    }

    private void EnsureValidStepIndex()
    {
        currentStepIndex = Mathf.Clamp(currentStepIndex, 0, StepCount);
    }

    private void RefreshUi()
    {
        if (stepTitleText)
        {
            stepTitleText.color = Color.white;
            stepTitleText.text = GetPresentedStepTitle();
        }

        if (instructionText)
        {
            instructionText.color = IsComplete ? completeColor : Color.white;
            instructionText.text = CurrentStepInstruction;
        }

        if (progressSlider)
        {
            progressSlider.value = GetProgress01();
            if (progressSlider.fillRect &&
                progressSlider.fillRect.TryGetComponent(out Image fillImage))
            {
                fillImage.color = IsComplete ? completeColor : activeColor;
            }
        }

        ApplyStepPresentation();
    }

    private void ConfigureProgressSlider()
    {
        if (!progressSlider)
            return;

        progressSlider.minValue = 0f;
        progressSlider.maxValue = 1f;
        progressSlider.wholeNumbers = false;
        progressSlider.interactable = false;
    }

    private string GetPresentedStepTitle()
    {
        string color = ColorUtility.ToHtmlStringRGB(IsComplete ? completeColor : activeColor);
        if (IsComplete)
            return $"<size=16><color=#{color}>PROCEDURE</color></size>\nComplete";

        if (!showStepNumber)
            return CurrentStepTitle;

        return $"<size=16><color=#{color}>STEP {currentStepIndex + 1} OF {StepCount}</color></size>\n{CurrentStepTitle}";
    }

    private void ApplyStepPresentation()
    {
        if (steps == null)
            return;

        ProcedureStep currentStep = GetCurrentStep();
        bool prerequisitesSatisfied = currentStep != null &&
                                      AreGatesSatisfied(currentStep.prerequisiteGates);
        HashSet<GameObject> configuredObjects = new();
        HashSet<Behaviour> configuredBehaviours = new();
        HashSet<Selectable> configuredSelectables = new();

        for (int i = 0; i < steps.Count; i++)
        {
            ProcedureStep step = steps[i];
            if (step == null)
                continue;

            AddConfiguredItems(step.activeObjects, configuredObjects);
            AddConfiguredItems(step.activeBehaviours, configuredBehaviours);
            AddConfiguredItems(step.activeSelectables, configuredSelectables);
        }

        foreach (GameObject configuredObject in configuredObjects)
        {
            bool shouldBeActive = currentStep != null && Contains(currentStep.activeObjects, configuredObject);
            if (configuredObject.activeSelf != shouldBeActive)
                configuredObject.SetActive(shouldBeActive);
        }

        foreach (Behaviour configuredBehaviour in configuredBehaviours)
        {
            bool shouldBeEnabled = powerOn &&
                                   prerequisitesSatisfied &&
                                   currentStep != null &&
                                   Contains(currentStep.activeBehaviours, configuredBehaviour);
            if (configuredBehaviour.enabled != shouldBeEnabled)
                configuredBehaviour.enabled = shouldBeEnabled;
        }

        foreach (Selectable configuredSelectable in configuredSelectables)
        {
            bool shouldBeInteractable = powerOn &&
                                        prerequisitesSatisfied &&
                                        currentStep != null &&
                                        Contains(currentStep.activeSelectables, configuredSelectable);
            if (configuredSelectable.interactable != shouldBeInteractable)
                configuredSelectable.interactable = shouldBeInteractable;
        }
    }

    private static void AddConfiguredItems<T>(T[] items, HashSet<T> destination) where T : UnityEngine.Object
    {
        if (items == null)
            return;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
                destination.Add(items[i]);
        }
    }

    private static bool Contains<T>(T[] items, T item) where T : UnityEngine.Object
    {
        if (items == null || item == null)
            return false;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == item)
                return true;
        }

        return false;
    }

    private float GetProgress01()
    {
        int stepCount = StepCount;
        if (stepCount <= 0)
            return 1f;

        return Mathf.Clamp01((float)currentStepIndex / stepCount);
    }

    private string GetStepTitle(ProcedureStep step)
    {
        if (step == null)
            return "Procedure Complete";

        if (!string.IsNullOrWhiteSpace(step.title))
            return step.title;

        if (!string.IsNullOrWhiteSpace(step.id))
            return step.id;

        return "Procedure Step " + (currentStepIndex + 1);
    }

    private string GetStepInstruction(ProcedureStep step)
    {
        if (step == null)
            return "Reset the station or prepare for the next run.";

        return step.instruction ?? string.Empty;
    }

    private void InvokeEnterEvent(ProcedureStep step)
    {
        if (step != null)
        {
            step.onEnter?.Invoke();
            StepEntered?.Invoke(currentStepIndex, step);
        }
    }

    private void InvokeCompleteEvent(ProcedureStep step)
    {
        if (step != null)
            step.onComplete?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}

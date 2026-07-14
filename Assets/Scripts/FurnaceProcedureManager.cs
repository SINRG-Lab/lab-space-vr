using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class FurnaceProcedureManager : MonoBehaviour
{
    public enum Step
    {
        PowerOn,
        LoadSubstrate,
        FeedSubstrateIntoTube,
        SetGasFlow,
        SetTemperatureZones,
        HeatAndSoak,
        StartGrowth,
        CoolDown,
        WithdrawSubstrate,
        Complete
    }

    [Serializable]
    public class StepEvent
    {
        public Step step;
        public UnityEvent onEnter;
        public UnityEvent onComplete;
    }

    [Header("UI")]
    [SerializeField] private TMP_Text stepTitleText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private Slider progressSlider;

    [Header("Gas Flow")]
    [SerializeField] private float minimumGasFlow = 1f;

    [Header("State")]
    [SerializeField] private Step currentStep = Step.PowerOn;
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

    [Header("Events")]
    [SerializeField] private StepEvent[] stepEvents;

    public Step CurrentStep => currentStep;
    public bool IsComplete => currentStep == Step.Complete;

    private void Start()
    {
        RefreshUi();
        InvokeEnterEvent(currentStep);
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

        SetCurrentStep(Step.PowerOn);
    }

    public void MarkPowerOn() => SetPowerOn(true);
    public void MarkSubstrateLoaded() => SetSubstrateLoaded(true);
    public void MarkRodConnected() => SetRodConnected(true);
    public void MarkSubstrateFedIntoTube() => SetSubstrateFedIntoTube(true);
    public void MarkTemperatureZonesSet() => SetTemperatureZonesSet(true);
    public void MarkHeatSoakComplete() => SetHeatSoakComplete(true);
    public void MarkGrowthStarted() => SetGrowthStarted(true);
    public void MarkCooldownComplete() => SetCooldownComplete(true);
    public void MarkSubstrateWithdrawn() => SetSubstrateWithdrawn(true);

    public void SetPowerOn(bool value)
    {
        powerOn = value;
        EvaluateCurrentStep();
    }

    public void SetSubstrateLoaded(bool value)
    {
        substrateLoaded = value;
        EvaluateCurrentStep();
    }

    public void SetRodConnected(bool value)
    {
        rodConnected = value;
        EvaluateCurrentStep();
    }

    public void SetSubstrateFedIntoTube(bool value)
    {
        substrateFedIntoTube = value;
        EvaluateCurrentStep();
    }

    public void SetGasFlowReady(bool value)
    {
        gasFlowReady = value;
        EvaluateCurrentStep();
    }

    public void SetGasFlowValue(float value)
    {
        gasFlowReady = value >= minimumGasFlow;
        EvaluateCurrentStep();
    }

    public void SetTemperatureZonesSet(bool value)
    {
        temperatureZonesSet = value;
        EvaluateCurrentStep();
    }

    public void SetHeatSoakComplete(bool value)
    {
        heatSoakComplete = value;
        EvaluateCurrentStep();
    }

    public void SetGrowthStarted(bool value)
    {
        growthStarted = value;
        EvaluateCurrentStep();
    }

    public void SetCooldownComplete(bool value)
    {
        cooldownComplete = value;
        EvaluateCurrentStep();
    }

    public void SetSubstrateWithdrawn(bool value)
    {
        substrateWithdrawn = value;
        EvaluateCurrentStep();
    }

    public void SetCurrentStep(Step step)
    {
        if (currentStep == step)
        {
            RefreshUi();
            return;
        }

        currentStep = step;
        RefreshUi();
        InvokeEnterEvent(currentStep);
        EvaluateCurrentStep();
    }

    public void EvaluateCurrentStep()
    {
        RefreshUi();

        if (currentStep == Step.Complete)
            return;

        if (!IsCurrentStepReady())
            return;

        CompleteCurrentStep();
    }

    private void CompleteCurrentStep()
    {
        InvokeCompleteEvent(currentStep);

        Step nextStep = GetNextStep(currentStep);
        currentStep = nextStep;

        RefreshUi();
        InvokeEnterEvent(currentStep);
    }

    private bool IsCurrentStepReady()
    {
        switch (currentStep)
        {
            case Step.PowerOn:
                return powerOn;
            case Step.LoadSubstrate:
                return substrateLoaded;
            case Step.FeedSubstrateIntoTube:
                return rodConnected && substrateFedIntoTube;
            case Step.SetGasFlow:
                return gasFlowReady;
            case Step.SetTemperatureZones:
                return temperatureZonesSet;
            case Step.HeatAndSoak:
                return heatSoakComplete;
            case Step.StartGrowth:
                return growthStarted;
            case Step.CoolDown:
                return cooldownComplete;
            case Step.WithdrawSubstrate:
                return substrateWithdrawn;
            case Step.Complete:
                return true;
            default:
                return false;
        }
    }

    private Step GetNextStep(Step step)
    {
        int next = (int)step + 1;
        int last = (int)Step.Complete;

        if (next > last)
            next = last;

        return (Step)next;
    }

    private void RefreshUi()
    {
        if (stepTitleText)
            stepTitleText.text = GetStepTitle(currentStep);

        if (instructionText)
            instructionText.text = GetStepInstruction(currentStep);

        if (progressSlider)
            progressSlider.value = GetProgress01();
    }

    private float GetProgress01()
    {
        int last = (int)Step.Complete;
        if (last <= 0)
            return 1f;

        return Mathf.Clamp01((float)currentStep / last);
    }

    private string GetStepTitle(Step step)
    {
        switch (step)
        {
            case Step.PowerOn:
                return "Power On";
            case Step.LoadSubstrate:
                return "Load Substrate";
            case Step.FeedSubstrateIntoTube:
                return "Feed Substrate";
            case Step.SetGasFlow:
                return "Set Gas Flow";
            case Step.SetTemperatureZones:
                return "Set Temperature Zones";
            case Step.HeatAndSoak:
                return "Heat and Soak";
            case Step.StartGrowth:
                return "Start Growth";
            case Step.CoolDown:
                return "Cool Down";
            case Step.WithdrawSubstrate:
                return "Withdraw Substrate";
            case Step.Complete:
                return "Procedure Complete";
            default:
                return step.ToString();
        }
    }

    private string GetStepInstruction(Step step)
    {
        switch (step)
        {
            case Step.PowerOn:
                return "Turn on the main furnace power.";
            case Step.LoadSubstrate:
                return "Place the substrate on the feed mechanism.";
            case Step.FeedSubstrateIntoTube:
                return "Connect the feed rod and move the substrate into the quartz tube.";
            case Step.SetGasFlow:
                return "Open the gas-flow valve to the required flow.";
            case Step.SetTemperatureZones:
                return "Set the three furnace temperature zones.";
            case Step.HeatAndSoak:
                return "Start heating and wait until the target soak is complete.";
            case Step.StartGrowth:
                return "Start the nanowire growth sequence.";
            case Step.CoolDown:
                return "Cool the furnace to the safe withdrawal state.";
            case Step.WithdrawSubstrate:
                return "Withdraw the substrate from the quartz tube.";
            case Step.Complete:
                return "Reset the station or prepare for the next run.";
            default:
                return string.Empty;
        }
    }

    private void InvokeEnterEvent(Step step)
    {
        StepEvent match = FindStepEvent(step);
        if (match != null)
            match.onEnter?.Invoke();
    }

    private void InvokeCompleteEvent(Step step)
    {
        StepEvent match = FindStepEvent(step);
        if (match != null)
            match.onComplete?.Invoke();
    }

    private StepEvent FindStepEvent(Step step)
    {
        if (stepEvents == null)
            return null;

        for (int i = 0; i < stepEvents.Length; i++)
        {
            StepEvent candidate = stepEvents[i];
            if (candidate != null && candidate.step == step)
                return candidate;
        }

        return null;
    }
}

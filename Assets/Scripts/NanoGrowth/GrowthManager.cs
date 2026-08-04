using UnityEngine;

public class GrowthManager : MonoBehaviour
{
    public enum GrowthState
    {
        Idle,
        Running,
        Paused,
        Complete
    }

    [Header("Growth")]
    public IncreaseTemperature heater;
    public Setting_Parameter settingParameter;

    [Header("Procedure")]
    [SerializeField] private FurnaceProcedureManager procedureManager;
    [SerializeField] private GrowthState state = GrowthState.Idle;

    public GrowthState State => state;
    public float Progress01 => settingParameter ? settingParameter.GrowthProgress01 : 0f;

    private void Start()
    {
        ResolveReferences();
        settingParameter?.SetGrowthEnabled(false);

        if (procedureManager)
            procedureManager.StepEntered += OnProcedureStepEntered;

        TryStartForCurrentProcedureStep();
    }

    private void OnDestroy()
    {
        if (procedureManager)
            procedureManager.StepEntered -= OnProcedureStepEntered;
    }

    private void Update()
    {
        ResolveReferences();
        if (!settingParameter)
            return;

        if (TryCompleteFromVisualState())
            return;

        if (state != GrowthState.Running && state != GrowthState.Paused)
            return;

        if (!AreGrowthConditionsValid())
        {
            settingParameter.SetGrowthEnabled(false);
            state = GrowthState.Paused;
            return;
        }

        settingParameter.SetGrowthEnabled(true);
        state = GrowthState.Running;

        TryCompleteFromVisualState();
    }

    public void StartGrowth()
    {
        TryStartGrowth();
    }

    public bool TryStartGrowth()
    {
        ResolveReferences();

        if (state == GrowthState.Running || state == GrowthState.Paused)
            return true;

        if (!CanStartGrowth())
        {
            Debug.LogWarning(
                "Growth requires confirmed parameters, the active growth step, a loaded substrate, completed heat soak, power, a closed lid, and safe gas flow.",
                this);
            return false;
        }

        settingParameter.ResetGrowthVisualization();
        procedureManager?.SetGrowthComplete(false);
        state = GrowthState.Running;
        settingParameter.SetGrowthEnabled(true);
        procedureManager?.MarkGrowthStarted();
        FurnaceInteractionFeedback.PlayActionConfirmed();
        return true;
    }

    public bool CompleteGrowthForDevelopment()
    {
        if (!TryStartGrowth())
            return false;

        settingParameter.CompleteGrowthForDevelopment();
        CompleteGrowth();
        return true;
    }

    public void ResetForDevelopment()
    {
        ResolveReferences();
        settingParameter?.ResetGrowthVisualization();
        state = GrowthState.Idle;
        procedureManager?.SetGrowthStarted(false);
        procedureManager?.SetGrowthComplete(false);
    }

    private void CompleteGrowth()
    {
        if (state == GrowthState.Complete)
            return;

        settingParameter.SetGrowthEnabled(false);
        state = GrowthState.Complete;
        procedureManager?.MarkGrowthComplete();
        FurnaceInteractionFeedback.PlayActionConfirmed();
    }

    private bool TryCompleteFromVisualState()
    {
        if (!settingParameter || !settingParameter.AllGrowthComplete)
            return false;

        bool managerStartedGrowth = state == GrowthState.Running ||
                                    state == GrowthState.Paused;

        if (procedureManager)
        {
            bool isGrowthStep = procedureManager.IsGateRequiredByCurrentStep(
                FurnaceProcedureManager.Gate.GrowthComplete);
            bool growthStarted = procedureManager.GetGate(
                FurnaceProcedureManager.Gate.GrowthStarted);

            if (!isGrowthStep ||
                (!managerStartedGrowth &&
                 !growthStarted &&
                 !settingParameter.growth_enabled))
            {
                return false;
            }

            if (!growthStarted)
                procedureManager.MarkGrowthStarted();
        }
        else if (!managerStartedGrowth && !settingParameter.growth_enabled)
        {
            return false;
        }

        CompleteGrowth();
        return true;
    }

    private bool CanStartGrowth()
    {
        if (!settingParameter || !settingParameter.HasGrowthVisuals)
            return false;

        if (procedureManager &&
            !procedureManager.IsGateRequiredByCurrentStep(
                FurnaceProcedureManager.Gate.GrowthStarted))
        {
            return false;
        }

        return AreGrowthConditionsValid();
    }

    private bool AreGrowthConditionsValid()
    {
        if (heater && !heater.AllZonesReached)
            return false;

        if (!procedureManager)
            return true;

        return procedureManager.GetGate(FurnaceProcedureManager.Gate.PowerOn) &&
               procedureManager.GetGate(FurnaceProcedureManager.Gate.GrowthParametersSet) &&
               procedureManager.GetGate(FurnaceProcedureManager.Gate.SubstrateFedIntoTube) &&
               procedureManager.GetGate(FurnaceProcedureManager.Gate.GasFlowReady) &&
               procedureManager.GetGate(FurnaceProcedureManager.Gate.HeatSoakComplete) &&
               procedureManager.GetGate(FurnaceProcedureManager.Gate.FurnaceClosed);
    }

    private void OnProcedureStepEntered(
        int _,
        FurnaceProcedureManager.ProcedureStep __)
    {
        TryStartForCurrentProcedureStep();
    }

    private void TryStartForCurrentProcedureStep()
    {
        if (!procedureManager)
            return;

        bool isAutomaticGrowthStep =
            procedureManager.IsGateRequiredByCurrentStep(
                FurnaceProcedureManager.Gate.GrowthStarted) &&
            procedureManager.IsGateRequiredByCurrentStep(
                FurnaceProcedureManager.Gate.GrowthComplete);

        if (isAutomaticGrowthStep)
            TryStartGrowth();
    }

    private void ResolveReferences()
    {
        if (!procedureManager)
            procedureManager = FurnaceProcedureManager.Instance;
        if (!heater)
            heater = FindFirstObjectByType<IncreaseTemperature>(FindObjectsInactive.Include);
        if (!settingParameter)
            settingParameter = FindFirstObjectByType<Setting_Parameter>(FindObjectsInactive.Include);
    }
}

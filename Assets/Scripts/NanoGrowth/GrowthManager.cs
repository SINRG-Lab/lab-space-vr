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
    }

    private void Update()
    {
        if (state != GrowthState.Running && state != GrowthState.Paused)
            return;

        ResolveReferences();
        if (!settingParameter)
            return;

        if (!AreGrowthConditionsValid())
        {
            settingParameter.SetGrowthEnabled(false);
            state = GrowthState.Paused;
            return;
        }

        settingParameter.SetGrowthEnabled(true);
        state = GrowthState.Running;

        if (settingParameter.AllGrowthComplete)
            CompleteGrowth();
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

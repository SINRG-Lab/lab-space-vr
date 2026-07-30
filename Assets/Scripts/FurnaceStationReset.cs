using UnityEngine;

[DisallowMultipleComponent]
public sealed class FurnaceStationReset : MonoBehaviour
{
    [SerializeField] private FurnaceProcedureManager procedureManager;
    [SerializeField] private AngleTrigger powerControl;
    [SerializeField] private SnapOnRelease substrateSnap;
    [SerializeField] private AutoConnectEnd rodConnector;
    [SerializeField] private FeedRailController feedRail;
    [SerializeField] private FurnaceLidState lidState;
    [SerializeField] private RotationToGasFlow gasFlow;
    [SerializeField] private IncreaseTemperature heater;
    [SerializeField] private Setting_Parameter growthSettings;
    [SerializeField] private GrowthManager growthController;

    private bool resetInProgress;

    public bool IsResetting => resetInProgress;

    private void Awake()
    {
        ResolveReferences();
    }

    public void ResetIfProcedureComplete()
    {
        ResolveReferences();
        if (!resetInProgress && procedureManager && procedureManager.IsComplete)
        {
            ResetStation();
        }
    }

    public void ResetStation()
    {
        if (resetInProgress)
        {
            return;
        }

        resetInProgress = true;
        try
        {
            ResolveReferences();

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
        finally
        {
            resetInProgress = false;
        }

        FurnaceInteractionFeedback.PlayActionConfirmed();
    }

    private void ResolveReferences()
    {
        if (!procedureManager)
        {
            procedureManager = GetComponent<FurnaceProcedureManager>();
        }

        procedureManager = FindIfMissing(procedureManager);
        powerControl = FindIfMissing(powerControl);
        substrateSnap = FindIfMissing(substrateSnap);
        rodConnector = FindIfMissing(rodConnector);
        feedRail = FindIfMissing(feedRail);
        lidState = FindIfMissing(lidState);
        gasFlow = FindIfMissing(gasFlow);
        heater = FindIfMissing(heater);
        growthSettings = FindIfMissing(growthSettings);
        growthController = FindIfMissing(growthController);
    }

    private static T FindIfMissing<T>(T current) where T : Object
    {
        return current
            ? current
            : FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }
}

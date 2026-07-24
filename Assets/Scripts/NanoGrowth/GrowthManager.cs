using UnityEngine;

public class GrowthManager : MonoBehaviour
{
    public IncreaseTemperature heater;
    public Setting_Parameter settingParameter;

    void Update()
    {
        if (!heater || !settingParameter)
            return;

        FurnaceProcedureManager procedure = FurnaceProcedureManager.Instance;
        if (procedure &&
            !procedure.GetGate(FurnaceProcedureManager.Gate.GrowthStarted))
        {
            return;
        }

        if (heater.AllZonesReached)
        {
            settingParameter.growth_enabled = true;
        }
    }
}

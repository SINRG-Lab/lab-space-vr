using UnityEngine;

public class GrowthManager : MonoBehaviour
{
    public IncreaseTemperature heater;
    public Setting_Parameter settingParameter;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(heater.Zone1Reached && heater.Zone2Reached && heater.Zone3Reached){
            settingParameter.growth_enabled = true;
        }
    }
}

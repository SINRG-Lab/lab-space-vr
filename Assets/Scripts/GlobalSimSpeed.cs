using UnityEngine;

public class GlobalSimSpeed : MonoBehaviour
{
    public static float Multiplier = 1f;
    public static float GrowthMultiplier = 10f;

    [Range(0f, 5f)]
    public float inspectorMultiplier = 1f;

    public float inspectorGrowthMultiplier = 1f;

    void Update()
    {
        Multiplier = inspectorMultiplier;
        GrowthMultiplier = inspectorGrowthMultiplier;
    }
}
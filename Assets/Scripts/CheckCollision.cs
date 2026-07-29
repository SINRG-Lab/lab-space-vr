using UnityEngine;

public class CheckCollision : MonoBehaviour
{
    [SerializeField] GrowthRate growthRate;

    void OnTriggerEnter(Collider other)
    {
        if (!growthRate ||
            other.GetComponentInParent<GrowthRate>() == growthRate)
        {
            return;
        }

        if (other.CompareTag("NanoWire") || other.CompareTag("NanoWireTip"))
            growthRate.StopGrowth();
    }
}

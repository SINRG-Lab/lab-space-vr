using UnityEngine;

public class CheckCollision : MonoBehaviour
{
    [SerializeField] GrowthRate growthRate;

    void OnTriggerEnter(Collider other)
    {
        // if (other.transform.root == transform.root) return;

        if (other.CompareTag("NanoWire") || other.CompareTag("NanoWireTip"))
        {
            growthRate.curr_nano_growth_enabled = false;
            Debug.Log("Collided");
        }
    }
}

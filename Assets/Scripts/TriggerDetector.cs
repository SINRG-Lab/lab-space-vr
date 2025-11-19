using UnityEngine;

public class TriggerDetector : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger ENTER: " + other.name);
    }

    private void OnTriggerStay(Collider other)
    {
        Debug.Log("Trigger STAY: " + other.name);
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Trigger EXIT: " + other.name);
    }
}

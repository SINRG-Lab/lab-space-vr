using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SnapRegionDetection : MonoBehaviour
{
    
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Entered by {other.name}");
    }
}

using UnityEngine;

public class ToggleContent : MonoBehaviour
{
    public GameObject target;

    public void Toggle()
    {
        if (!target) return;
        target.SetActive(!target.activeSelf);
    }
    
}

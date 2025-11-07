using UnityEngine;

public class EnableDisableHandle : MonoBehaviour
{
    public GameObject simTwin;
    bool simTwinState = false;

    public void ToggleSimTwin(){
        simTwin.SetActive(!simTwinState);
        simTwinState = !simTwinState;
    }
}

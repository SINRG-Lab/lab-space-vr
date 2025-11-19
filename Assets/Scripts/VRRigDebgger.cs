using UnityEngine;
using TMPro;

public class VRRigDebugger : MonoBehaviour
{
    public TMP_Text data;

    void Update()
    {
        data.text = ("Rig Y: " + transform.position.y);
    }
}
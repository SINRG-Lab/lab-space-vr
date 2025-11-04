using UnityEngine;
using Oculus.Interaction;

public class SnapOnRelease : MonoBehaviour
{
    [Header("Trigger zone to snap within")]
    public Collider targetTrigger; 

    [SerializeField] 
    private Grabbable grabbable;

    [SerializeField]
    private Transform snapTarget;

    public Material highlightMaterial;
    
    Rigidbody rb;
    bool inside;

    Renderer rend;
    Material original;

    void Awake(){
        rb = GetComponent<Rigidbody>();
        if (!grabbable) grabbable = GetComponent<Grabbable>();

        rend = GetComponentInChildren<Renderer>();
        original = rend.material;
    }

    void OnTriggerEnter(Collider other){
        if(other == targetTrigger){
            rend.material = highlightMaterial;
            inside = true;
        }
    }

    void OnTriggerExit(Collider other){
        if (other == targetTrigger){
            rend.material = original;
            inside = false;
        }
    }

    void Update()
    {
        bool isGrabbed = grabbable.SelectingPointsCount > 0;

        if (inside){
            if (!isGrabbed){
                // rb.linearVelocity = Vector3.zero;
                // rb.angularVelocity = Vector3.zero;
                rend.material = original;
                transform.SetPositionAndRotation(snapTarget.position, snapTarget.rotation);
            }
        }
        
    }

}

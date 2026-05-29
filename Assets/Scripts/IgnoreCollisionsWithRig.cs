using UnityEngine;

[DisallowMultipleComponent]
public class IgnoreCollisionsWithRig : MonoBehaviour
{
    [SerializeField] Transform rigRoot;
    [SerializeField] string rigRootName = "[BuildingBlock] Camera Rig";

    void Awake()
    {
        IgnoreRigCollisions();
    }

    void Start()
    {
        IgnoreRigCollisions();
    }

    public void IgnoreRigCollisions()
    {
        if (!rigRoot)
        {
            GameObject rig = GameObject.Find(rigRootName);
            if (rig)
            {
                rigRoot = rig.transform;
            }
        }

        if (!rigRoot)
        {
            return;
        }

        Collider[] objectColliders = GetComponentsInChildren<Collider>(true);
        Collider[] rigColliders = rigRoot.GetComponentsInChildren<Collider>(true);

        foreach (Collider objectCollider in objectColliders)
        {
            foreach (Collider rigCollider in rigColliders)
            {
                if (objectCollider && rigCollider && objectCollider != rigCollider)
                {
                    Physics.IgnoreCollision(objectCollider, rigCollider, true);
                }
            }
        }
    }
}

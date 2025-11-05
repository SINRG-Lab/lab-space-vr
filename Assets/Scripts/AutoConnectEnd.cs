using UnityEngine;

public class AutoConnectEnd : MonoBehaviour
{
    [Header("Owner")]
    public Rigidbody ownerRb;
    public Transform snapPoint;

    [Header("Joint Options")]
    public bool useConfigurableJoint = false;
    public float breakForce = Mathf.Infinity;
    public float breakTorque = Mathf.Infinity;

    FixedJoint fixedJoint;
    ConfigurableJoint configJoint;

    public float maxSeparation = 0.12f;

    void Reset()
    {
        ownerRb = GetComponentInParent<Rigidbody>();
        if (!snapPoint) snapPoint = transform;
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        var otherEnd = other.GetComponent<AutoConnectEnd>();
        if (!otherEnd || otherEnd.ownerRb == ownerRb) return;

        if (fixedJoint || configJoint) return;

        ConnectTo(otherEnd);
    }

    void OnTriggerExit(Collider other)
    {
        // optional: auto-disconnect when end-caps separate
        // var otherEnd = other.GetComponent<AutoConnectEnd>();
        // if (otherEnd) Disconnect();
    }

    void Update()
    {
        if (fixedJoint || configJoint)
        {
            var a = snapPoint.position;
            var b = (fixedJoint ? fixedJoint.connectedBody : configJoint.connectedBody).transform
                    .TransformPoint(configJoint ? configJoint.connectedAnchor : fixedJoint.connectedAnchor);

            if (Vector3.Distance(a, b) > maxSeparation)
                Disconnect();
        }
    }

    public void Disconnect()
    {
        if (fixedJoint) Destroy(fixedJoint);
        if (configJoint) Destroy(configJoint);
        fixedJoint = null;
        configJoint = null;
    }

    void ConnectTo(AutoConnectEnd other)
    {
        // 1) Snap ends together (place our owner so tips coincide)
        // Temporarily freeze to teleport cleanly
        bool wasKinematic = ownerRb.isKinematic;
        ownerRb.isKinematic = true;
        ownerRb.linearVelocity = Vector3.zero;
        ownerRb.angularVelocity = Vector3.zero;

        // Move so our snapPoint lands exactly on the other snapPoint
        Vector3 delta = other.snapPoint.position - snapPoint.position;
        ownerRb.position += delta;

        // (Optional) align orientation to the other tip
        // ownerRb.rotation = Quaternion.FromToRotation(snapPoint.forward, other.snapPoint.forward) * ownerRb.rotation;

        Physics.SyncTransforms();
        ownerRb.isKinematic = wasKinematic;

        // 2) Create the joint on OUR owner, connecting to theirs
        if (!useConfigurableJoint)
        {
            fixedJoint = ownerRb.gameObject.AddComponent<FixedJoint>();
            fixedJoint.connectedBody = other.ownerRb;

            // Anchors at each tip (in local joint space)
            fixedJoint.autoConfigureConnectedAnchor = false;
            fixedJoint.anchor = ownerRb.transform.InverseTransformPoint(snapPoint.position);
            fixedJoint.connectedAnchor = other.ownerRb.transform.InverseTransformPoint(other.snapPoint.position);

            fixedJoint.breakForce = breakForce;
            fixedJoint.breakTorque = breakTorque;
        }
        else
        {
            configJoint = ownerRb.gameObject.AddComponent<ConfigurableJoint>();
            configJoint.connectedBody = other.ownerRb;

            configJoint.autoConfigureConnectedAnchor = false;
            configJoint.anchor = ownerRb.transform.InverseTransformPoint(snapPoint.position);
            configJoint.connectedAnchor = other.ownerRb.transform.InverseTransformPoint(other.snapPoint.position);

            // Lock everything -> acts like FixedJoint, but tunable if you want springs later
            configJoint.xMotion = ConfigurableJointMotion.Locked;
            configJoint.yMotion = ConfigurableJointMotion.Locked;
            configJoint.zMotion = ConfigurableJointMotion.Locked;
            configJoint.angularXMotion = ConfigurableJointMotion.Locked;
            configJoint.angularYMotion = ConfigurableJointMotion.Locked;
            configJoint.angularZMotion = ConfigurableJointMotion.Locked;

            configJoint.breakForce = breakForce;
            configJoint.breakTorque = breakTorque;
        }
    }
}

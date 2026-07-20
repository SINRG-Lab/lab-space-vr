using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

public class AutoConnectEnd : MonoBehaviour
{
    public enum ConnectorRole
    {
        Any,
        Plug,
        Socket
    }

    private static readonly List<AutoConnectEnd> ActiveEnds = new();

    [Header("Owner")]
    public Rigidbody ownerRb;
    public Transform snapPoint;
    [SerializeField] private Grabbable ownerGrabbable;

    [Header("Compatibility")]
    [SerializeField] private string connectionId;
    [SerializeField] private ConnectorRole role = ConnectorRole.Any;
    [SerializeField] private bool canInitiateConnection = true;

    [Header("Guidance")]
    [SerializeField, Min(0f)] private float guidanceRange = 2f;
    [SerializeField, Min(0f)] private float connectionDistance = 0.12f;
    [SerializeField] private bool requireAlignment;
    [SerializeField, Range(0f, 180f)] private float maxAlignmentAngle = 45f;
    [SerializeField] private GameObject connectionGuide;
    [SerializeField] private bool createGuideMarker = true;
    [SerializeField] private Material guideBaseMaterial;
    [SerializeField] private Color guideColor = new(0.15f, 0.75f, 1f, 0.3f);
    [SerializeField] private Color validGuideColor = new(0.2f, 1f, 0.4f, 0.6f);
    [SerializeField, Min(0.001f)] private float guideSize = 0.035f;

    [Header("Procedure")]
    [SerializeField] private FurnaceProcedureManager procedureManager;
    [SerializeField] private FurnaceProcedureManager.Gate procedureGate =
        FurnaceProcedureManager.Gate.RodConnected;
    [SerializeField] private bool restrictInteractionToCurrentStep = true;

    [Header("Snap Motion")]
    [SerializeField, Min(0f)] private float snapDuration = 0.18f;
    [SerializeField] private AnimationCurve snapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool alignRotationOnConnect = true;
    [SerializeField] private bool flipTargetForward;

    [Header("Joint Options")]
    public bool useConfigurableJoint = false;
    public float breakForce = Mathf.Infinity;
    public float breakTorque = Mathf.Infinity;
    public float maxSeparation = 0.12f;

    [Header("Events")]
    public UnityEvent OnValidTargetEntered;
    public UnityEvent OnValidTargetExited;
    public UnityEvent OnConnectionStarted;
    public UnityEvent OnConnected;
    public UnityEvent OnDisconnected;

    private FixedJoint fixedJoint;
    private ConfigurableJoint configurableJoint;
    private AutoConnectEnd currentCandidate;
    private AutoConnectEnd connectedEnd;
    private AutoConnectEnd connectionOwner;
    private AutoConnectEnd connectingTarget;
    private Renderer[] guideRenderers;
    private Material runtimeGuideMaterial;
    private Material runtimeValidGuideMaterial;
    private bool ownsGuide;
    private bool wasGrabbed;
    private bool candidateWasValid;
    private bool isConnecting;
    private bool previousKinematicState;
    private FeedRailController feedRailController;

    public bool IsConnected => connectedEnd;
    public AutoConnectEnd ConnectedEnd => connectedEnd;

    private void Reset()
    {
        ownerRb = GetComponentInParent<Rigidbody>();
        snapPoint = transform;

        Collider trigger = GetComponent<Collider>();
        if (trigger)
        {
            trigger.isTrigger = true;
        }
    }

    private void Awake()
    {
        if (!ownerRb)
        {
            ownerRb = GetComponentInParent<Rigidbody>();
        }

        if (!snapPoint)
        {
            snapPoint = transform;
        }

        if (!ownerGrabbable && ownerRb)
        {
            ownerGrabbable = ownerRb.GetComponent<Grabbable>();
        }

        if (ownerRb)
        {
            feedRailController = ownerRb.GetComponent<FeedRailController>();
        }

        if (!ownerRb || !snapPoint)
        {
            Debug.LogError(
                $"{nameof(AutoConnectEnd)} on {name} requires an owner Rigidbody and snap point.",
                this);
            enabled = false;
            return;
        }

        if (canInitiateConnection && !ownerGrabbable)
        {
            Debug.LogWarning(
                $"{nameof(AutoConnectEnd)} on {name} cannot initiate without a Grabbable on {ownerRb.name}.",
                this);
            canInitiateConnection = false;
        }

        SetupGuide();
        ResolveProcedureManager();
    }

    private void OnEnable()
    {
        if (!ActiveEnds.Contains(this))
        {
            ActiveEnds.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveEnds.Remove(this);
        HideGuide();
        CancelConnectionAnimation();

        if (connectedEnd)
        {
            Disconnect();
        }
    }

    private void Update()
    {
        if (connectedEnd)
        {
            MonitorConnectionSeparation();
            wasGrabbed = IsOwnerGrabbed();
            return;
        }

        if (!IsProcedureAvailable())
        {
            ClearCandidate();
            wasGrabbed = IsOwnerGrabbed();
            return;
        }

        if (!canInitiateConnection || isConnecting)
        {
            return;
        }

        bool isGrabbed = IsOwnerGrabbed();

        if (isGrabbed)
        {
            currentCandidate = FindBestCandidate();
            UpdateGuide(currentCandidate);
        }

        if (wasGrabbed && !isGrabbed)
        {
            AutoConnectEnd releasedCandidate = currentCandidate;
            bool canConnect = releasedCandidate && IsConnectionPoseValid(releasedCandidate);
            ClearCandidate();

            if (canConnect)
            {
                StartCoroutine(ConnectTo(releasedCandidate));
            }
        }
        else if (!isGrabbed)
        {
            ClearCandidate();
        }

        wasGrabbed = isGrabbed;
    }

    private AutoConnectEnd FindBestCandidate()
    {
        AutoConnectEnd bestCandidate = null;
        float bestDistanceSquared = guidanceRange * guidanceRange;

        for (int i = ActiveEnds.Count - 1; i >= 0; i--)
        {
            AutoConnectEnd candidate = ActiveEnds[i];
            if (!candidate)
            {
                ActiveEnds.RemoveAt(i);
                continue;
            }

            if (!IsCompatibleWith(candidate))
            {
                continue;
            }

            float distanceSquared = (snapPoint.position - candidate.snapPoint.position).sqrMagnitude;
            if (distanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestCandidate = candidate;
            }
        }

        return bestCandidate;
    }

    private bool IsCompatibleWith(AutoConnectEnd other)
    {
        if (!other || other == this || !other.isActiveAndEnabled)
        {
            return false;
        }

        if (ownerRb == other.ownerRb || connectedEnd || other.connectedEnd || other.isConnecting)
        {
            return false;
        }

        if (!string.Equals(connectionId, other.connectionId, System.StringComparison.Ordinal))
        {
            return false;
        }

        return role == ConnectorRole.Any ||
               other.role == ConnectorRole.Any ||
               role != other.role;
    }

    private bool IsConnectionPoseValid(AutoConnectEnd other)
    {
        if (!IsCompatibleWith(other))
        {
            return false;
        }

        if (Vector3.Distance(snapPoint.position, other.snapPoint.position) > connectionDistance)
        {
            return false;
        }

        if (!requireAlignment)
        {
            return true;
        }

        Vector3 targetForward = flipTargetForward ? -other.snapPoint.forward : other.snapPoint.forward;
        return Vector3.Angle(snapPoint.forward, targetForward) <= maxAlignmentAngle;
    }

    private IEnumerator ConnectTo(AutoConnectEnd other)
    {
        if (!IsConnectionPoseValid(other))
        {
            yield break;
        }

        isConnecting = true;
        connectingTarget = other;
        other.isConnecting = true;
        previousKinematicState = ownerRb.isKinematic;

        OnConnectionStarted?.Invoke();
        other.OnConnectionStarted?.Invoke();

        Vector3 startPosition = ownerRb.position;
        Quaternion startRotation = ownerRb.rotation;

        ownerRb.isKinematic = true;
        ownerRb.linearVelocity = Vector3.zero;
        ownerRb.angularVelocity = Vector3.zero;

        if (snapDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < snapDuration)
            {
                if (!other || !other.isActiveAndEnabled)
                {
                    CancelConnectionAnimation();
                    yield break;
                }

                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                CalculateTargetPose(other, out Vector3 targetPosition, out Quaternion targetRotation);
                float normalizedTime = Mathf.Clamp01(elapsed / snapDuration);
                float curvedTime = snapCurve != null
                    ? snapCurve.Evaluate(normalizedTime)
                    : normalizedTime;

                ownerRb.MovePosition(Vector3.LerpUnclamped(startPosition, targetPosition, curvedTime));
                ownerRb.MoveRotation(Quaternion.SlerpUnclamped(startRotation, targetRotation, curvedTime));
            }
        }

        if (!other || !other.isActiveAndEnabled)
        {
            CancelConnectionAnimation();
            yield break;
        }

        CalculateTargetPose(other, out Vector3 finalPosition, out Quaternion finalRotation);
        ownerRb.position = finalPosition;
        ownerRb.rotation = finalRotation;
        Physics.SyncTransforms();

        ownerRb.isKinematic = previousKinematicState;
        if (!previousKinematicState)
        {
            ownerRb.WakeUp();
        }

        CreateJoint(other);
        connectedEnd = other;
        other.connectedEnd = this;
        connectionOwner = this;
        other.connectionOwner = this;
        isConnecting = false;
        other.isConnecting = false;
        connectingTarget = null;

        OnConnected?.Invoke();
        other.OnConnected?.Invoke();
        FurnaceInteractionFeedback.PlayActionConfirmed();
    }

    private void CalculateTargetPose(
        AutoConnectEnd other,
        out Vector3 targetPosition,
        out Quaternion targetRotation)
    {
        if (feedRailController &&
            feedRailController.TryCalculateConnectionPose(
                other.snapPoint.position,
                out targetPosition,
                out targetRotation))
        {
            return;
        }

        targetRotation = ownerRb.rotation;

        if (alignRotationOnConnect)
        {
            Quaternion desiredSnapRotation = flipTargetForward
                ? Quaternion.LookRotation(-other.snapPoint.forward, other.snapPoint.up)
                : other.snapPoint.rotation;
            Quaternion rotationDelta = desiredSnapRotation * Quaternion.Inverse(snapPoint.rotation);
            targetRotation = rotationDelta * ownerRb.rotation;

            Vector3 rotatedSnapOffset = rotationDelta * (snapPoint.position - ownerRb.position);
            targetPosition = other.snapPoint.position - rotatedSnapOffset;
            return;
        }

        targetPosition = ownerRb.position + other.snapPoint.position - snapPoint.position;
    }

    private void CreateJoint(AutoConnectEnd other)
    {
        if (!useConfigurableJoint)
        {
            fixedJoint = ownerRb.gameObject.AddComponent<FixedJoint>();
            fixedJoint.connectedBody = other.ownerRb;
            fixedJoint.autoConfigureConnectedAnchor = false;
            fixedJoint.anchor = ownerRb.transform.InverseTransformPoint(snapPoint.position);
            fixedJoint.connectedAnchor = other.ownerRb.transform.InverseTransformPoint(other.snapPoint.position);
            fixedJoint.breakForce = breakForce;
            fixedJoint.breakTorque = breakTorque;
            return;
        }

        configurableJoint = ownerRb.gameObject.AddComponent<ConfigurableJoint>();
        configurableJoint.connectedBody = other.ownerRb;
        configurableJoint.autoConfigureConnectedAnchor = false;
        configurableJoint.anchor = ownerRb.transform.InverseTransformPoint(snapPoint.position);
        configurableJoint.connectedAnchor = other.ownerRb.transform.InverseTransformPoint(other.snapPoint.position);
        configurableJoint.xMotion = ConfigurableJointMotion.Locked;
        configurableJoint.yMotion = ConfigurableJointMotion.Locked;
        configurableJoint.zMotion = ConfigurableJointMotion.Locked;
        configurableJoint.angularXMotion = ConfigurableJointMotion.Locked;
        configurableJoint.angularYMotion = ConfigurableJointMotion.Locked;
        configurableJoint.angularZMotion = ConfigurableJointMotion.Locked;
        configurableJoint.breakForce = breakForce;
        configurableJoint.breakTorque = breakTorque;
    }

    private void MonitorConnectionSeparation()
    {
        if (connectionOwner != this || !connectedEnd || IsOwnerGrabbed())
        {
            return;
        }

        if (Vector3.Distance(snapPoint.position, connectedEnd.snapPoint.position) > maxSeparation)
        {
            DisconnectInternal();
        }
    }

    public void Disconnect()
    {
        if (connectionOwner && connectionOwner != this)
        {
            connectionOwner.DisconnectInternal();
            return;
        }

        DisconnectInternal();
    }

    private void DisconnectInternal()
    {
        if (!connectedEnd && !fixedJoint && !configurableJoint)
        {
            return;
        }

        AutoConnectEnd other = connectedEnd;

        if (fixedJoint)
        {
            Destroy(fixedJoint);
        }

        if (configurableJoint)
        {
            Destroy(configurableJoint);
        }

        fixedJoint = null;
        configurableJoint = null;
        connectedEnd = null;
        connectionOwner = null;

        if (other)
        {
            other.connectedEnd = null;
            other.connectionOwner = null;
            other.OnDisconnected?.Invoke();
        }

        OnDisconnected?.Invoke();
    }

    private void OnJointBreak(float force)
    {
        DisconnectInternal();
    }

    private bool IsOwnerGrabbed()
    {
        return ownerGrabbable && ownerGrabbable.SelectingPointsCount > 0;
    }

    private void UpdateGuide(AutoConnectEnd candidate)
    {
        if (!candidate || !connectionGuide)
        {
            HideGuide();
            return;
        }

        bool isValid = IsConnectionPoseValid(candidate);
        connectionGuide.transform.SetPositionAndRotation(
            candidate.snapPoint.position,
            candidate.snapPoint.rotation);
        connectionGuide.SetActive(true);
        SetGuideMaterial(isValid ? runtimeValidGuideMaterial : runtimeGuideMaterial);

        if (isValid != candidateWasValid)
        {
            candidateWasValid = isValid;
            if (isValid)
            {
                FurnaceInteractionFeedback.PlayTargetAvailable();
                OnValidTargetEntered?.Invoke();
            }
            else
            {
                OnValidTargetExited?.Invoke();
            }
        }
    }

    private void ClearCandidate()
    {
        currentCandidate = null;
        HideGuide();
    }

    private void HideGuide()
    {
        if (connectionGuide)
        {
            connectionGuide.SetActive(false);
        }

        if (candidateWasValid)
        {
            candidateWasValid = false;
            OnValidTargetExited?.Invoke();
        }
    }

    private void SetupGuide()
    {
        if (!connectionGuide && createGuideMarker && canInitiateConnection)
        {
            connectionGuide = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            connectionGuide.name = $"{ownerRb.name} Connection Guide";
            connectionGuide.layer = 2;
            connectionGuide.transform.localScale = Vector3.one * guideSize;
            ownsGuide = true;

            Collider guideCollider = connectionGuide.GetComponent<Collider>();
            if (guideCollider)
            {
                Destroy(guideCollider);
            }
        }

        if (!connectionGuide)
        {
            return;
        }

        guideRenderers = connectionGuide.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer guideRenderer in guideRenderers)
        {
            guideRenderer.shadowCastingMode = ShadowCastingMode.Off;
            guideRenderer.receiveShadows = false;
        }

        Material sourceMaterial = guideBaseMaterial;
        if (!sourceMaterial && guideRenderers.Length > 0)
        {
            sourceMaterial = guideRenderers[0].sharedMaterial;
        }

        if (sourceMaterial)
        {
            runtimeGuideMaterial = CreateTransparentMaterial(sourceMaterial, guideColor);
            runtimeValidGuideMaterial = CreateTransparentMaterial(sourceMaterial, validGuideColor);
        }

        HideGuide();
    }

    private static Material CreateTransparentMaterial(Material source, Color color)
    {
        Material material = new(source)
        {
            name = $"{source.name} (Connection Guide)",
            renderQueue = (int)RenderQueue.Transparent
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    private void SetGuideMaterial(Material material)
    {
        if (!material || guideRenderers == null)
        {
            return;
        }

        foreach (Renderer guideRenderer in guideRenderers)
        {
            Material[] materials = new Material[Mathf.Max(1, guideRenderer.sharedMaterials.Length)];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            guideRenderer.sharedMaterials = materials;
        }
    }

    private void CancelConnectionAnimation()
    {
        StopAllCoroutines();

        if (!isConnecting)
        {
            return;
        }

        if (ownerRb)
        {
            ownerRb.isKinematic = previousKinematicState;
        }

        if (connectingTarget)
        {
            connectingTarget.isConnecting = false;
        }

        connectingTarget = null;
        isConnecting = false;
    }

    private void ResolveProcedureManager()
    {
        if (!procedureManager)
            procedureManager = FindFirstObjectByType<FurnaceProcedureManager>();
    }

    private bool IsProcedureAvailable()
    {
        return !restrictInteractionToCurrentStep ||
               !procedureManager ||
               procedureManager.IsGateRequiredByCurrentStep(procedureGate);
    }

    private void OnDestroy()
    {
        if (ownsGuide && connectionGuide)
        {
            Destroy(connectionGuide);
        }

        if (runtimeGuideMaterial)
        {
            Destroy(runtimeGuideMaterial);
        }

        if (runtimeValidGuideMaterial)
        {
            Destroy(runtimeValidGuideMaterial);
        }
    }
}

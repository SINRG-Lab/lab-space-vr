using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class FeedRailController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody ownerRb;
    [SerializeField] private Grabbable grabbable;
    [SerializeField] private AutoConnectEnd connectionEnd;
    [SerializeField] private Transform railStart;
    [SerializeField] private Transform railEnd;

    [Header("Rail Behavior")]
    [SerializeField] private bool lockRotation = true;
    [SerializeField, Range(0f, 1f)] private float completionThreshold = 0.96f;
    [SerializeField, Range(0f, 1f)] private float resetThreshold = 0.9f;
    [SerializeField] private bool resetCompletionWhenRetracted = true;
    [SerializeField] private bool lockAtCompletion = true;

    [Header("Guidance")]
    [SerializeField] private bool showGuideWhileGrabbed = true;
    [SerializeField] private Material guideBaseMaterial;
    [SerializeField] private Color guideColor = new(0.15f, 0.75f, 1f, 0.35f);
    [SerializeField] private Color completeGuideColor = new(0.2f, 1f, 0.4f, 0.65f);
    [SerializeField, Min(0.001f)] private float guideWidth = 0.008f;
    [SerializeField, Min(0.001f)] private float endMarkerSize = 0.04f;

    [Header("Events")]
    public UnityEvent OnRailActivated;
    public UnityEvent<float> OnProgressChanged;
    public UnityEvent OnFeedCompleted;
    public UnityEvent OnFeedReset;

    private GameObject guideRoot;
    private GameObject endMarker;
    private LineRenderer guideLine;
    private Renderer endMarkerRenderer;
    private Material runtimeGuideMaterial;
    private Material runtimeCompleteMaterial;
    private Vector3 ownerStartPosition;
    private Quaternion ownerStartRotation;
    private bool railActive;
    private bool wasConnected;
    private bool feedCompleted;
    private bool withdrawalUnlocked;
    private float progress;

    public float Progress => progress;
    public bool FeedCompleted => feedCompleted;

    private void Reset()
    {
        ownerRb = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();
        connectionEnd = GetComponentInChildren<AutoConnectEnd>();
    }

    private void Awake()
    {
        if (!ownerRb)
        {
            ownerRb = GetComponent<Rigidbody>();
        }

        if (!grabbable)
        {
            grabbable = GetComponent<Grabbable>();
        }

        if (!connectionEnd)
        {
            connectionEnd = GetComponentInChildren<AutoConnectEnd>();
        }

        if (!ownerRb || !grabbable || !connectionEnd || !railStart || !railEnd)
        {
            Debug.LogError(
                $"{nameof(FeedRailController)} on {name} requires a Rigidbody, Grabbable, connection end, and rail endpoints.",
                this);
            enabled = false;
            return;
        }

        if (!TryGetRail(out _, out _))
        {
            Debug.LogError($"{nameof(FeedRailController)} on {name} has overlapping rail endpoints.", this);
            enabled = false;
            return;
        }

        SetupGuide();
    }

    private void Update()
    {
        bool isConnected = connectionEnd.IsConnected;

        if (isConnected && !wasConnected)
        {
            ActivateRail();
        }
        else if (!isConnected && wasConnected)
        {
            DeactivateRail(true);
        }

        bool isGrabbed = grabbable.SelectingPointsCount > 0;
        SetGuideVisible(railActive && (!showGuideWhileGrabbed || isGrabbed));

        if (guideRoot && guideRoot.activeSelf)
        {
            UpdateGuidePose();
        }

        wasConnected = isConnected;
    }

    private void LateUpdate()
    {
        if (!railActive || !connectionEnd.IsConnected || grabbable.SelectingPointsCount == 0)
        {
            return;
        }

        if (!TryGetRail(out Vector3 railAxis, out float railLength))
        {
            return;
        }

        float distance = Vector3.Dot(ownerRb.position - ownerStartPosition, railAxis);
        distance = Mathf.Clamp(distance, 0f, railLength);
        if (feedCompleted && lockAtCompletion && !withdrawalUnlocked)
        {
            distance = railLength;
        }

        ownerRb.position = ownerStartPosition + railAxis * distance;
        if (lockRotation)
        {
            ownerRb.rotation = ownerStartRotation;
        }

        SetProgress(distance / railLength);
    }

    public void ActivateRail()
    {
        if (!connectionEnd.IsConnected)
        {
            return;
        }

        ownerStartPosition = ownerRb.position;
        ownerStartRotation = ownerRb.rotation;
        railActive = true;
        feedCompleted = false;
        withdrawalUnlocked = false;
        SetProgress(0f, true);
        SetGuideState(false);
        OnRailActivated?.Invoke();
    }

    public void ResetRail()
    {
        bool wasComplete = feedCompleted;
        ownerStartPosition = ownerRb.position;
        ownerStartRotation = ownerRb.rotation;
        feedCompleted = false;
        withdrawalUnlocked = false;
        SetProgress(0f, true);
        SetGuideState(false);

        if (wasComplete)
        {
            OnFeedReset?.Invoke();
        }
    }

    private void DeactivateRail(bool resetCompletion)
    {
        railActive = false;
        SetGuideVisible(false);

        if (resetCompletion && feedCompleted)
        {
            feedCompleted = false;
            OnFeedReset?.Invoke();
        }

        SetProgress(0f, true);
    }

    public void UnlockForWithdrawal()
    {
        withdrawalUnlocked = true;
    }

    private void SetProgress(float value, bool forceEvent = false)
    {
        value = Mathf.Clamp01(value);
        if (!forceEvent && Mathf.Abs(progress - value) < 0.001f)
        {
            return;
        }

        progress = value;
        OnProgressChanged?.Invoke(progress);

        if (!feedCompleted && progress >= completionThreshold)
        {
            feedCompleted = true;
            SetGuideState(true);
            OnFeedCompleted?.Invoke();
        }
        else if (feedCompleted && resetCompletionWhenRetracted && progress < resetThreshold)
        {
            feedCompleted = false;
            SetGuideState(false);
            OnFeedReset?.Invoke();
        }
    }

    private bool TryGetRail(out Vector3 axis, out float length)
    {
        Vector3 railVector = railEnd.position - railStart.position;
        length = railVector.magnitude;
        axis = length > 0.0001f ? railVector / length : Vector3.zero;
        return length > 0.0001f;
    }

    private void SetupGuide()
    {
        guideRoot = new GameObject($"{name} Feed Rail Guide");
        guideRoot.layer = 2;

        guideLine = guideRoot.AddComponent<LineRenderer>();
        guideLine.useWorldSpace = true;
        guideLine.positionCount = 2;
        guideLine.startWidth = guideWidth;
        guideLine.endWidth = guideWidth;
        guideLine.numCapVertices = 4;
        guideLine.shadowCastingMode = ShadowCastingMode.Off;
        guideLine.receiveShadows = false;

        endMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        endMarker.name = "Feed Rail End Marker";
        endMarker.layer = 2;
        endMarker.transform.SetParent(guideRoot.transform, true);
        endMarker.transform.localScale = Vector3.one * endMarkerSize;

        Collider markerCollider = endMarker.GetComponent<Collider>();
        if (markerCollider)
        {
            Destroy(markerCollider);
        }

        endMarkerRenderer = endMarker.GetComponent<Renderer>();
        endMarkerRenderer.shadowCastingMode = ShadowCastingMode.Off;
        endMarkerRenderer.receiveShadows = false;

        Material sourceMaterial = guideBaseMaterial
            ? guideBaseMaterial
            : endMarkerRenderer.sharedMaterial;
        runtimeGuideMaterial = CreateTransparentMaterial(sourceMaterial, guideColor);
        runtimeCompleteMaterial = CreateTransparentMaterial(sourceMaterial, completeGuideColor);

        SetGuideState(false);
        UpdateGuidePose();
        SetGuideVisible(false);
    }

    private void UpdateGuidePose()
    {
        guideLine.SetPosition(0, railStart.position);
        guideLine.SetPosition(1, railEnd.position);
        endMarker.transform.position = railEnd.position;
    }

    private void SetGuideState(bool complete)
    {
        Material material = complete ? runtimeCompleteMaterial : runtimeGuideMaterial;
        if (!material)
        {
            return;
        }

        guideLine.sharedMaterial = material;
        endMarkerRenderer.sharedMaterial = material;
    }

    private void SetGuideVisible(bool visible)
    {
        if (guideRoot && guideRoot.activeSelf != visible)
        {
            guideRoot.SetActive(visible);
        }
    }

    private static Material CreateTransparentMaterial(Material source, Color color)
    {
        Material material = new(source)
        {
            name = $"{source.name} (Feed Rail Guide)",
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

    private void OnDisable()
    {
        railActive = false;
        wasConnected = false;
        SetGuideVisible(false);
    }

    private void OnDestroy()
    {
        if (guideRoot)
        {
            Destroy(guideRoot);
        }

        if (runtimeGuideMaterial)
        {
            Destroy(runtimeGuideMaterial);
        }

        if (runtimeCompleteMaterial)
        {
            Destroy(runtimeCompleteMaterial);
        }
    }
}

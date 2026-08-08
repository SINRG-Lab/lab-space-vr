using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HologramComposer : MonoBehaviour
{
    struct SourceCameraPose
    {
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;

        public SourceCameraPose(Camera camera)
        {
            LocalPosition = camera ? camera.transform.localPosition : Vector3.zero;
            LocalRotation = camera ? camera.transform.localRotation : Quaternion.identity;
        }
    }

    [Header("Source Cameras")]
    public Camera topCamera;
    public Camera leftCamera;
    public Camera bottomCamera;
    public Camera rightCamera;

    [Header("Output Material")]
    public Material hologramMaterial;

    [Header("Lifecycle")]
    [SerializeField] private bool activateOnStart = true;
    [SerializeField] private HologramFinalOutput finalOutput;

    [Header("Render Texture Settings")]
    [Range(256, 4096)] public int textureSize = 1024;
    public int depthBuffer = 24;

    [Header("Capture Camera Settings")]
    [SerializeField] private string captureLayerName = "HologramCapture";
    [SerializeField, Range(20f, 100f)] private float captureFieldOfView = 60f;
    [SerializeField, Min(0.01f)] private float captureNearClip = 0.05f;
    [SerializeField, Min(0.1f)] private float captureFarClip = 10f;
    [SerializeField] private string compositorLayerName = "Holo_UI_Stream";
    [SerializeField, Min(0.1f)] private float referenceContextRadius = 0.65f;
    [SerializeField] private Vector2 contextZoomRange = new(0.75f, 1.5f);
    [SerializeField, Range(0.2f, 1.5f)]
    private float globalDistanceMultiplier = 0.8f;

    [Header("Layout")]
    [Range(0.1f, 0.4f)] public float diamondHalfWidth = 0.22f;
    [Range(0.1f, 0.4f)] public float diamondHalfHeight = 0.22f;
    [Range(0.5f, 1.5f)] public float contentScale = 0.9f;

    [Header("Optional extra flips")]
    public bool flipTopX;
    public bool flipTopY;
    public bool flipLeftX;
    public bool flipLeftY;
    public bool flipBottomX;
    public bool flipBottomY;
    public bool flipRightX;
    public bool flipRightY;

    RenderTexture topRT;
    RenderTexture leftRT;
    RenderTexture bottomRT;
    RenderTexture rightRT;
    Coroutine activationRoutine;
    int captureLayer = -1;
    int activeCaptureMask = ~0;
    float activeContextRadius = 0.65f;
    float activeDistanceMultiplier = 1f;
    float activeFieldOfView;
    float activeCameraYOffset;
    Vector3 activeCameraRotationOffset;
    bool activeCenterCamerasOnFocus;
    bool activeIncludeApparatus = true;
    bool activeUseAuthoredCameraSpacing;
    readonly List<Transform> focusTargets = new();
    readonly List<Transform> captureRoots = new();
    readonly List<GameObject> captureObjects = new();
    readonly List<GameObject> overriddenObjects = new();
    readonly List<int> overriddenLayers = new();
    bool captureLayerOverridesActive;
    Transform cameraRig;
    Vector3 cameraRigInitialLocalPosition;
    Quaternion cameraRigInitialLocalRotation;
    Vector3 referenceFocusLocal;
    SourceCameraPose topCameraPose;
    SourceCameraPose leftCameraPose;
    SourceCameraPose bottomCameraPose;
    SourceCameraPose rightCameraPose;

    public bool IsCaptureActive { get; private set; }

    void Awake()
    {
        captureLayer = LayerMask.NameToLayer(captureLayerName);
        if (captureLayer < 0)
        {
            Debug.LogError(
                $"Missing Unity layer '{captureLayerName}' for hologram cameras.",
                this);
            enabled = false;
            return;
        }

        ConfigureCompositorLayer();
        CacheSourceCameraLayout();
        CacheCaptureRoots();
        activeCaptureMask = GetCaptureMask();
        activeFieldOfView = captureFieldOfView;

        if (!finalOutput)
            finalOutput = GetComponent<HologramFinalOutput>();

        DisableSourceCameras();
        finalOutput?.DeactivateOutput();
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RestoreCaptureLayers();
    }

    void Start()
    {
        if (activateOnStart)
            ActivateCapture();
    }

    public void ActivateCapture()
    {
        if (!enabled || IsCaptureActive || activationRoutine != null)
            return;

        activationRoutine = StartCoroutine(ActivateCaptureRoutine());
    }

    public void ShowContextFocus(
        IReadOnlyList<Transform> targets,
        float contextRadius,
        float distanceMultiplier = 1f,
        bool includeApparatus = true,
        bool useAuthoredCameraSpacing = false,
        float fieldOfViewOverride = 0f,
        float cameraYOffset = 0f,
        Vector3 cameraRotationOffset = default,
        bool centerCamerasOnFocus = false)
    {
        focusTargets.Clear();
        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Transform target = targets[i];
                if (target)
                    focusTargets.Add(target);
            }
        }

        activeCaptureMask = GetCaptureMask();
        activeContextRadius = Mathf.Max(0.1f, contextRadius);
        activeDistanceMultiplier = Mathf.Max(0.1f, distanceMultiplier);
        activeFieldOfView = fieldOfViewOverride > 0f
            ? Mathf.Clamp(fieldOfViewOverride, 1f, 179f)
            : captureFieldOfView;
        activeCameraYOffset = cameraYOffset;
        activeCameraRotationOffset = cameraRotationOffset;
        activeCenterCamerasOnFocus = centerCamerasOnFocus;
        activeIncludeApparatus = includeApparatus;
        activeUseAuthoredCameraSpacing = useAuthoredCameraSpacing;
        RebuildCaptureObjectCache();
        ApplyActiveCaptureMask();
        ApplyActiveFieldOfView();
        RefreshFocusFraming();
    }

    int GetCaptureMask()
    {
        return captureLayer >= 0 ? 1 << captureLayer : 0;
    }

    void ConfigureCompositorLayer()
    {
        int compositorLayer = LayerMask.NameToLayer(compositorLayerName);
        if (compositorLayer < 0)
            return;

        Renderer[] outputRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < outputRenderers.Length; i++)
        {
            Renderer outputRenderer = outputRenderers[i];
            if (!outputRenderer)
                continue;

            outputRenderer.gameObject.layer = compositorLayer;
        }
    }

    public void RefreshFocusFraming()
    {
        if (TryGetFocusBounds(out Bounds focusBounds))
        {
            FrameSourceCameras(focusBounds, activeContextRadius);
            return;
        }

        if (captureLayer >= 0 && TryGetLayerBounds(captureLayer, out Bounds captureBounds))
            FrameSourceCameras(captureBounds, activeContextRadius);
    }

    public void DeactivateCapture()
    {
        if (activationRoutine != null)
        {
            StopCoroutine(activationRoutine);
            activationRoutine = null;
        }

        DisableSourceCameras();
        RestoreSourceCameraLayout();
        finalOutput?.DeactivateOutput();
        ReleaseCaptureTextures();
        ClearMaterialTextures();
        IsCaptureActive = false;
    }

    IEnumerator ActivateCaptureRoutine()
    {
        RebuildCaptureObjectCache();
        SetSourceCameraObjectsActive();

        ConfigureAuxCamera(topCamera, activeCaptureMask);
        ConfigureAuxCamera(leftCamera, activeCaptureMask);
        ConfigureAuxCamera(bottomCamera, activeCaptureMask);
        ConfigureAuxCamera(rightCamera, activeCaptureMask);

        topRT ??= CreateRT("Holo_TopRT");
        leftRT ??= CreateRT("Holo_LeftRT");
        bottomRT ??= CreateRT("Holo_BottomRT");
        rightRT ??= CreateRT("Holo_RightRT");

        if (topCamera) topCamera.targetTexture = topRT;
        if (leftCamera) leftCamera.targetTexture = leftRT;
        if (bottomCamera) bottomCamera.targetTexture = bottomRT;
        if (rightCamera) rightCamera.targetTexture = rightRT;

        ApplyToMaterial();
        finalOutput?.ActivateOutput();
        IsCaptureActive = true;

        // Runtime nanowires and their capture layers are created during Start.
        yield return null;
        RefreshFocusFraming();
        activationRoutine = null;
    }

    void ApplyActiveCaptureMask()
    {
        if (!IsCaptureActive)
            return;

        if (topCamera) topCamera.cullingMask = activeCaptureMask;
        if (leftCamera) leftCamera.cullingMask = activeCaptureMask;
        if (bottomCamera) bottomCamera.cullingMask = activeCaptureMask;
        if (rightCamera) rightCamera.cullingMask = activeCaptureMask;
    }

    void ApplyActiveFieldOfView()
    {
        if (topCamera) topCamera.fieldOfView = activeFieldOfView;
        if (leftCamera) leftCamera.fieldOfView = activeFieldOfView;
        if (bottomCamera) bottomCamera.fieldOfView = activeFieldOfView;
        if (rightCamera) rightCamera.fieldOfView = activeFieldOfView;
    }

    void BeginCameraRendering(
        ScriptableRenderContext context,
        Camera renderingCamera)
    {
        if (!IsSourceCamera(renderingCamera))
            return;

        RestoreCaptureLayers();
        overriddenObjects.Clear();
        overriddenLayers.Clear();

        for (int i = 0; i < captureObjects.Count; i++)
        {
            GameObject captureObject = captureObjects[i];
            if (!captureObject || !captureObject.activeInHierarchy)
                continue;

            overriddenObjects.Add(captureObject);
            overriddenLayers.Add(captureObject.layer);
            captureObject.layer = captureLayer;
        }

        captureLayerOverridesActive = true;
    }

    void EndCameraRendering(
        ScriptableRenderContext context,
        Camera renderingCamera)
    {
        if (IsSourceCamera(renderingCamera))
            RestoreCaptureLayers();
    }

    bool IsSourceCamera(Camera candidate)
    {
        return candidate &&
            (candidate == topCamera ||
             candidate == leftCamera ||
             candidate == bottomCamera ||
             candidate == rightCamera);
    }

    void RestoreCaptureLayers()
    {
        if (!captureLayerOverridesActive)
            return;

        int restoreCount = Mathf.Min(
            overriddenObjects.Count,
            overriddenLayers.Count);
        for (int i = restoreCount - 1; i >= 0; i--)
        {
            GameObject captureObject = overriddenObjects[i];
            if (captureObject)
                captureObject.layer = overriddenLayers[i];
        }

        overriddenObjects.Clear();
        overriddenLayers.Clear();
        captureLayerOverridesActive = false;
    }

    void CacheCaptureRoots()
    {
        captureRoots.Clear();

        Transform furnaceRoot = cameraRig ? cameraRig.parent : null;
        AddCaptureRoot(furnaceRoot);

        Transform stationRoot = furnaceRoot ? furnaceRoot.parent : null;
        AddCaptureRoot(FindDirectChild(stationRoot, "Pulling Rod Parent"));
        AddCaptureRoot(FindDirectChild(stationRoot, "Plate"));

        FurnaceStepIndicator indicator = FindFirstObjectByType<FurnaceStepIndicator>(
            FindObjectsInactive.Include);
        if (indicator)
            AddCaptureRoot(indicator.transform);

        RebuildCaptureObjectCache();
    }

    void AddCaptureRoot(Transform captureRoot)
    {
        if (captureRoot && !captureRoots.Contains(captureRoot))
            captureRoots.Add(captureRoot);
    }

    static Transform FindDirectChild(Transform parent, string childName)
    {
        if (!parent)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;
        }

        return null;
    }

    void RebuildCaptureObjectCache()
    {
        captureObjects.Clear();
        HashSet<GameObject> seenObjects = new();

        if (activeIncludeApparatus)
        {
            for (int i = 0; i < captureRoots.Count; i++)
                AddCaptureHierarchy(captureRoots[i], seenObjects);
        }

        for (int i = 0; i < focusTargets.Count; i++)
            AddCaptureHierarchy(focusTargets[i], seenObjects);
    }

    void AddCaptureHierarchy(
        Transform captureRoot,
        HashSet<GameObject> seenObjects)
    {
        if (!captureRoot)
            return;

        Transform[] hierarchy = captureRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < hierarchy.Length; i++)
        {
            GameObject captureObject = hierarchy[i].gameObject;
            if (seenObjects.Add(captureObject))
                captureObjects.Add(captureObject);
        }
    }

    void SetSourceCameraObjectsActive()
    {
        Camera[] sourceCameras = { topCamera, leftCamera, bottomCamera, rightCamera };
        foreach (Camera sourceCamera in sourceCameras)
        {
            if (!sourceCamera)
                continue;

            Transform sourceParent = sourceCamera.transform.parent;
            if (sourceParent && !sourceParent.gameObject.activeSelf)
                sourceParent.gameObject.SetActive(true);

            if (!sourceCamera.gameObject.activeSelf)
                sourceCamera.gameObject.SetActive(true);
        }
    }

    void ConfigureAuxCamera(Camera camera, int captureMask)
    {
        if (!camera) return;

        camera.enabled = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = captureMask;
        camera.orthographic = false;
        camera.fieldOfView = activeFieldOfView;
        camera.nearClipPlane = captureNearClip;
        camera.farClipPlane = Mathf.Max(captureNearClip + 0.01f, captureFarClip);
        camera.useOcclusionCulling = false;
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        camera.allowHDR = false;
        camera.allowMSAA = false;

        if (camera.TryGetComponent<AudioListener>(out var listener))
            listener.enabled = false;

        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.allowXRRendering = false;
        cameraData.renderShadows = false;
        cameraData.requiresColorTexture = false;
        cameraData.requiresDepthTexture = false;
        cameraData.renderPostProcessing = false;
    }

    void DisableSourceCameras()
    {
        Camera[] sourceCameras = { topCamera, leftCamera, bottomCamera, rightCamera };
        Transform sourceParent = null;
        foreach (Camera sourceCamera in sourceCameras)
        {
            if (!sourceCamera)
                continue;

            sourceCamera.enabled = false;
            sourceCamera.targetTexture = null;
            sourceParent ??= sourceCamera.transform.parent;
        }

        if (sourceParent)
            sourceParent.gameObject.SetActive(false);
    }

    bool TryGetFocusBounds(out Bounds focusBounds)
    {
        focusBounds = default;
        bool hasBounds = false;
        for (int i = 0; i < focusTargets.Count; i++)
        {
            Transform focusTarget = focusTargets[i];
            if (!focusTarget || !focusTarget.gameObject.activeInHierarchy)
                continue;

            EncapsulatePoint(ref focusBounds, ref hasBounds, focusTarget.position);
            Renderer[] targetRenderers =
                focusTarget.GetComponentsInChildren<Renderer>(false);
            for (int rendererIndex = 0;
                 rendererIndex < targetRenderers.Length;
                 rendererIndex++)
            {
                Renderer targetRenderer = targetRenderers[rendererIndex];
                if (!targetRenderer || !targetRenderer.enabled)
                    continue;

                EncapsulateBounds(
                    ref focusBounds,
                    ref hasBounds,
                    targetRenderer.bounds);
            }
        }

        return hasBounds;
    }

    bool TryGetLayerBounds(int subjectLayer, out Bounds layerBounds)
    {
        layerBounds = default;
        bool hasBounds = false;
        Renderer[] renderers = FindObjectsByType<Renderer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer layerRenderer = renderers[i];
            if (!layerRenderer || !layerRenderer.enabled ||
                layerRenderer.gameObject.layer != subjectLayer)
                continue;

            EncapsulateBounds(
                ref layerBounds,
                ref hasBounds,
                layerRenderer.bounds);
        }

        return hasBounds;
    }

    static void EncapsulatePoint(
        ref Bounds destination,
        ref bool hasBounds,
        Vector3 point)
    {
        EncapsulateBounds(
            ref destination,
            ref hasBounds,
            new Bounds(point, Vector3.zero));
    }

    static void EncapsulateBounds(
        ref Bounds destination,
        ref bool hasBounds,
        Bounds source)
    {
        if (!hasBounds)
        {
            destination = source;
            hasBounds = true;
            return;
        }

        destination.Encapsulate(source);
    }

    void FrameSourceCameras(Bounds focusBounds, float contextRadius)
    {
        if (!cameraRig)
            return;

        float requiredRadius = Mathf.Max(
            focusBounds.extents.magnitude,
            Mathf.Max(0.1f, contextRadius));
        float zoomScale = activeUseAuthoredCameraSpacing
            ? 1f
            : Mathf.Clamp(
                requiredRadius / Mathf.Max(0.1f, referenceContextRadius),
                Mathf.Min(contextZoomRange.x, contextZoomRange.y),
                Mathf.Max(contextZoomRange.x, contextZoomRange.y)) *
              globalDistanceMultiplier * activeDistanceMultiplier;

        RestoreCameraPose(topCamera, topCameraPose, zoomScale);
        RestoreCameraPose(leftCamera, leftCameraPose, zoomScale);
        RestoreCameraPose(bottomCamera, bottomCameraPose, zoomScale);
        RestoreCameraPose(rightCamera, rightCameraPose, zoomScale);

        cameraRig.localRotation =
            cameraRigInitialLocalRotation *
            Quaternion.Euler(activeCameraRotationOffset);
        Vector3 referenceFocusOffset =
            cameraRig.TransformVector(referenceFocusLocal);
        cameraRig.position =
            focusBounds.center - referenceFocusOffset +
            Vector3.up * activeCameraYOffset;

        if (activeCenterCamerasOnFocus)
        {
            CenterCameraOnFocus(topCamera, focusBounds.center);
            CenterCameraOnFocus(leftCamera, focusBounds.center);
            CenterCameraOnFocus(bottomCamera, focusBounds.center);
            CenterCameraOnFocus(rightCamera, focusBounds.center);
        }
    }

    static void CenterCameraOnFocus(Camera sourceCamera, Vector3 focusPoint)
    {
        if (!sourceCamera)
            return;

        Vector3 focusDirection = focusPoint - sourceCamera.transform.position;
        if (focusDirection.sqrMagnitude < 0.000001f)
            return;

        Quaternion correction = Quaternion.FromToRotation(
            sourceCamera.transform.forward,
            focusDirection.normalized);
        sourceCamera.transform.rotation =
            correction * sourceCamera.transform.rotation;
    }

    void CacheSourceCameraLayout()
    {
        cameraRig = topCamera ? topCamera.transform.parent : null;
        if (!cameraRig)
            return;

        cameraRigInitialLocalPosition = cameraRig.localPosition;
        cameraRigInitialLocalRotation = cameraRig.localRotation;
        topCameraPose = new SourceCameraPose(topCamera);
        leftCameraPose = new SourceCameraPose(leftCamera);
        bottomCameraPose = new SourceCameraPose(bottomCamera);
        rightCameraPose = new SourceCameraPose(rightCamera);
        referenceFocusLocal = CalculateReferenceFocusLocal();
    }

    Vector3 CalculateReferenceFocusLocal()
    {
        Camera[] sourceCameras = { topCamera, leftCamera, bottomCamera, rightCamera };
        Vector3 focusSum = Vector3.zero;
        int validCameraCount = 0;
        for (int i = 0; i < sourceCameras.Length; i++)
        {
            Camera sourceCamera = sourceCameras[i];
            if (!sourceCamera)
                continue;

            Vector3 localPosition = sourceCamera.transform.localPosition;
            Vector3 localForward = cameraRig.InverseTransformDirection(
                sourceCamera.transform.forward).normalized;
            float planarLengthSquared =
                localForward.x * localForward.x +
                localForward.z * localForward.z;
            if (planarLengthSquared < 0.000001f)
                continue;

            float distanceToCenter = -(
                localPosition.x * localForward.x +
                localPosition.z * localForward.z) / planarLengthSquared;
            focusSum += localPosition + localForward * distanceToCenter;
            validCameraCount++;
        }

        return validCameraCount > 0
            ? focusSum / validCameraCount
            : Vector3.zero;
    }

    void RestoreCameraPose(
        Camera sourceCamera,
        SourceCameraPose sourcePose,
        float zoomScale)
    {
        if (!sourceCamera)
            return;

        Vector3 focusOffset = sourcePose.LocalPosition - referenceFocusLocal;
        sourceCamera.transform.localPosition =
            referenceFocusLocal + focusOffset * zoomScale;
        sourceCamera.transform.localRotation = sourcePose.LocalRotation;
    }

    void RestoreSourceCameraLayout()
    {
        if (!cameraRig)
            return;

        cameraRig.localPosition = cameraRigInitialLocalPosition;
        cameraRig.localRotation = cameraRigInitialLocalRotation;
        RestoreCameraPose(topCamera, topCameraPose, 1f);
        RestoreCameraPose(leftCamera, leftCameraPose, 1f);
        RestoreCameraPose(bottomCamera, bottomCameraPose, 1f);
        RestoreCameraPose(rightCamera, rightCameraPose, 1f);
    }

    void OnValidate()
    {
        captureFarClip = Mathf.Max(captureNearClip + 0.01f, captureFarClip);
        ApplyToMaterial();
    }

    void ApplyToMaterial()
    {
        if (hologramMaterial == null) return;

        if (topRT) hologramMaterial.SetTexture("_TopTex", topRT);
        if (leftRT) hologramMaterial.SetTexture("_LeftTex", leftRT);
        if (bottomRT) hologramMaterial.SetTexture("_BottomTex", bottomRT);
        if (rightRT) hologramMaterial.SetTexture("_RightTex", rightRT);

        hologramMaterial.SetFloat("_DiamondHalfWidth", diamondHalfWidth);
        hologramMaterial.SetFloat("_DiamondHalfHeight", diamondHalfHeight);
        hologramMaterial.SetFloat("_ContentScale", contentScale);

        hologramMaterial.SetFloat("_FlipTopX", flipTopX ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipTopY", flipTopY ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipLeftX", flipLeftX ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipLeftY", flipLeftY ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipBottomX", flipBottomX ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipBottomY", flipBottomY ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipRightX", flipRightX ? 1f : 0f);
        hologramMaterial.SetFloat("_FlipRightY", flipRightY ? 1f : 0f);
    }

    RenderTexture CreateRT(string rtName)
    {
        RenderTexture rt = new RenderTexture(textureSize, textureSize, depthBuffer, RenderTextureFormat.ARGB32);
        rt.name = rtName;
        rt.Create();
        return rt;
    }

    void OnDestroy()
    {
        ReleaseCaptureTextures();
    }

    void ReleaseCaptureTextures()
    {
        ReleaseRT(ref topRT);
        ReleaseRT(ref leftRT);
        ReleaseRT(ref bottomRT);
        ReleaseRT(ref rightRT);
    }

    void ClearMaterialTextures()
    {
        if (!hologramMaterial)
            return;

        hologramMaterial.SetTexture("_TopTex", null);
        hologramMaterial.SetTexture("_LeftTex", null);
        hologramMaterial.SetTexture("_BottomTex", null);
        hologramMaterial.SetTexture("_RightTex", null);
    }

    void ReleaseRT(ref RenderTexture rt)
    {
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }
}

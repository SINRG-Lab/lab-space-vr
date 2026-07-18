using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

public class SnapOnRelease : MonoBehaviour
{
    [Header("Snap Target")]
    public Collider targetTrigger;

    [SerializeField] private Grabbable grabbable;
    [SerializeField] private Transform snapTarget;
    [SerializeField] private Material highlightMaterial;

    [Header("Placement Guide")]
    [Tooltip("Optional custom preview. When empty, a visual-only copy is created at runtime.")]
    [SerializeField] private GameObject snapGuide;
    [SerializeField] private bool createGuideFromObject = true;
    [SerializeField] private Material guideMaterial;
    [SerializeField] private Material validGuideMaterial;
    [SerializeField] private Color guideColor = new(0.15f, 0.75f, 1f, 0.3f);
    [SerializeField] private Color validGuideColor = new(0.2f, 1f, 0.4f, 0.55f);

    [Header("Snap Motion")]
    [SerializeField, Min(0f)] private float snapDuration = 0.2f;
    [SerializeField] private AnimationCurve snapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Post-Snap Constraints")]
    [SerializeField] private bool applyConstraintsAfterSnap = true;
    [SerializeField, Min(0f)] private float constraintDelay = 2f;
    [SerializeField] private RigidbodyConstraints releasedConstraints =
        RigidbodyConstraints.FreezePositionY |
        RigidbodyConstraints.FreezePositionZ |
        RigidbodyConstraints.FreezeRotation;

    [Header("Events")]
    public UnityEvent OnSnapZoneEntered;
    public UnityEvent OnSnapZoneExited;
    public UnityEvent OnSnapStarted;
    public UnityEvent OnSnapped;

    private readonly List<RendererState> objectRendererStates = new();

    private Rigidbody rb;
    private Renderer[] guideRenderers;
    private Material runtimeGuideMaterial;
    private Material runtimeValidGuideMaterial;
    private bool ownsGuide;
    private bool inside;
    private bool wasGrabbed;
    private bool pendingSnap;
    private bool isSnapping;
    private bool previousKinematicState;
    private Vector3 queuedPosition;
    private Quaternion queuedRotation;

    private sealed class RendererState
    {
        public Renderer Renderer { get; }
        public Material[] OriginalMaterials { get; }

        public RendererState(Renderer renderer)
        {
            Renderer = renderer;
            OriginalMaterials = renderer.sharedMaterials;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!grabbable)
        {
            grabbable = GetComponent<Grabbable>();
        }

        if (!rb || !grabbable || !targetTrigger || !snapTarget)
        {
            Debug.LogError(
                $"{nameof(SnapOnRelease)} on {name} requires a Rigidbody, Grabbable, target trigger, and snap target.",
                this);
            enabled = false;
            return;
        }

        CacheObjectRenderers();
        SetupGuide();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other != targetTrigger || isSnapping)
        {
            return;
        }

        inside = true;
        SetObjectHighlight(true);
        SetGuideState(true);
        OnSnapZoneEntered?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != targetTrigger || isSnapping)
        {
            return;
        }

        inside = false;
        SetObjectHighlight(false);
        SetGuideState(false);
        OnSnapZoneExited?.Invoke();
    }

    private void Update()
    {
        bool isGrabbed = grabbable.SelectingPointsCount > 0;

        if (isGrabbed && !wasGrabbed)
        {
            SetGuideVisible(true);
            SetGuideState(inside);
        }

        if (inside && wasGrabbed && !isGrabbed && !isSnapping)
        {
            queuedPosition = snapTarget.position;
            queuedRotation = snapTarget.rotation;
            pendingSnap = true;
            SetObjectHighlight(false);
            SetGuideVisible(false);
        }
        else if (wasGrabbed && !isGrabbed)
        {
            SetGuideVisible(false);
        }

        wasGrabbed = isGrabbed;
    }

    private void FixedUpdate()
    {
        if (!pendingSnap)
        {
            return;
        }

        pendingSnap = false;
        StartCoroutine(SnapToTarget(queuedPosition, queuedRotation));
    }

    private IEnumerator SnapToTarget(Vector3 targetPosition, Quaternion targetRotation)
    {
        isSnapping = true;
        OnSnapStarted?.Invoke();

        previousKinematicState = rb.isKinematic;
        Vector3 startPosition = rb.position;
        Quaternion startRotation = rb.rotation;

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (snapDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < snapDuration)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / snapDuration);
                float curvedTime = snapCurve.Evaluate(normalizedTime);

                rb.MovePosition(Vector3.LerpUnclamped(startPosition, targetPosition, curvedTime));
                rb.MoveRotation(Quaternion.SlerpUnclamped(startRotation, targetRotation, curvedTime));
            }
        }

        rb.position = targetPosition;
        rb.rotation = targetRotation;
        Physics.SyncTransforms();

        rb.isKinematic = previousKinematicState;
        if (!previousKinematicState)
        {
            rb.WakeUp();
        }

        isSnapping = false;
        OnSnapped?.Invoke();

        if (applyConstraintsAfterSnap)
        {
            StartCoroutine(ApplyConstraintsDelayed());
        }
    }

    private IEnumerator ApplyConstraintsDelayed()
    {
        if (constraintDelay > 0f)
        {
            yield return new WaitForSeconds(constraintDelay);
        }

        bool isGrabbed = grabbable.SelectingPointsCount > 0;
        rb.constraints = isGrabbed ? RigidbodyConstraints.None : releasedConstraints;
    }

    private void CacheObjectRenderers()
    {
        foreach (Renderer objectRenderer in GetComponentsInChildren<Renderer>(true))
        {
            objectRendererStates.Add(new RendererState(objectRenderer));
        }
    }

    private void SetupGuide()
    {
        if (!snapGuide && createGuideFromObject)
        {
            snapGuide = CreateGuideFromObject();
            ownsGuide = snapGuide != null;
        }

        if (!snapGuide)
        {
            return;
        }

        snapGuide.transform.SetPositionAndRotation(snapTarget.position, snapTarget.rotation);
        guideRenderers = snapGuide.GetComponentsInChildren<Renderer>(true);
        CreateRuntimeGuideMaterials();
        SetGuideState(false);
        SetGuideVisible(false);
    }

    private GameObject CreateGuideFromObject()
    {
        GameObject guideRoot = new($"{name} Snap Guide");
        guideRoot.layer = gameObject.layer;
        guideRoot.transform.SetPositionAndRotation(snapTarget.position, snapTarget.rotation);
        guideRoot.transform.localScale = transform.lossyScale;

        CopyVisualHierarchy(transform, guideRoot.transform, true);

        if (guideRoot.GetComponentInChildren<Renderer>(true))
        {
            return guideRoot;
        }

        Destroy(guideRoot);
        return null;
    }

    private void CopyVisualHierarchy(Transform source, Transform destination, bool isRoot)
    {
        if (!isRoot)
        {
            destination.localPosition = source.localPosition;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
        }

        MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
        MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
        if (sourceRenderer && sourceRenderer.enabled && sourceFilter && sourceFilter.sharedMesh)
        {
            MeshFilter guideFilter = destination.gameObject.AddComponent<MeshFilter>();
            guideFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer guideRenderer = destination.gameObject.AddComponent<MeshRenderer>();
            guideRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            guideRenderer.shadowCastingMode = ShadowCastingMode.Off;
            guideRenderer.receiveShadows = false;
            guideRenderer.lightProbeUsage = LightProbeUsage.Off;
            guideRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        foreach (Transform sourceChild in source)
        {
            if (!sourceChild.gameObject.activeSelf)
            {
                continue;
            }

            GameObject guideChild = new(sourceChild.name);
            guideChild.layer = sourceChild.gameObject.layer;
            guideChild.transform.SetParent(destination, false);
            CopyVisualHierarchy(sourceChild, guideChild.transform, false);
        }
    }

    private void CreateRuntimeGuideMaterials()
    {
        Material fallbackMaterial = highlightMaterial;
        if (!fallbackMaterial && guideRenderers.Length > 0 && guideRenderers[0].sharedMaterial)
        {
            fallbackMaterial = guideRenderers[0].sharedMaterial;
        }

        if (!guideMaterial && fallbackMaterial)
        {
            runtimeGuideMaterial = CreateTransparentMaterial(fallbackMaterial, guideColor);
        }

        if (!validGuideMaterial && fallbackMaterial)
        {
            runtimeValidGuideMaterial = CreateTransparentMaterial(fallbackMaterial, validGuideColor);
        }
    }

    private static Material CreateTransparentMaterial(Material source, Color color)
    {
        Material material = new(source)
        {
            name = $"{source.name} (Snap Guide)",
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

    private void SetObjectHighlight(bool highlighted)
    {
        foreach (RendererState state in objectRendererStates)
        {
            if (!state.Renderer)
            {
                continue;
            }

            if (highlighted && highlightMaterial && state.Renderer.enabled)
            {
                state.Renderer.sharedMaterials = RepeatMaterial(highlightMaterial, state.OriginalMaterials.Length);
            }
            else
            {
                state.Renderer.sharedMaterials = state.OriginalMaterials;
            }
        }
    }

    private void SetGuideState(bool valid)
    {
        if (guideRenderers == null)
        {
            return;
        }

        Material stateMaterial = valid
            ? validGuideMaterial ? validGuideMaterial : runtimeValidGuideMaterial
            : guideMaterial ? guideMaterial : runtimeGuideMaterial;

        if (!stateMaterial)
        {
            return;
        }

        foreach (Renderer guideRenderer in guideRenderers)
        {
            guideRenderer.sharedMaterials = RepeatMaterial(
                stateMaterial,
                Mathf.Max(1, guideRenderer.sharedMaterials.Length));
        }
    }

    private void SetGuideVisible(bool visible)
    {
        if (!snapGuide)
        {
            return;
        }

        if (visible)
        {
            snapGuide.transform.SetPositionAndRotation(snapTarget.position, snapTarget.rotation);
        }

        snapGuide.SetActive(visible);
    }

    private static Material[] RepeatMaterial(Material material, int count)
    {
        Material[] materials = new Material[Mathf.Max(1, count)];
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = material;
        }

        return materials;
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (isSnapping && rb)
        {
            rb.isKinematic = previousKinematicState;
        }

        pendingSnap = false;
        isSnapping = false;
        SetObjectHighlight(false);
        SetGuideVisible(false);
    }

    private void OnDestroy()
    {
        if (ownsGuide && snapGuide)
        {
            Destroy(snapGuide);
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

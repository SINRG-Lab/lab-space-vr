using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class FurnaceStepIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FurnaceProcedureManager procedureManager;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Renderer indicatorRenderer;

    [Header("Appearance")]
    [SerializeField] private Color indicatorColor = new(0.2f, 0.78f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float visualScale = 1f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float followSpeed = 12f;
    [SerializeField, Min(0f)] private float rotationSpeed = 16f;
    [SerializeField, Min(0f)] private float pulseHeight = 0.012f;
    [SerializeField, Min(0f)] private float pulseScale = 0.06f;
    [SerializeField, Min(0f)] private float pulseFrequency = 1.4f;

    private Transform currentTarget;
    private Vector3 currentOffset;
    private Material runtimeMaterial;
    private Mesh runtimeMesh;
    private bool isVisible;

    private void Awake()
    {
        if (!procedureManager)
            procedureManager = FindFirstObjectByType<FurnaceProcedureManager>();

        if (!visualRoot)
            CreateRuntimeVisual();

        SetVisible(false);
    }

    private void OnEnable()
    {
        if (!procedureManager)
            procedureManager = FindFirstObjectByType<FurnaceProcedureManager>();

        if (procedureManager)
        {
            procedureManager.StepEntered += HandleStepEntered;
            procedureManager.ProcedureCompleted += HandleProcedureCompleted;
            SetTarget(
                procedureManager.CurrentIndicatorTarget,
                procedureManager.CurrentIndicatorOffset);
        }
    }

    private void LateUpdate()
    {
        if (!currentTarget || (procedureManager && procedureManager.IsComplete))
        {
            SetVisible(false);
            return;
        }

        Vector3 targetPosition = currentTarget.position;
        float pulse = Mathf.Sin(Time.unscaledTime * pulseFrequency * Mathf.PI * 2f);
        Vector3 desiredPosition = targetPosition + currentOffset + Vector3.up * (pulse * pulseHeight);

        if (!isVisible)
        {
            transform.position = desiredPosition;
            SetVisible(true);
        }
        else
        {
            float followBlend = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followBlend);
        }

        Vector3 targetDirection = targetPosition - transform.position;
        if (targetDirection.sqrMagnitude > 0.000001f)
        {
            Quaternion desiredRotation = Quaternion.FromToRotation(Vector3.down, targetDirection.normalized);
            float rotationBlend = 1f - Mathf.Exp(-rotationSpeed * Time.unscaledDeltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
        }

        if (visualRoot)
        {
            float animatedScale = visualScale * (1f + pulse * pulseScale);
            visualRoot.localScale = Vector3.one * animatedScale;
        }
    }

    private void HandleStepEntered(int stepIndex, FurnaceProcedureManager.ProcedureStep step)
    {
        SetTarget(step?.indicatorTarget, step?.indicatorOffset ?? Vector3.zero);
    }

    private void HandleProcedureCompleted()
    {
        SetTarget(null, Vector3.zero);
    }

    private void SetTarget(Transform target, Vector3 offset)
    {
        currentTarget = target;
        currentOffset = offset;
        SetVisible(currentTarget);

        if (currentTarget)
        {
            transform.position = currentTarget.position + currentOffset;
        }
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (visualRoot && visualRoot.gameObject.activeSelf != visible)
            visualRoot.gameObject.SetActive(visible);
    }

    private void CreateRuntimeVisual()
    {
        GameObject arrowObject = new("Arrow Visual");
        arrowObject.layer = 2;
        arrowObject.transform.SetParent(transform, false);
        visualRoot = arrowObject.transform;

        MeshFilter meshFilter = arrowObject.AddComponent<MeshFilter>();
        runtimeMesh = CreateArrowMesh();
        meshFilter.sharedMesh = runtimeMesh;

        MeshRenderer meshRenderer = arrowObject.AddComponent<MeshRenderer>();
        indicatorRenderer = meshRenderer;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader)
            shader = Shader.Find("Unlit/Color");

        if (!shader)
        {
            Debug.LogError("The furnace step indicator requires an unlit shader.", this);
            return;
        }

        runtimeMaterial = new Material(shader)
        {
            name = "Furnace Step Indicator (Runtime)"
        };

        if (runtimeMaterial.HasProperty("_BaseColor"))
            runtimeMaterial.SetColor("_BaseColor", indicatorColor);
        if (runtimeMaterial.HasProperty("_Color"))
            runtimeMaterial.SetColor("_Color", indicatorColor);

        meshRenderer.sharedMaterial = runtimeMaterial;
    }

    private static Mesh CreateArrowMesh()
    {
        const int sideCount = 12;
        const float shaftRadius = 0.009f;
        const float headRadius = 0.027f;
        const float shaftTop = 0.055f;
        const float headBase = 0f;
        const float tip = -0.06f;

        Vector3[] vertices = new Vector3[sideCount * 3 + 2];
        int tipIndex = sideCount * 3;
        int topCenterIndex = tipIndex + 1;

        for (int i = 0; i < sideCount; i++)
        {
            float angle = i * Mathf.PI * 2f / sideCount;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);

            vertices[i] = new Vector3(x * shaftRadius, shaftTop, z * shaftRadius);
            vertices[sideCount + i] = new Vector3(x * shaftRadius, headBase, z * shaftRadius);
            vertices[sideCount * 2 + i] = new Vector3(x * headRadius, headBase, z * headRadius);
        }

        vertices[tipIndex] = new Vector3(0f, tip, 0f);
        vertices[topCenterIndex] = new Vector3(0f, shaftTop, 0f);

        int[] triangles = new int[sideCount * 18];
        int triangleIndex = 0;

        for (int i = 0; i < sideCount; i++)
        {
            int next = (i + 1) % sideCount;
            int top = i;
            int topNext = next;
            int shaftBottom = sideCount + i;
            int shaftBottomNext = sideCount + next;
            int head = sideCount * 2 + i;
            int headNext = sideCount * 2 + next;

            AddTriangle(triangles, ref triangleIndex, top, shaftBottomNext, shaftBottom);
            AddTriangle(triangles, ref triangleIndex, top, topNext, shaftBottomNext);
            AddTriangle(triangles, ref triangleIndex, topCenterIndex, topNext, top);
            AddTriangle(triangles, ref triangleIndex, shaftBottom, headNext, head);
            AddTriangle(triangles, ref triangleIndex, shaftBottom, shaftBottomNext, headNext);
            AddTriangle(triangles, ref triangleIndex, head, headNext, tipIndex);
        }

        Mesh mesh = new()
        {
            name = "Furnace Step Indicator Arrow",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddTriangle(int[] triangles, ref int index, int a, int b, int c)
    {
        triangles[index++] = a;
        triangles[index++] = b;
        triangles[index++] = c;
    }

    private void OnDisable()
    {
        if (procedureManager)
        {
            procedureManager.StepEntered -= HandleStepEntered;
            procedureManager.ProcedureCompleted -= HandleProcedureCompleted;
        }
    }

    private void OnDestroy()
    {
        if (runtimeMaterial)
            Destroy(runtimeMaterial);
        if (runtimeMesh)
            Destroy(runtimeMesh);
    }
}

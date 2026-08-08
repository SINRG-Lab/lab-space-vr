using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Math = System.Math;

public class Setting_Parameter : MonoBehaviour
{
    [Header("Radius (nm)")]
    public TextMeshProUGUI radius;

    [Header("Required Height (nm)")]
    public TextMeshProUGUI requied_height;

    [Header("Substrate Dimension")]
    public TextMeshProUGUI substrate_dimension;

    public GrowthRate growth_rate;

    [Header("Substrate")]
    public GameObject Substrate;

    [Header("Catalyst")]
    public Slider catalyst_count;
    [SerializeField, Min(1)] private int defaultCatalystCount = 24;
    [SerializeField, Min(1)] private int maxCatalystCount = 32;
    [Range(1f, 10f)] public float catalyst_size_randomness = 5f;
    public Transform catalyst_holder;

    [Header("NanoWire")]
    public GameObject NanoWire;

    [Header("Hologram Capture")]
    [SerializeField] private string hologramCaptureLayerName = "HologramCapture";

    [Header("Procedure")]
    [SerializeField] private FurnaceProcedureManager procedureManager;
    [SerializeField] private GrowthManager growthManager;
    [SerializeField] private GameObject parameterPanelRoot;
    [SerializeField] private Button primaryActionButton;
    [SerializeField] private TMP_Text primaryActionLabel;

    [Header("Growth Estimate")]
    [SerializeField] private TMP_Text estimatedGrowthTimeText;

    public bool growth_enabled;

    [Range(1f, 10f)]
    public float simSpeedMultiplier = 1f;

    public float simSpeed = 1e7f;

    private readonly List<GrowthRate> growthRates = new();
    private int last_count = -1;
    private bool parametersConfirmed;
    private double confirmedRadiusNm;
    private double confirmedTargetHeightNm;
    private int confirmedCatalystCount;
    private string lastEstimatedRadiusText;
    private string lastEstimatedHeightText;
    private float lastEstimatedGrowthMultiplier = float.NaN;
    private float lastEstimatedSimulationMultiplier = float.NaN;
    private int hologramCaptureLayer = -1;

    public bool HasGrowthVisuals => growthRates.Count > 0;
    public bool ParametersConfirmed => parametersConfirmed;

    public bool AllGrowthComplete
    {
        get
        {
            if (growthRates.Count == 0)
                return false;

            for (int i = 0; i < growthRates.Count; i++)
            {
                if (growthRates[i] && !growthRates[i].HasFinishedGrowth)
                    return false;
            }

            return true;
        }
    }

    public float GrowthProgress01
    {
        get
        {
            if (growthRates.Count == 0)
                return 0f;

            float total = 0f;
            int validCount = 0;
            for (int i = 0; i < growthRates.Count; i++)
            {
                if (!growthRates[i])
                    continue;

                total += growthRates[i].Progress01;
                validCount++;
            }

            return validCount > 0 ? total / validCount : 0f;
        }
    }

    private void Start()
    {
        ApplySaneDefaults();
        ResolveProcedureReferences();
        ResolveHologramCaptureLayer();

        if (catalyst_count)
        {
            catalyst_count.wholeNumbers = true;
            catalyst_count.minValue = 1f;
            catalyst_count.maxValue = Mathf.Max(1, maxCatalystCount);

            int requestedCount = Mathf.RoundToInt(catalyst_count.value);
            if (requestedCount < 1 || requestedCount > maxCatalystCount)
                requestedCount = Mathf.Clamp(defaultCatalystCount, 1, maxCatalystCount);

            catalyst_count.SetValueWithoutNotify(requestedCount);
            catalyst_count.onValueChanged.AddListener(OnCatalystCountChanged);
        }

        SpawnCatalyst();
        if (procedureManager)
            procedureManager.StepEntered += OnProcedureStepEntered;
        RefreshProcedureUi();
        RefreshGrowthTimeEstimate(true);
    }

    private void Update()
    {
        RefreshGrowthTimeEstimate(false);
    }

    private void OnDestroy()
    {
        if (catalyst_count)
            catalyst_count.onValueChanged.RemoveListener(OnCatalystCountChanged);
        if (procedureManager)
            procedureManager.StepEntered -= OnProcedureStepEntered;
    }

    private void OnCatalystCountChanged(float _)
    {
        if (parametersConfirmed)
        {
            catalyst_count.SetValueWithoutNotify(confirmedCatalystCount);
            return;
        }

        if (!growth_enabled)
            SpawnCatalyst();
    }

    private void SpawnCatalyst()
    {
        if (!catalyst_count || !catalyst_holder || !NanoWire || !Substrate)
            return;

        int count = Mathf.Clamp(
            Mathf.RoundToInt(catalyst_count.value),
            1,
            maxCatalystCount);
        if (count == last_count)
            return;

        last_count = count;
        growthRates.Clear();

        for (int i = catalyst_holder.childCount - 1; i >= 0; i--)
            Destroy(catalyst_holder.GetChild(i).gameObject);

        int spawnedCount = 0;
        if (growth_rate && growth_rate.gameObject.activeInHierarchy)
        {
            AssignToHologramCapture(growth_rate.gameObject);
            RegisterGrowthRate(growth_rate);
            spawnedCount = 1;
        }

        for (int i = spawnedCount; i < count; i++)
        {
            GameObject growthVisual = SpawnOnTopAnywhere(
                NanoWire,
                Substrate.transform,
                catalyst_holder);
            AssignToHologramCapture(growthVisual);
            RegisterGrowthRate(growthVisual.GetComponent<GrowthRate>());
        }

        growth_enabled = false;
    }

    private void ResolveHologramCaptureLayer()
    {
        hologramCaptureLayer = LayerMask.NameToLayer(hologramCaptureLayerName);
        if (hologramCaptureLayer < 0)
        {
            Debug.LogError(
                $"Missing Unity layer '{hologramCaptureLayerName}' for hologram capture.",
                this);
            return;
        }

        AssignToHologramCapture(Substrate);
        if (catalyst_holder)
            AssignToHologramCapture(catalyst_holder.gameObject);
    }

    private void AssignToHologramCapture(GameObject root)
    {
        if (!root || hologramCaptureLayer < 0)
            return;

        SetLayerRecursively(root.transform, hologramCaptureLayer);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }

    private GameObject SpawnOnTopAnywhere(
        GameObject prefab,
        Transform substrate,
        Transform parent = null)
    {
        Renderer substrateRenderer = substrate.GetComponentInChildren<Renderer>();
        Bounds substrateBounds = substrateRenderer.bounds;

        GameObject growthVisual = Instantiate(prefab, parent);
        Renderer growthRenderer = growthVisual.GetComponentInChildren<Renderer>();
        Vector3 extents = growthRenderer ? growthRenderer.bounds.extents : Vector3.zero;

        float x = Random.Range(
            substrateBounds.min.x + extents.x,
            substrateBounds.max.x - extents.x);
        float z = Random.Range(
            substrateBounds.min.z + extents.z,
            substrateBounds.max.z - extents.z);
        float y = substrateBounds.max.y;

        growthVisual.transform.SetPositionAndRotation(
            new Vector3(x, y, z),
            Quaternion.Euler(
                Random.Range(-10f, 10f),
                0f,
                Random.Range(-10f, 10f)));
        return growthVisual;
    }

    public void ToggleGrowthEnabled()
    {
        HandlePrimaryAction();
    }

    public void HandlePrimaryAction()
    {
        ResolveProcedureReferences();
        if (IsParameterConfigurationStep())
        {
            TryConfirmGrowthParameters();
            return;
        }

        if (growthManager)
            growthManager.TryStartGrowth();
        else
            SetGrowthEnabled(!growth_enabled);

        RefreshProcedureUi();
    }

    public void SetGrowthEnabled(bool enabled)
    {
        if (growth_enabled == enabled)
            return;

        growth_enabled = enabled;
    }

    public void ResetGrowthVisualization()
    {
        SetGrowthEnabled(false);
        for (int i = 0; i < growthRates.Count; i++)
            growthRates[i]?.ResetGrowth();
    }

    public void CompleteGrowthForDevelopment()
    {
        for (int i = 0; i < growthRates.Count; i++)
            growthRates[i]?.CompleteGrowthForDevelopment();
    }

    public bool TryGetGrowthParameters(
        out double radiusNm,
        out double targetHeightNm)
    {
        if (parametersConfirmed)
        {
            radiusNm = confirmedRadiusNm;
            targetHeightNm = confirmedTargetHeightNm;
            return true;
        }

        bool radiusValid = TryReadPositiveValue(radius, out radiusNm);
        bool heightValid = TryReadPositiveValue(requied_height, out targetHeightNm);
        return radiusValid && heightValid;
    }

    public bool ConfirmParametersForDevelopment()
    {
        return TryConfirmGrowthParameters();
    }

    public void ResetParameterConfirmationForDevelopment()
    {
        parametersConfirmed = false;
        confirmedRadiusNm = 0d;
        confirmedTargetHeightNm = 0d;
        confirmedCatalystCount = 0;
        RefreshProcedureUi();
    }

    private bool TryConfirmGrowthParameters()
    {
        ResolveProcedureReferences();
        if (!procedureManager || !IsParameterConfigurationStep())
        {
            Debug.LogWarning(
                "Growth parameters can only be confirmed during the Configure Growth step.",
                this);
            return false;
        }

        if (!TryReadPositiveValue(radius, out double radiusNm) ||
            !TryReadPositiveValue(requied_height, out double targetHeightNm) ||
            !catalyst_count)
        {
            Debug.LogWarning(
                "Growth parameters require a positive radius, positive target height, and catalyst count.",
                this);
            return false;
        }

        int catalystCount = Mathf.Clamp(
            Mathf.RoundToInt(catalyst_count.value),
            1,
            maxCatalystCount);
        catalyst_count.SetValueWithoutNotify(catalystCount);
        SpawnCatalyst();

        confirmedRadiusNm = radiusNm;
        confirmedTargetHeightNm = targetHeightNm;
        confirmedCatalystCount = catalystCount;
        parametersConfirmed = true;
        SetParameterControlsInteractable(false);
        RefreshGrowthTimeEstimate(true);
        procedureManager.MarkGrowthParametersSet();
        FurnaceInteractionFeedback.PlayActionConfirmed();
        return true;
    }

    private void OnProcedureStepEntered(
        int _,
        FurnaceProcedureManager.ProcedureStep step)
    {
        if (IsParameterConfigurationStep() &&
            !procedureManager.GetGate(FurnaceProcedureManager.Gate.GrowthParametersSet))
        {
            parametersConfirmed = false;
            confirmedRadiusNm = 0d;
            confirmedTargetHeightNm = 0d;
            confirmedCatalystCount = 0;
        }

        RefreshProcedureUi();
    }

    private bool IsParameterConfigurationStep()
    {
        return procedureManager &&
               procedureManager.IsGateRequiredByCurrentStep(
                   FurnaceProcedureManager.Gate.GrowthParametersSet);
    }

    private void RefreshProcedureUi()
    {
        ResolveProcedureReferences();
        bool isConfigurationStep = IsParameterConfigurationStep();
        bool isGrowthStartStep = procedureManager &&
                                 procedureManager.IsGateRequiredByCurrentStep(
                                     FurnaceProcedureManager.Gate.GrowthStarted);

        if (primaryActionLabel)
            primaryActionLabel.text = isConfigurationStep ? "Confirm" : "Start";

        if (primaryActionButton)
        {
            primaryActionButton.interactable =
                (isConfigurationStep && !parametersConfirmed) ||
                (isGrowthStartStep &&
                 !procedureManager.GetGate(
                     FurnaceProcedureManager.Gate.GrowthStarted));
        }

        SetParameterControlsInteractable(
            isConfigurationStep && !parametersConfirmed);
        RefreshGrowthTimeEstimate(true);
    }

    private void RefreshGrowthTimeEstimate(bool force)
    {
        ResolveGrowthEstimateText();
        if (!estimatedGrowthTimeText)
            return;

        string radiusText = radius ? radius.text : string.Empty;
        string heightText = requied_height ? requied_height.text : string.Empty;
        float growthMultiplier = GlobalSimSpeed.GrowthMultiplier;
        float simulationMultiplier = GlobalSimSpeed.Multiplier;

        if (!force &&
            radiusText == lastEstimatedRadiusText &&
            heightText == lastEstimatedHeightText &&
            Mathf.Approximately(growthMultiplier, lastEstimatedGrowthMultiplier) &&
            Mathf.Approximately(simulationMultiplier, lastEstimatedSimulationMultiplier))
        {
            return;
        }

        lastEstimatedRadiusText = radiusText;
        lastEstimatedHeightText = heightText;
        lastEstimatedGrowthMultiplier = growthMultiplier;
        lastEstimatedSimulationMultiplier = simulationMultiplier;

        if (!TryReadPositiveValue(radius, out double radiusNm) ||
            !TryReadPositiveValue(requied_height, out double targetHeightNm) ||
            !growth_rate ||
            !growth_rate.TryEstimateGrowthDuration(
                radiusNm,
                targetHeightNm,
                out _,
                out double demoSeconds))
        {
            estimatedGrowthTimeText.text = "Simulation: --";
            return;
        }

        estimatedGrowthTimeText.text = $"Simulation: {FormatDuration(demoSeconds)}";
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds < 1d)
            return $"{seconds:0.0} sec";
        if (seconds < 60d)
            return $"{seconds:0} sec";
        if (seconds < 3600d)
            return $"{Math.Floor(seconds / 60d):0}m {seconds % 60d:00} sec";
        if (seconds < 86400d)
            return $"{Math.Floor(seconds / 3600d):0}h {Math.Floor(seconds % 3600d / 60d):00}m";

        return $"{Math.Floor(seconds / 86400d):0}d {Math.Floor(seconds % 86400d / 3600d):00}h";
    }

    private void SetParameterControlsInteractable(bool interactable)
    {
        if (!parameterPanelRoot)
            return;

        Selectable[] controls = parameterPanelRoot.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i] && controls[i] != primaryActionButton)
                controls[i].interactable = interactable;
        }
    }

    private void ResolveProcedureReferences()
    {
        if (!procedureManager)
            procedureManager = FurnaceProcedureManager.Instance;
        if (!growthManager)
            growthManager = FindFirstObjectByType<GrowthManager>(FindObjectsInactive.Include);
        ResolveGrowthEstimateText();
    }

    private void ResolveGrowthEstimateText()
    {
        if (estimatedGrowthTimeText || !parameterPanelRoot)
            return;

        TMP_Text[] panelTexts =
            parameterPanelRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < panelTexts.Length; i++)
        {
            if (panelTexts[i] && panelTexts[i].name == "EstimatedGrowthTime")
            {
                estimatedGrowthTimeText = panelTexts[i];
                return;
            }
        }
    }

    private void RegisterGrowthRate(GrowthRate rate)
    {
        if (!rate || growthRates.Contains(rate))
            return;

        rate.Configure(this);
        rate.ResetGrowth();
        growthRates.Add(rate);
    }

    private void ApplySaneDefaults()
    {
        ApplyDefault(radius, 10f);
        ApplyDefault(requied_height, 50f);
    }

    private static void ApplyDefault(TMP_Text text, float fallback)
    {
        if (text &&
            float.TryParse(
                text.text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value) &&
            value > 0f)
        {
            return;
        }

        if (text)
            text.text = fallback.ToString("0", CultureInfo.InvariantCulture);
    }

    private static bool TryReadPositiveValue(TMP_Text text, out double value)
    {
        value = 0d;
        return text &&
               double.TryParse(
                   text.text,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out value) &&
               value > 0d;
    }
}

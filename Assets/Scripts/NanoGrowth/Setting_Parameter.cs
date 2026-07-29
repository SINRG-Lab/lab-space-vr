using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Procedure")]
    [SerializeField] private FurnaceProcedureManager procedureManager;
    [SerializeField] private GrowthManager growthManager;
    [SerializeField] private GameObject parameterPanelRoot;
    [SerializeField] private Button primaryActionButton;
    [SerializeField] private TMP_Text primaryActionLabel;

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
        if (growth_rate)
        {
            RegisterGrowthRate(growth_rate);
            spawnedCount = 1;
        }

        for (int i = spawnedCount; i < count; i++)
        {
            GameObject growthVisual = SpawnOnTopAnywhere(
                NanoWire,
                Substrate.transform,
                catalyst_holder);
            RegisterGrowthRate(growthVisual.GetComponent<GrowthRate>());
        }

        growth_enabled = false;
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
            growthManager.StartGrowth();
        else
            SetGrowthEnabled(!growth_enabled);
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

        if (primaryActionLabel)
            primaryActionLabel.text = isConfigurationStep ? "Confirm" : "Start";

        SetParameterControlsInteractable(
            isConfigurationStep && !parametersConfirmed);
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

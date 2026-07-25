using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RotationToGasFlow : MonoBehaviour
{
    [Header("References")]
    public TMP_Text valueText;
    public Transform target;
    public Slider progressSlider;
    [SerializeField] private FurnaceProcedureManager procedureManager;

    [Header("Rotation Mapping")]
    public float minAngle = 0f;
    public float maxAngle = 180f;
    public Axis axis = Axis.Z;
    [SerializeField] private bool captureMinimumFromInitialPose = true;
    [SerializeField] private Vector3 configuredMinimumLocalEuler;

    [Header("Output")]
    public float minValue;
    public float maxValue = 5000f;
    [SerializeField, Min(0f)] private float valueIncrement = 50f;
    [SerializeField] private string valueFormat = "0";
    [SerializeField] private string unit = "sccm";

    [Header("Feedback")]
    [SerializeField] private Color adjustingColor = new(0.2f, 0.78f, 1f, 1f);
    [SerializeField] private Color readyColor = new(0.28f, 0.95f, 0.5f, 1f);

    private Quaternion minimumLocalRotation;
    private Image sliderFillImage;
    private bool initialized;

    public float CurrentValue { get; private set; }
    public float NormalizedValue { get; private set; }
    public bool IsReady { get; private set; }

    public enum Axis
    {
        X,
        Y,
        Z
    }

    private void Start()
    {
        if (!target)
        {
            target = transform;
        }

        if (!procedureManager)
        {
            procedureManager = FurnaceProcedureManager.Instance;
        }

        minimumLocalRotation = captureMinimumFromInitialPose
            ? target.localRotation
            : Quaternion.Euler(configuredMinimumLocalEuler);

        ConfigureSlider();
        RefreshFromRotation(playFeedback: false);
    }

    private void Update()
    {
        if (target)
        {
            RefreshFromRotation(playFeedback: true);
        }
    }

    private void RefreshFromRotation(bool playFeedback)
    {
        float rotationRange = Mathf.Abs(maxAngle - minAngle);
        float rotationFromMinimum = Quaternion.Angle(minimumLocalRotation, target.localRotation);
        NormalizedValue = rotationRange > Mathf.Epsilon
            ? Mathf.Clamp01(rotationFromMinimum / rotationRange)
            : 0f;

        float rawValue = Mathf.Lerp(minValue, maxValue, NormalizedValue);
        float nextValue = QuantizeValue(rawValue);
        bool valueChanged = !initialized || !Mathf.Approximately(CurrentValue, nextValue);
        bool wasReady = IsReady;
        CurrentValue = nextValue;

        if (progressSlider)
        {
            progressSlider.SetValueWithoutNotify(CurrentValue);
        }

        if (valueChanged)
        {
            UpdateReadout();
        }

        PublishProcedureState(valueChanged);

        bool readinessChanged = !initialized || wasReady != IsReady;
        if (valueChanged || readinessChanged)
        {
            UpdateColors();
        }

        if (initialized && !wasReady && IsReady && playFeedback)
        {
            FurnaceInteractionFeedback.PlayActionConfirmed();
        }

        initialized = true;
    }

    private float QuantizeValue(float value)
    {
        if (valueIncrement <= Mathf.Epsilon)
        {
            return Mathf.Clamp(value, minValue, maxValue);
        }

        float quantized = Mathf.Round(value / valueIncrement) * valueIncrement;
        return Mathf.Clamp(quantized, minValue, maxValue);
    }

    private void ConfigureSlider()
    {
        if (!progressSlider)
        {
            return;
        }

        progressSlider.minValue = minValue;
        progressSlider.maxValue = maxValue;
        progressSlider.wholeNumbers = false;
        progressSlider.interactable = false;

        if (progressSlider.fillRect)
        {
            progressSlider.fillRect.TryGetComponent(out sliderFillImage);
        }
    }

    private void UpdateReadout()
    {
        if (!valueText)
        {
            return;
        }

        string formattedValue = CurrentValue.ToString(valueFormat);
        valueText.text = string.IsNullOrWhiteSpace(unit)
            ? formattedValue
            : $"{formattedValue} {unit}";
    }

    private void PublishProcedureState(bool valueChanged)
    {
        if (!procedureManager)
        {
            IsReady = CurrentValue > minValue;
            return;
        }

        bool managerState = procedureManager.GetGate(FurnaceProcedureManager.Gate.GasFlowReady);
        if (valueChanged || managerState != IsReady)
        {
            procedureManager.SetGasFlowValue(CurrentValue);
        }

        IsReady = procedureManager.GetGate(FurnaceProcedureManager.Gate.GasFlowReady);
    }

    private void UpdateColors()
    {
        Color color = IsReady ? readyColor : adjustingColor;
        if (valueText)
        {
            valueText.color = color;
        }

        if (sliderFillImage)
        {
            sliderFillImage.color = color;
        }
    }

    public void SetValueForDevelopment(float value)
    {
        if (!target)
        {
            target = transform;
        }

        float normalized = Mathf.InverseLerp(minValue, maxValue, value);
        float rotationRange = Mathf.Abs(maxAngle - minAngle);
        Vector3 rotationAxis = axis switch
        {
            Axis.X => Vector3.right,
            Axis.Y => Vector3.up,
            _ => Vector3.forward
        };

        target.localRotation =
            minimumLocalRotation *
            Quaternion.AngleAxis(rotationRange * normalized, rotationAxis);
        Physics.SyncTransforms();
        RefreshFromRotation(playFeedback: false);
    }

    public void ResetForDevelopment()
    {
        SetValueForDevelopment(minValue);
    }

    private void OnValidate()
    {
        maxValue = Mathf.Max(maxValue, minValue);
        valueIncrement = Mathf.Max(0f, valueIncrement);
    }
}

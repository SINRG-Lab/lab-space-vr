using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

public class IncreaseTemperature : MonoBehaviour
{
    public enum HeatingState
    {
        Idle,
        Ramping,
        Soaking,
        Paused,
        Complete,
        Cooling
    }

    [Header("UI")]
    public TMP_Text maxValueText_zone1;
    public TMP_Text maxValueText_zone2;
    public TMP_Text maxValueText_zone3;
    public TMP_Text currValueText_zone1;
    public TMP_Text currValueText_zone2;
    public TMP_Text currValueText_zone3;

    [Header("Blinking Indicator")]
    public Renderer blinkRenderer;
    public Material blinkMaterialA;
    public Material blinkMaterialB;
    public float blinkInterval = 0.2f;
    public TemperatureMaterialController tempMat;

    [Header("Value Settings")]
    public float currentValue_zone1;
    public float currentValue_zone2;
    public float currentValue_zone3;
    public float maxValue_zone1 = 870f;
    public float maxValue_zone2 = 870f;
    public float maxValue_zone3 = 870f;
    [Min(0.01f)] public float duration = 20f;

    [Header("Procedure")]
    [SerializeField] private FurnaceProcedureManager procedureManager;
    [SerializeField, Min(0f)] private float minimumSetpoint = 500f;
    [SerializeField, Min(0f)] private float soakDuration = 5f;
    [SerializeField, Min(0f)] private float safeWithdrawalTemperature = 50f;
    [SerializeField, Min(0.01f)] private float cooldownDuration = 20f;
    [SerializeField] private bool pauseWhenSafetyGateDrops = true;
    [SerializeField] private HeatingState state = HeatingState.Idle;

    public bool isIncreasingTemperature;
    public bool Zone1Reached { get; private set; }
    public bool Zone2Reached { get; private set; }
    public bool Zone3Reached { get; private set; }
    public HeatingState State => state;
    public bool AllZonesReached => Zone1Reached && Zone2Reached && Zone3Reached;
    public float SoakProgress01 { get; private set; }
    public float CooldownProgress01 { get; private set; }
    public float SafeWithdrawalTemperature => safeWithdrawalTemperature;
    public float MaxCurrentTemperature =>
        Mathf.Max(currentValue_zone1, currentValue_zone2, currentValue_zone3);
    public bool IsSafeForWithdrawal =>
        MaxCurrentTemperature <= safeWithdrawalTemperature + reachedTolerance;

    [SerializeField] private float reachedTolerance = 0.5f;

    private Coroutine routineAll;
    private float cooldownStartTemperature;
    private bool cooldownCompletionPublished;

    private void Start()
    {
        ResolveProcedureManager();
        ReadMaxFromTextZones();
        ApplyAll(
            currentValue_zone1,
            currentValue_zone2,
            currentValue_zone3,
            maxValue_zone1,
            maxValue_zone2,
            maxValue_zone3,
            false);
        PublishSetpointGate();
    }

    public void ReadMaxFromTextZones()
    {
        TryReadSetpoints();
    }

    public void NotifySetpointsChanged()
    {
        bool parsed = TryReadSetpoints();
        PublishSetpointGate(parsed);

        if (state == HeatingState.Complete &&
            (!IsAtTarget(currentValue_zone1, maxValue_zone1) ||
             !IsAtTarget(currentValue_zone2, maxValue_zone2) ||
             !IsAtTarget(currentValue_zone3, maxValue_zone3)))
        {
            ResolveProcedureManager();
            procedureManager?.SetHeatSoakComplete(false);
            state = HeatingState.Idle;
        }
    }

    public void StartAllZones()
    {
        if (state == HeatingState.Ramping ||
            state == HeatingState.Soaking ||
            state == HeatingState.Paused)
        {
            return;
        }

        bool parsed = TryReadSetpoints();
        PublishSetpointGate(parsed);
        if (!parsed || !AreSetpointsValid())
        {
            Debug.LogWarning(
                $"All temperature zones must be at least {minimumSetpoint:0} before heating.",
                this);
            return;
        }

        ResolveProcedureManager();
        if (!CanHeat())
        {
            Debug.LogWarning(
                "Heating requires power, a closed furnace, valid setpoints, and safe gas flow.",
                this);
            return;
        }

        procedureManager?.SetHeatSoakComplete(false);
        Zone1Reached = Zone2Reached = Zone3Reached = false;
        SoakProgress01 = 0f;

        RestartAll(RampAndSoak(
            currentValue_zone1, maxValue_zone1,
            currentValue_zone2, maxValue_zone2,
            currentValue_zone3, maxValue_zone3));
    }

    public void ResetAllZones()
    {
        ResolveProcedureManager();
        if (procedureManager &&
            procedureManager.IsGateRequiredByCurrentStep(
                FurnaceProcedureManager.Gate.CooldownComplete))
        {
            StartCooldown();
            return;
        }

        procedureManager?.SetHeatSoakComplete(false);

        Zone1Reached = Zone2Reached = Zone3Reached = false;
        SoakProgress01 = 0f;
        isIncreasingTemperature = false;

        RestartAll(CoolAllZones(
            currentValue_zone1,
            currentValue_zone2,
            currentValue_zone3,
            false));
    }

    public void StartCooldown()
    {
        if (state == HeatingState.Cooling)
            return;

        ResolveProcedureManager();
        if (procedureManager &&
            !procedureManager.IsGateRequiredByCurrentStep(
                FurnaceProcedureManager.Gate.CooldownComplete))
        {
            Debug.LogWarning(
                "Cooldown can only be started during the Cool Down procedure step.",
                this);
            return;
        }

        procedureManager?.SetHeatSoakComplete(false);
        procedureManager?.SetCooldownComplete(false);
        Zone1Reached = Zone2Reached = Zone3Reached = false;
        SoakProgress01 = 0f;
        CooldownProgress01 = 0f;
        cooldownCompletionPublished = false;
        isIncreasingTemperature = false;

        RestartAll(CoolAllZones(
            currentValue_zone1,
            currentValue_zone2,
            currentValue_zone3,
            true));
    }

    public void SetAllSetpointsForDevelopment(float value)
    {
        string formattedValue = value.ToString("0", CultureInfo.InvariantCulture);
        if (maxValueText_zone1)
            maxValueText_zone1.text = formattedValue;
        if (maxValueText_zone2)
            maxValueText_zone2.text = formattedValue;
        if (maxValueText_zone3)
            maxValueText_zone3.text = formattedValue;

        maxValue_zone1 = value;
        maxValue_zone2 = value;
        maxValue_zone3 = value;
        PublishSetpointGate();
    }

    public bool CompleteHeatSoakForDevelopment()
    {
        TryReadSetpoints();
        PublishSetpointGate();
        if (!AreSetpointsValid() || !CanHeat())
            return false;

        if (routineAll != null)
        {
            StopCoroutine(routineAll);
            routineAll = null;
        }

        ApplyAll(
            maxValue_zone1,
            maxValue_zone2,
            maxValue_zone3,
            maxValue_zone1,
            maxValue_zone2,
            maxValue_zone3,
            true);
        Zone1Reached = Zone2Reached = Zone3Reached = true;
        SoakProgress01 = 1f;
        isIncreasingTemperature = false;
        state = HeatingState.Complete;

        ResolveProcedureManager();
        procedureManager?.MarkHeatSoakComplete();
        return true;
    }

    public void StartCooldownForDevelopment(bool instant)
    {
        if (instant)
        {
            ResolveProcedureManager();
            procedureManager?.SetHeatSoakComplete(false);
            procedureManager?.SetCooldownComplete(false);
            if (routineAll != null)
            {
                StopCoroutine(routineAll);
                routineAll = null;
            }

            ApplyAll(0f, 0f, 0f, 0f, 0f, 0f, false);
            Zone1Reached = Zone2Reached = Zone3Reached = false;
            SoakProgress01 = 0f;
            CooldownProgress01 = 1f;
            isIncreasingTemperature = false;
            state = HeatingState.Idle;
            cooldownCompletionPublished = true;
            procedureManager?.MarkCooldownComplete();
            return;
        }

        StartCooldown();
    }

    public void ResetForDevelopment()
    {
        if (routineAll != null)
        {
            StopCoroutine(routineAll);
            routineAll = null;
        }

        SetAllSetpointsForDevelopment(0f);
        ApplyAll(0f, 0f, 0f, 0f, 0f, 0f, false);
        Zone1Reached = Zone2Reached = Zone3Reached = false;
        SoakProgress01 = 0f;
        CooldownProgress01 = 0f;
        cooldownStartTemperature = 0f;
        cooldownCompletionPublished = false;
        isIncreasingTemperature = false;
        state = HeatingState.Idle;

        ResolveProcedureManager();
        procedureManager?.SetHeatSoakComplete(false);
        procedureManager?.SetCooldownComplete(false);
    }

    private void RestartAll(IEnumerator routine)
    {
        if (routineAll != null)
            StopCoroutine(routineAll);

        routineAll = StartCoroutine(routine);
    }

    private IEnumerator RampAndSoak(
        float from1, float to1,
        float from2, float to2,
        float from3, float to3)
    {
        float elapsed = 0f;
        float rampDuration = Mathf.Max(0.01f, duration);

        while (elapsed < rampDuration)
        {
            if (!CanHeat())
            {
                state = HeatingState.Paused;
                isIncreasingTemperature = false;
                yield return null;
                continue;
            }

            state = HeatingState.Ramping;
            isIncreasingTemperature = true;
            elapsed += GetSimulationDeltaTime();
            float t = Mathf.Clamp01(elapsed / rampDuration);

            ApplyAll(
                Mathf.Lerp(from1, to1, t),
                Mathf.Lerp(from2, to2, t),
                Mathf.Lerp(from3, to3, t),
                to1, to2, to3,
                true);

            yield return null;
        }

        ApplyAll(to1, to2, to3, to1, to2, to3, true);

        ResolveProcedureManager();
        procedureManager?.MarkHeatSoakComplete();

        float soakElapsed = 0f;
        while (soakElapsed < soakDuration)
        {
            if (!CanHeat())
            {
                state = HeatingState.Paused;
                isIncreasingTemperature = false;
                yield return null;
                continue;
            }

            state = HeatingState.Soaking;
            isIncreasingTemperature = true;
            soakElapsed += GetSimulationDeltaTime();
            SoakProgress01 = soakDuration <= 0f
                ? 1f
                : Mathf.Clamp01(soakElapsed / soakDuration);
            yield return null;
        }

        SoakProgress01 = 1f;
        isIncreasingTemperature = false;
        state = HeatingState.Complete;
        routineAll = null;

        FurnaceInteractionFeedback.PlayActionConfirmed();
    }

    private IEnumerator CoolAllZones(
        float from1,
        float from2,
        float from3,
        bool publishCooldownCompletion)
    {
        state = HeatingState.Cooling;
        float elapsed = 0f;
        float coolingTime = Mathf.Max(0.01f, cooldownDuration);
        cooldownStartTemperature = Mathf.Max(from1, from2, from3);
        CooldownProgress01 = IsSafeForWithdrawal ? 1f : 0f;
        cooldownCompletionPublished = false;

        if (publishCooldownCompletion && IsSafeForWithdrawal)
            PublishCooldownCompletion();

        while (elapsed < coolingTime)
        {
            elapsed += GetSimulationDeltaTime();
            float t = Mathf.Clamp01(elapsed / coolingTime);
            ApplyAll(
                Mathf.Lerp(from1, 0f, t),
                Mathf.Lerp(from2, 0f, t),
                Mathf.Lerp(from3, 0f, t),
                0f, 0f, 0f,
                false);

            CooldownProgress01 = cooldownStartTemperature <= safeWithdrawalTemperature
                ? 1f
                : 1f - Mathf.InverseLerp(
                    safeWithdrawalTemperature,
                    cooldownStartTemperature,
                    MaxCurrentTemperature);

            if (publishCooldownCompletion &&
                !cooldownCompletionPublished &&
                IsSafeForWithdrawal)
            {
                PublishCooldownCompletion();
            }

            yield return null;
        }

        ApplyAll(0f, 0f, 0f, 0f, 0f, 0f, false);
        CooldownProgress01 = 1f;
        state = HeatingState.Idle;
        routineAll = null;

        if (publishCooldownCompletion && !cooldownCompletionPublished)
            PublishCooldownCompletion();
    }

    private void PublishCooldownCompletion()
    {
        cooldownCompletionPublished = true;
        FurnaceInteractionFeedback.PlayActionConfirmed();
        ResolveProcedureManager();
        procedureManager?.MarkCooldownComplete();
    }

    private void ApplyAll(
        float v1, float v2, float v3,
        float target1, float target2, float target3,
        bool updateReachedState)
    {
        currentValue_zone1 = v1;
        currentValue_zone2 = v2;
        currentValue_zone3 = v3;

        if (currValueText_zone1)
            currValueText_zone1.text = v1.ToString("0.0", CultureInfo.InvariantCulture);
        if (currValueText_zone2)
            currValueText_zone2.text = v2.ToString("0.0", CultureInfo.InvariantCulture);
        if (currValueText_zone3)
            currValueText_zone3.text = v3.ToString("0.0", CultureInfo.InvariantCulture);

        if (tempMat)
        {
            tempMat.SetTemperatureZone1(v1);
            tempMat.SetTemperatureZone2(v2);
            tempMat.SetTemperatureZone3(v3);
        }

        if (!updateReachedState)
            return;

        Zone1Reached = IsAtTarget(v1, target1);
        Zone2Reached = IsAtTarget(v2, target2);
        Zone3Reached = IsAtTarget(v3, target3);
    }

    private bool TryReadSetpoints()
    {
        bool zone1Parsed = TryReadSetpoint(maxValueText_zone1, out float zone1);
        bool zone2Parsed = TryReadSetpoint(maxValueText_zone2, out float zone2);
        bool zone3Parsed = TryReadSetpoint(maxValueText_zone3, out float zone3);

        if (zone1Parsed)
            maxValue_zone1 = zone1;
        if (zone2Parsed)
            maxValue_zone2 = zone2;
        if (zone3Parsed)
            maxValue_zone3 = zone3;

        return zone1Parsed && zone2Parsed && zone3Parsed;
    }

    private static bool TryReadSetpoint(TMP_Text valueText, out float value)
    {
        value = 0f;
        return valueText &&
               float.TryParse(
                   valueText.text,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out value);
    }

    private bool AreSetpointsValid()
    {
        return maxValue_zone1 >= minimumSetpoint &&
               maxValue_zone2 >= minimumSetpoint &&
               maxValue_zone3 >= minimumSetpoint;
    }

    private void PublishSetpointGate(bool parsed = true)
    {
        ResolveProcedureManager();
        procedureManager?.SetTemperatureZonesSet(parsed && AreSetpointsValid());
    }

    private bool CanHeat()
    {
        if (!pauseWhenSafetyGateDrops)
            return true;

        ResolveProcedureManager();
        if (!procedureManager)
            return true;

        return procedureManager.GetGate(FurnaceProcedureManager.Gate.PowerOn) &&
               procedureManager.GetGate(FurnaceProcedureManager.Gate.FurnaceClosed) &&
               procedureManager.GetGate(FurnaceProcedureManager.Gate.GasFlowReady) &&
               procedureManager.GetGate(FurnaceProcedureManager.Gate.TemperatureZonesSet);
    }

    private bool IsAtTarget(float value, float target)
    {
        return Mathf.Abs(value - target) <= reachedTolerance;
    }

    private static float GetSimulationDeltaTime()
    {
        return Time.deltaTime * Mathf.Max(0f, GlobalSimSpeed.Multiplier);
    }

    private void ResolveProcedureManager()
    {
        if (!procedureManager)
            procedureManager = FurnaceProcedureManager.Instance;
    }

    private void OnDisable()
    {
        if (routineAll != null)
        {
            StopCoroutine(routineAll);
            routineAll = null;
        }

        isIncreasingTemperature = false;
        if (state != HeatingState.Complete)
            state = HeatingState.Idle;
    }
}

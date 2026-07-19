using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class QuestPerformanceBootstrap : MonoBehaviour
{
    [SerializeField, Min(1f)] private float targetRefreshRate = 72f;
    [SerializeField, Min(1)] private int maxWaitFrames = 180;
    [SerializeField] private bool logResult = true;

    private IEnumerator Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        for (int frame = 0; frame < maxWaitFrames; frame++)
        {
            float[] availableRates = OVRPlugin.systemDisplayFrequenciesAvailable;
            if (availableRates != null && availableRates.Length > 0)
            {
                ApplyClosestSupportedRate(availableRates);
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning(
            $"Could not set the requested {targetRefreshRate:0.#} Hz refresh rate because the Quest display was not ready.",
            this);
#else
        yield break;
#endif
    }

    private void ApplyClosestSupportedRate(float[] availableRates)
    {
        float selectedRate = availableRates[0];
        float closestDifference = Mathf.Abs(selectedRate - targetRefreshRate);

        for (int i = 1; i < availableRates.Length; i++)
        {
            float difference = Mathf.Abs(availableRates[i] - targetRefreshRate);
            if (difference < closestDifference)
            {
                selectedRate = availableRates[i];
                closestDifference = difference;
            }
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Mathf.RoundToInt(selectedRate);
        OVRPlugin.systemDisplayFrequency = selectedRate;

        if (logResult)
        {
            Debug.Log(
                $"Quest performance baseline: requested {targetRefreshRate:0.#} Hz, using {selectedRate:0.#} Hz.",
                this);
        }
    }
}

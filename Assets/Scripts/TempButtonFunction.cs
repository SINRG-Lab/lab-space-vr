using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class TempButtonFunction : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Target")]
    public TMP_Text valueText;

    [Header("Behavior")]
    public bool isIncrement = true;
    public int step = 5;
    public float minValue = 0f;
    public float maxValue = 1000f;

    [Header("Long Press")]
    public float longPressDelay = 0.35f;
    public float repeatInterval = 0.07f;

    bool holding;
    Coroutine repeatCo;

    public void ClickOnce() => ApplyDelta();

    public void OnPointerDown(PointerEventData eventData)
    {
        holding = true;
        ApplyDelta();
        repeatCo = StartCoroutine(HoldRepeat());
    }

    public void OnPointerUp(PointerEventData eventData) => StopHold();
    public void OnPointerExit(PointerEventData eventData) => StopHold();

    void StopHold()
    {
        holding = false;
        if (repeatCo != null) { StopCoroutine(repeatCo); repeatCo = null; }
    }

    IEnumerator HoldRepeat()
    {
        yield return new WaitForSeconds(longPressDelay);
        while (holding)
        {
            ApplyDelta();
            yield return new WaitForSeconds(repeatInterval);
        }
    }

    void ApplyDelta()
    {
        if (!valueText) return;

        if (!float.TryParse(valueText.text, out float val))
            val = 0f;

        val += isIncrement ? step : -step;
        val = Mathf.Clamp(val, minValue, maxValue);
        valueText.text = val.ToString();
    }
}

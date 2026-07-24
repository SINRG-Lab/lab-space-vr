using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Globalization;
using UnityEngine.UI;

public class TempButtonFunction : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Target")]
    public TMP_Text valueText;
    public IncreaseTemperature temperatureController;

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
    Selectable selectable;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void ClickOnce() => ApplyDelta();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (selectable && !selectable.interactable)
            return;

        holding = true;
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
        while (holding && (!selectable || selectable.interactable))
        {
            ApplyDelta();
            yield return new WaitForSeconds(repeatInterval);
        }

        holding = false;
        repeatCo = null;
    }

    void ApplyDelta()
    {
        if (!valueText) return;

        if (!float.TryParse(
                valueText.text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float val))
            val = 0f;

        val += isIncrement ? step : -step;
        val = Mathf.Clamp(val, minValue, maxValue);
        valueText.text = val.ToString("0", CultureInfo.InvariantCulture);
        temperatureController?.NotifySetpointsChanged();
    }

    void OnDisable()
    {
        StopHold();
    }
}

using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HologramStreamModeSelector : MonoBehaviour
{
    [SerializeField] private HologramProcedureFocus controller;
    [SerializeField] private Canvas hostCanvas;

    [Header("Placement")]
    [SerializeField] private Vector2 anchoredPosition = new(0f, 0.72f);
    [SerializeField] private float panelDepth = 0.43f;
    [SerializeField] private Vector2 panelSize = new(400f, 52f);
    [SerializeField, Min(0.0001f)] private float worldScale = 0.005f;

    [Header("Appearance")]
    [SerializeField] private Color panelColor = new(0.015f, 0.025f, 0.035f, 0.96f);
    [SerializeField] private Color inactiveColor = new(0.08f, 0.105f, 0.125f, 1f);
    [SerializeField] private Color activeColor = new(0.16f, 0.76f, 0.95f, 1f);
    [SerializeField] private Color inactiveTextColor = Color.white;
    [SerializeField] private Color activeTextColor = new(0.015f, 0.025f, 0.035f, 1f);

    private readonly List<Button> modeButtons = new();
    private readonly List<TMP_Text> modeLabels = new();
    private GameObject selectorRoot;

    private static readonly string[] ModeNames =
    {
        "Procedure",
        "Operator",
        "Hands",
        "Head"
    };

    private void Start()
    {
        ResolveReferences();
        if (!controller || !hostCanvas)
        {
            Debug.LogWarning(
                "Hologram stream selector needs a controller and host canvas.",
                this);
            return;
        }

        BuildSelector();
        controller.StreamModeChanged += RefreshSelection;
        RefreshSelection(controller.CurrentStreamMode);
    }

    private void OnDestroy()
    {
        if (controller)
            controller.StreamModeChanged -= RefreshSelection;
    }

    private void ResolveReferences()
    {
        if (!controller)
        {
            controller = GetComponent<HologramProcedureFocus>();
        }

        if (!controller)
        {
            controller = FindFirstObjectByType<HologramProcedureFocus>(
                FindObjectsInactive.Include);
        }

        if (!hostCanvas)
        {
            FurnaceProcedureManager manager =
                FurnaceProcedureManager.Instance;
            if (manager)
                hostCanvas = manager.GetComponentInChildren<Canvas>(true);
        }
    }

    private void BuildSelector()
    {
        selectorRoot = new GameObject(
            "Hologram Stream Modes",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(Image),
            typeof(HologramCaptureExclude));
        selectorRoot.layer = hostCanvas.gameObject.layer;

        RectTransform selectorRect =
            selectorRoot.GetComponent<RectTransform>();
        selectorRect.SetParent(hostCanvas.transform, false);
        selectorRect.anchorMin = new Vector2(0.5f, 0.5f);
        selectorRect.anchorMax = new Vector2(0.5f, 0.5f);
        selectorRect.pivot = new Vector2(0.5f, 0.5f);
        selectorRect.sizeDelta = panelSize;
        selectorRect.anchoredPosition = anchoredPosition;
        selectorRect.localScale = Vector3.one * worldScale;
        selectorRect.localRotation = Quaternion.identity;
        Vector3 localPosition = selectorRect.localPosition;
        localPosition.z = panelDepth;
        selectorRect.localPosition = localPosition;

        Canvas selectorCanvas = selectorRoot.GetComponent<Canvas>();
        selectorCanvas.renderMode = RenderMode.WorldSpace;
        selectorCanvas.worldCamera = hostCanvas.worldCamera;
        selectorCanvas.overrideSorting = true;
        selectorCanvas.sortingOrder = hostCanvas.sortingOrder + 20;

        Image panelImage = selectorRoot.GetComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = false;

        TMP_FontAsset font = null;
        TMP_Text sourceText = hostCanvas.GetComponentInChildren<TMP_Text>(true);
        if (sourceText)
            font = sourceText.font;

        const float margin = 5f;
        const float gap = 4f;
        float buttonWidth =
            (panelSize.x - margin * 2f - gap * (ModeNames.Length - 1)) /
            ModeNames.Length;
        float buttonHeight = panelSize.y - margin * 2f;
        float firstCenterX =
            -panelSize.x * 0.5f + margin + buttonWidth * 0.5f;

        modeButtons.Clear();
        modeLabels.Clear();
        for (int i = 0; i < ModeNames.Length; i++)
        {
            HologramProcedureFocus.StreamMode mode =
                (HologramProcedureFocus.StreamMode)i;
            Button button = CreateButton(
                selectorRect,
                ModeNames[i],
                new Vector2(firstCenterX + i * (buttonWidth + gap), 0f),
                new Vector2(buttonWidth, buttonHeight),
                font);
            button.onClick.AddListener(() => controller.SetStreamMode(mode));
        }

        ConfigureInteraction(selectorCanvas, selectorRect);
    }

    private Button CreateButton(
        RectTransform parent,
        string label,
        Vector2 position,
        Vector2 size,
        TMP_FontAsset font)
    {
        GameObject buttonObject = new GameObject(
            $"{label} Mode",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.layer = selectorRoot.layer;

        RectTransform buttonRect =
            buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(parent, false);
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = size;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = inactiveColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.78f, 0.9f, 0.96f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.layer = selectorRoot.layer;

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText =
            labelObject.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = 16f;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.raycastTarget = false;
        labelText.color = inactiveTextColor;
        if (font)
            labelText.font = font;

        modeButtons.Add(button);
        modeLabels.Add(labelText);
        return button;
    }

    private static void ConfigureInteraction(
        Canvas canvas,
        RectTransform canvasRect)
    {
        PointableCanvas pointableCanvas =
            canvas.gameObject.AddComponent<PointableCanvas>();
        pointableCanvas.InjectAllPointableCanvas(canvas);

        PlaneSurface planeSurface =
            canvas.gameObject.AddComponent<PlaneSurface>();
        planeSurface.InjectAllPlaneSurface(
            PlaneSurface.NormalFacing.Backward,
            true);

        BoundsClipper clipper =
            canvas.gameObject.AddComponent<BoundsClipper>();
        clipper.Position = Vector3.zero;
        clipper.Size = new Vector3(
            canvasRect.rect.width,
            canvasRect.rect.height,
            0.01f);

        ClippedPlaneSurface clippedSurface =
            canvas.gameObject.AddComponent<ClippedPlaneSurface>();
        clippedSurface.InjectAllClippedPlaneSurface(
            planeSurface,
            new IBoundsClipper[] { clipper });

        RayInteractable rayInteractable =
            canvas.gameObject.AddComponent<RayInteractable>();
        rayInteractable.InjectAllRayInteractable(clippedSurface);
        rayInteractable.InjectOptionalPointableElement(pointableCanvas);

        PokeInteractable pokeInteractable =
            canvas.gameObject.AddComponent<PokeInteractable>();
        pokeInteractable.InjectAllPokeInteractable(clippedSurface);
        pokeInteractable.InjectOptionalPointableElement(pointableCanvas);
    }

    private void RefreshSelection(
        HologramProcedureFocus.StreamMode selectedMode)
    {
        int selectedIndex = (int)selectedMode;
        for (int i = 0; i < modeButtons.Count; i++)
        {
            bool selected = i == selectedIndex;
            Image image = modeButtons[i]
                ? modeButtons[i].targetGraphic as Image
                : null;
            if (image)
                image.color = selected ? activeColor : inactiveColor;
            if (i < modeLabels.Count && modeLabels[i])
            {
                modeLabels[i].color = selected
                    ? activeTextColor
                    : inactiveTextColor;
            }
        }
    }
}

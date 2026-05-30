using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class AnatomyPartFocusController : MonoBehaviour
{
    [Serializable]
    public class AnatomyPart
    {
        public string displayName;
        [Tooltip("Optional words matched against renderer object names and material names under Model Root.")]
        public string[] matchKeywords;
        [Tooltip("Optional manually assigned objects for this part. All child renderers are included.")]
        public GameObject[] targetObjects;
        [Tooltip("Optional manually assigned renderers for this part.")]
        public Renderer[] targetRenderers;
        [Tooltip("Optional UI button. If assigned, it is wired automatically at runtime.")]
        public Button button;
        [Tooltip("Optional target transform for this part. If empty, the controller uses Default Focus Target.")]
        public Transform focusTargetOverride;
        [Tooltip("Optional objects that hold grab/interactable components for this part.")]
        public GameObject[] grabObjects;
        [Tooltip("Optional exact grab/interactable components for this part.")]
        public Behaviour[] grabBehaviours;
    }

    [Header("Model")]
    [SerializeField] Transform modelRoot;
    [SerializeField] bool includeInactiveRenderers = true;
    [SerializeField] bool autoResolveTargetsOnStart = true;

    [Header("UI")]
    [SerializeField] Button showAllButton;
    [SerializeField] AnatomyPart[] parts = new AnatomyPart[0];

    [Header("Focus Behavior")]
    [SerializeField] bool hideUnfocusedRenderers = true;
    [SerializeField] bool disableUnfocusedColliders = true;
    [SerializeField] GameObject[] alwaysVisibleObjects;
    [SerializeField] bool showAllOnStart = true;

    [Header("Focus Movement")]
    [SerializeField] bool moveModelRootOnFocus = true;
    [SerializeField] Transform defaultFocusTarget;
    [Tooltip("When enabled, the focused renderers' bounds center moves to the focus target instead of moving the model pivot there.")]
    [SerializeField] bool alignFocusedBoundsCenterToTarget = true;
    [SerializeField] Vector3 focusTargetOffset;
    [SerializeField] bool matchFocusTargetRotation = true;
    [SerializeField] bool matchFocusTargetScale = false;
    [SerializeField] float focusMoveDuration = 0.35f;
    [SerializeField] bool logFocusMovementWarnings = true;

    [Header("Focused Grab")]
    [SerializeField] bool requireFocusForGrab = true;
    [SerializeField] bool autoFindGrabBehaviours = true;
    [SerializeField] string[] grabBehaviourTypeNameKeywords = { "Grab", "Grabbable", "Interactable" };
    [SerializeField] bool disableGrabWhenShowingAll = true;

    [Header("Temporary Desktop Test UI")]
    [SerializeField] bool showDesktopTestButtons = true;
    [SerializeField] bool createRuntimeDesktopCanvas = true;
    [SerializeField] bool showImguiFallback = false;
    [SerializeField] bool enableNumberKeyShortcuts = true;
    [SerializeField] Vector2 desktopTestPanelPosition = new Vector2(20f, 20f);
    [SerializeField] Vector2 desktopTestPanelSize = new Vector2(220f, 420f);

    [Header("Hand Render Style Grab Setup")]
    [SerializeField] bool installHandRenderStyleGrabSetup = true;
    [SerializeField] bool useFocusedBoundsForGrabCollider = true;

    readonly Dictionary<AnatomyPart, List<Renderer>> resolvedRenderers = new Dictionary<AnatomyPart, List<Renderer>>();
    readonly HashSet<Renderer> allRenderers = new HashSet<Renderer>();
    readonly HashSet<Collider> allColliders = new HashSet<Collider>();
    readonly HashSet<Renderer> alwaysVisibleRenderers = new HashSet<Renderer>();
    readonly HashSet<Collider> alwaysVisibleColliders = new HashSet<Collider>();
    readonly Dictionary<AnatomyPart, List<Behaviour>> resolvedGrabBehaviours = new Dictionary<AnatomyPart, List<Behaviour>>();
    readonly HashSet<Behaviour> allGrabBehaviours = new HashSet<Behaviour>();
    readonly HashSet<Behaviour> installedHandRenderStyleGrabBehaviours = new HashSet<Behaviour>();

    Vector3 homePosition;
    Quaternion homeRotation;
    Vector3 homeScale;
    Vector3 moveStartPosition;
    Quaternion moveStartRotation;
    Vector3 moveStartScale;
    Vector3 moveEndPosition;
    Quaternion moveEndRotation;
    Vector3 moveEndScale;
    float moveElapsed;
    bool homePoseCaptured;
    bool isMoving;
    AnatomyPart pendingGrabPart;
    List<Renderer> pendingGrabRenderers;
    bool enableGrabWhenFocusMoveCompletes;
    GameObject runtimeDesktopCanvas;

    void Reset()
    {
        modelRoot = transform;
    }

    void Awake()
    {
        if (!modelRoot)
        {
            modelRoot = transform;
        }

        CaptureHomePose();
        CacheModelContent();

        if (autoResolveTargetsOnStart)
        {
            ResolveAllParts();
        }

        InstallHandRenderStyleGrabSetupIfNeeded();

        BindButtons();

        if (showAllOnStart)
        {
            ShowAll();
        }

        CreateDesktopTestCanvas();
    }

    void LateUpdate()
    {
        if (!isMoving || !modelRoot)
        {
            return;
        }

        if (focusMoveDuration <= 0f)
        {
            ApplyMoveDestination(1f);
            isMoving = false;
            EnablePendingGrabBehaviours();
            return;
        }

        moveElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(moveElapsed / focusMoveDuration);
        t = t * t * (3f - 2f * t);
        ApplyMoveDestination(t);

        if (moveElapsed >= focusMoveDuration)
        {
            isMoving = false;
            EnablePendingGrabBehaviours();
        }
    }

    void Update()
    {
        if (!enableNumberKeyShortcuts)
        {
            return;
        }

        if (GetNumberKeyDown(0))
        {
            ShowAll();
            return;
        }

        for (int i = 0; i < parts.Length && i < 9; i++)
        {
            if (GetNumberKeyDown(i + 1))
            {
                FocusPart(i);
                return;
            }
        }
    }

    void OnGUI()
    {
        if (!showDesktopTestButtons || !showImguiFallback)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(desktopTestPanelPosition, desktopTestPanelSize), GUI.skin.box);
        GUILayout.Label("Anatomy Focus Test");

        if (GUILayout.Button("Show All / Reset (0)"))
        {
            ShowAll();
        }

        if (GUILayout.Button("Move To Focus Target"))
        {
            MoveToDefaultFocusTarget();
        }

        GUILayout.Space(8f);

        for (int i = 0; i < parts.Length; i++)
        {
            AnatomyPart part = parts[i];
            string label = part != null && !string.IsNullOrWhiteSpace(part.displayName)
                ? part.displayName
                : "Part " + (i + 1);

            if (i < 9)
            {
                label = (i + 1) + ": " + label;
            }

            if (GUILayout.Button(label))
            {
                FocusPart(i);
            }
        }

        GUILayout.EndArea();
    }

    void CreateDesktopTestCanvas()
    {
        if (!showDesktopTestButtons || !createRuntimeDesktopCanvas || runtimeDesktopCanvas)
        {
            return;
        }

        EnsureEventSystem();

        runtimeDesktopCanvas = new GameObject("Anatomy Desktop Test Canvas");

        Canvas canvas = runtimeDesktopCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = runtimeDesktopCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        runtimeDesktopCanvas.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(runtimeDesktopCanvas.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(desktopTestPanelPosition.x, -desktopTestPanelPosition.y);
        panelRect.sizeDelta = desktopTestPanelSize;

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.06f, 0.07f, 0.9f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(panel.transform, "Anatomy Focus Test");
        CreateDesktopButton(panel.transform, "Show All / Reset (0)", ShowAll);
        CreateDesktopButton(panel.transform, "Move To Focus Target", MoveToDefaultFocusTarget);

        for (int i = 0; i < parts.Length; i++)
        {
            int partIndex = i;
            AnatomyPart part = parts[i];
            string label = part != null && !string.IsNullOrWhiteSpace(part.displayName)
                ? part.displayName
                : "Part " + (i + 1);

            if (i < 9)
            {
                label = (i + 1) + ": " + label;
            }

            CreateDesktopButton(panel.transform, label, () => FocusPart(partIndex));
        }
    }

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>())
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("Desktop Test EventSystem");
        eventSystemObject.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    void CreateLabel(Transform parent, string text)
    {
        GameObject labelObject = new GameObject(text);
        labelObject.transform.SetParent(parent, false);

        Text label = labelObject.AddComponent<Text>();
        label.text = text;
        label.font = GetRuntimeFont();
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;

        LayoutElement layoutElement = labelObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 28f;
    }

    void CreateDesktopButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.24f, 0.32f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.26f, 0.34f, 0.45f, 1f);
        colors.pressedColor = new Color(0.12f, 0.17f, 0.24f, 1f);
        button.colors = colors;

        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 34f;

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 0f);
        textRect.offsetMax = new Vector2(-10f, 0f);

        Text buttonText = textObject.AddComponent<Text>();
        buttonText.text = label;
        buttonText.font = GetRuntimeFont();
        buttonText.fontSize = 15;
        buttonText.color = Color.white;
        buttonText.alignment = TextAnchor.MiddleLeft;
    }

    static Font GetRuntimeFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (!font)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    static bool GetNumberKeyDown(int digit)
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;

        if (keyboard == null)
        {
            return false;
        }

        switch (digit)
        {
            case 0:
                return keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame;
            case 1:
                return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
            case 2:
                return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
            case 3:
                return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
            case 4:
                return keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;
            case 5:
                return keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame;
            case 6:
                return keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame;
            case 7:
                return keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame;
            case 8:
                return keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame;
            case 9:
                return keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame;
            default:
                return false;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (digit < 0 || digit > 9)
        {
            return false;
        }

        return Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + digit))
            || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + digit));
#else
        return false;
#endif
    }

    [ContextMenu("Resolve Part Targets")]
    public void ResolveAllParts()
    {
        resolvedRenderers.Clear();
        resolvedGrabBehaviours.Clear();

        foreach (AnatomyPart part in parts)
        {
            if (part == null)
            {
                continue;
            }

            HashSet<Renderer> renderers = new HashSet<Renderer>();

            AddRenderersFromObjects(renderers, part.targetObjects);
            AddRenderers(renderers, part.targetRenderers);
            AddKeywordMatches(renderers, part.matchKeywords);

            resolvedRenderers[part] = new List<Renderer>(renderers);

            HashSet<Behaviour> grabBehaviours = new HashSet<Behaviour>();
            AddGrabBehaviours(grabBehaviours, part.grabBehaviours);
            AddGrabBehavioursFromObjects(grabBehaviours, part.grabObjects);

            if (autoFindGrabBehaviours)
            {
                AddGrabBehavioursForRenderers(grabBehaviours, renderers);
            }

            resolvedGrabBehaviours[part] = new List<Behaviour>(grabBehaviours);
        }
    }

    public void FocusPart(int index)
    {
        if (index < 0 || index >= parts.Length)
        {
            Debug.LogWarning($"Part index {index} is outside the configured anatomy parts.", this);
            return;
        }

        FocusPart(parts[index]);
    }

    public void FocusPart(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        foreach (AnatomyPart part in parts)
        {
            if (part != null && string.Equals(part.displayName, displayName, StringComparison.OrdinalIgnoreCase))
            {
                FocusPart(part);
                return;
            }
        }

        Debug.LogWarning($"No anatomy part named '{displayName}' is configured.", this);
    }

    public void ShowAll()
    {
        ClearPendingGrabEnable();

        foreach (Renderer modelRenderer in allRenderers)
        {
            if (modelRenderer)
            {
                modelRenderer.enabled = true;
            }
        }

        foreach (Collider modelCollider in allColliders)
        {
            if (modelCollider)
            {
                modelCollider.enabled = true;
            }
        }

        MoveModelHome();
        SetGrabBehavioursEnabled(!requireFocusForGrab || !disableGrabWhenShowingAll);
    }

    public void ClearFocus()
    {
        ShowAll();
    }

    [ContextMenu("Move To Default Focus Target")]
    public void MoveToDefaultFocusTarget()
    {
        MoveModelToTarget(defaultFocusTarget);
    }

    [ContextMenu("Return To Home Pose")]
    public void ReturnToHomePose()
    {
        MoveModelHome();
    }

    void FocusPart(AnatomyPart part)
    {
        if (part == null)
        {
            return;
        }

        if (!resolvedRenderers.TryGetValue(part, out List<Renderer> focusedRenderers))
        {
            ResolveAllParts();
            resolvedRenderers.TryGetValue(part, out focusedRenderers);
        }

        HashSet<Renderer> focused = new HashSet<Renderer>(focusedRenderers ?? new List<Renderer>());
        focused.UnionWith(alwaysVisibleRenderers);

        foreach (Renderer modelRenderer in allRenderers)
        {
            if (!modelRenderer)
            {
                continue;
            }

            modelRenderer.enabled = !hideUnfocusedRenderers || focused.Contains(modelRenderer);
        }

        MoveModelToFocusTarget(part, focused);
        UpdateGrabColliderForFocusedRenderers(focused);
        EnableFocusedGrabBehavioursWhenReady(part, focused);

        if (!disableUnfocusedColliders)
        {
            return;
        }

        HashSet<Collider> focusedColliders = ResolveCollidersForRenderers(focused);
        focusedColliders.UnionWith(alwaysVisibleColliders);

        foreach (Collider modelCollider in allColliders)
        {
            if (!modelCollider)
            {
                continue;
            }

            modelCollider.enabled = focusedColliders.Contains(modelCollider);
        }
    }

    void CaptureHomePose()
    {
        if (!modelRoot)
        {
            return;
        }

        homePosition = modelRoot.position;
        homeRotation = modelRoot.rotation;
        homeScale = modelRoot.localScale;
        homePoseCaptured = true;
    }

    void MoveModelToFocusTarget(AnatomyPart part, IEnumerable<Renderer> focusedRenderers)
    {
        if (!moveModelRootOnFocus)
        {
            return;
        }

        Transform focusTarget = part != null && part.focusTargetOverride
            ? part.focusTargetOverride
            : defaultFocusTarget;

        MoveModelToTarget(focusTarget, focusedRenderers);
    }

    void MoveModelToTarget(Transform focusTarget)
    {
        MoveModelToTarget(focusTarget, allRenderers);
    }

    void MoveModelToTarget(Transform focusTarget, IEnumerable<Renderer> focusedRenderers)
    {
        if (!moveModelRootOnFocus)
        {
            return;
        }

        if (!modelRoot)
        {
            Warn("Cannot move anatomy model because Model Root is not assigned.");
            return;
        }

        if (!focusTarget)
        {
            Warn("Cannot move anatomy model because no focus target is assigned. Set Default Focus Target or the part's Focus Target Override.");
            return;
        }

        Quaternion targetRotation = matchFocusTargetRotation
            ? focusTarget.rotation
            : homeRotation;

        Vector3 targetScale = matchFocusTargetScale
            ? focusTarget.localScale
            : homeScale;

        Vector3 targetPosition = focusTarget.position + focusTargetOffset;

        if (alignFocusedBoundsCenterToTarget)
        {
            Bounds focusedBounds;
            if (TryGetRendererBounds(focusedRenderers, out focusedBounds))
            {
                Vector3 localBoundsCenter = modelRoot.InverseTransformPoint(focusedBounds.center);
                Vector3 targetCenterOffset = targetRotation * Vector3.Scale(localBoundsCenter, targetScale);
                targetPosition -= targetCenterOffset;
            }
            else
            {
                Warn("Cannot align focused anatomy bounds because no focused renderers were resolved. Moving model pivot instead.");
            }
        }

        BeginMove(targetPosition, targetRotation, targetScale);
    }

    void MoveModelHome()
    {
        if (!moveModelRootOnFocus)
        {
            return;
        }

        if (!modelRoot)
        {
            Warn("Cannot return anatomy model because Model Root is not assigned.");
            return;
        }

        if (!homePoseCaptured)
        {
            Warn("Cannot return anatomy model because its home pose was not captured.");
            return;
        }

        BeginMove(homePosition, homeRotation, homeScale);
    }

    void BeginMove(Vector3 targetPosition, Quaternion targetRotation, Vector3 targetScale)
    {
        moveStartPosition = modelRoot.position;
        moveStartRotation = modelRoot.rotation;
        moveStartScale = modelRoot.localScale;
        moveEndPosition = targetPosition;
        moveEndRotation = targetRotation;
        moveEndScale = targetScale;
        moveElapsed = 0f;

        if (focusMoveDuration <= 0f)
        {
            ApplyMoveDestination(1f);
            isMoving = false;
            return;
        }

        isMoving = true;
    }

    void ApplyMoveDestination(float t)
    {
        modelRoot.position = Vector3.Lerp(moveStartPosition, moveEndPosition, t);
        modelRoot.rotation = Quaternion.Slerp(moveStartRotation, moveEndRotation, t);
        modelRoot.localScale = Vector3.Lerp(moveStartScale, moveEndScale, t);
    }

    void Warn(string message)
    {
        if (logFocusMovementWarnings)
        {
            Debug.LogWarning(message, this);
        }
    }

    void EnableFocusedGrabBehaviours(AnatomyPart part, IEnumerable<Renderer> focusedRenderers)
    {
        if (!requireFocusForGrab)
        {
            SetGrabBehavioursEnabled(true);
            return;
        }

        if (!resolvedGrabBehaviours.TryGetValue(part, out List<Behaviour> focusedGrabBehaviours))
        {
            ResolveAllParts();
            resolvedGrabBehaviours.TryGetValue(part, out focusedGrabBehaviours);
        }

        HashSet<Behaviour> focused = new HashSet<Behaviour>(focusedGrabBehaviours ?? new List<Behaviour>());

        if (autoFindGrabBehaviours && focused.Count == 0)
        {
            AddGrabBehavioursForRenderers(focused, focusedRenderers);
        }

        foreach (Behaviour grabBehaviour in focused)
        {
            if (grabBehaviour)
            {
                allGrabBehaviours.Add(grabBehaviour);
            }
        }

        focused.UnionWith(installedHandRenderStyleGrabBehaviours);

        foreach (Behaviour grabBehaviour in allGrabBehaviours)
        {
            if (!grabBehaviour)
            {
                continue;
            }

            grabBehaviour.enabled = focused.Contains(grabBehaviour);
        }
    }

    void EnableFocusedGrabBehavioursWhenReady(AnatomyPart part, IEnumerable<Renderer> focusedRenderers)
    {
        if (!isMoving)
        {
            ClearPendingGrabEnable();
            EnableFocusedGrabBehaviours(part, focusedRenderers);
            return;
        }

        SetGrabBehavioursEnabled(false);
        pendingGrabPart = part;
        pendingGrabRenderers = focusedRenderers != null
            ? new List<Renderer>(focusedRenderers)
            : new List<Renderer>();
        enableGrabWhenFocusMoveCompletes = true;
    }

    void EnablePendingGrabBehaviours()
    {
        if (!enableGrabWhenFocusMoveCompletes)
        {
            return;
        }

        AnatomyPart part = pendingGrabPart;
        List<Renderer> focusedRenderers = pendingGrabRenderers;
        ClearPendingGrabEnable();

        if (part != null)
        {
            EnableFocusedGrabBehaviours(part, focusedRenderers);
        }
    }

    void ClearPendingGrabEnable()
    {
        pendingGrabPart = null;
        pendingGrabRenderers = null;
        enableGrabWhenFocusMoveCompletes = false;
    }

    void InstallHandRenderStyleGrabSetupIfNeeded()
    {
        if (!installHandRenderStyleGrabSetup || !modelRoot)
        {
            return;
        }

        Type grabbableType = FindType("Oculus.Interaction.Grabbable");
        Type grabInteractableType = FindType("Oculus.Interaction.GrabInteractable");
        Type handGrabInteractableType = FindType("Oculus.Interaction.HandGrab.HandGrabInteractable");
        Type grabFreeTransformerType = FindType("Oculus.Interaction.GrabFreeTransformer");

        if (grabbableType == null || grabInteractableType == null || handGrabInteractableType == null || grabFreeTransformerType == null)
        {
            Warn("Could not install hand_render-style grab setup because one or more Oculus Interaction types were not found.");
            return;
        }

        Rigidbody rigidbody = modelRoot.GetComponent<Rigidbody>();
        if (!rigidbody)
        {
            rigidbody = modelRoot.gameObject.AddComponent<Rigidbody>();
        }

        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        BoxCollider collider = modelRoot.GetComponent<BoxCollider>();
        if (!collider)
        {
            collider = modelRoot.gameObject.AddComponent<BoxCollider>();
        }

        collider.isTrigger = true;
        UpdateGrabColliderForFocusedRenderers(allRenderers);

        Behaviour transformer = GetOrAddBehaviour(modelRoot.gameObject, grabFreeTransformerType);
        Behaviour grabbable = GetOrAddBehaviour(modelRoot.gameObject, grabbableType);

        ConfigureGrabbable(grabbable, transformer, rigidbody);

        Transform componentsRoot = modelRoot.Find("anatomy_focus_grab_components");
        if (!componentsRoot)
        {
            GameObject componentsObject = new GameObject("anatomy_focus_grab_components");
            componentsObject.transform.SetParent(modelRoot, false);
            componentsRoot = componentsObject.transform;
        }

        Behaviour grabInteractable = GetOrAddBehaviour(componentsRoot.gameObject, grabInteractableType);
        Behaviour handGrabInteractable = GetOrAddBehaviour(componentsRoot.gameObject, handGrabInteractableType);

        ConfigureGrabInteractable(grabInteractable, grabbable, rigidbody);
        ConfigureHandGrabInteractable(handGrabInteractable, grabbable, rigidbody);

        RegisterInstalledGrabBehaviour(grabbable);
        RegisterInstalledGrabBehaviour(transformer);
        RegisterInstalledGrabBehaviour(grabInteractable);
        RegisterInstalledGrabBehaviour(handGrabInteractable);

        SetGrabBehavioursEnabled(!requireFocusForGrab || !disableGrabWhenShowingAll);
    }

    void RegisterInstalledGrabBehaviour(Behaviour behaviour)
    {
        if (!behaviour)
        {
            return;
        }

        allGrabBehaviours.Add(behaviour);
        installedHandRenderStyleGrabBehaviours.Add(behaviour);
    }

    void ConfigureGrabInteractable(Behaviour grabInteractable, Behaviour grabbable, Rigidbody rigidbody)
    {
        InvokeOrSetField(grabInteractable, "InjectOptionalPointableElement", "_pointableElement", grabbable);
        InvokeOrSetField(grabInteractable, "InjectRigidbody", "_rigidbody", rigidbody);
        SetPrivateField(grabInteractable, "_useClosestPointAsGrabSource", false);
        SetPrivateField(grabInteractable, "_resetGrabOnGrabsUpdated", true);
    }

    void ConfigureHandGrabInteractable(Behaviour handGrabInteractable, Behaviour grabbable, Rigidbody rigidbody)
    {
        InvokeOrSetField(handGrabInteractable, "InjectOptionalPointableElement", "_pointableElement", grabbable);
        InvokeOrSetField(handGrabInteractable, "InjectRigidbody", "_rigidbody", rigidbody);
        SetPrivateField(handGrabInteractable, "_resetGrabOnGrabsUpdated", true);
        SetPrivateField(handGrabInteractable, "_slippiness", 0f);
        SetPrivateField(handGrabInteractable, "_supportedGrabTypes", 3);
        SetPrivateField(handGrabInteractable, "_handAligment", 1);
    }

    void ConfigureGrabbable(Behaviour grabbable, Behaviour transformer, Rigidbody rigidbody)
    {
        InvokeOrSetField(grabbable, "InjectOptionalOneGrabTransformer", "_oneGrabTransformer", transformer);
        InvokeOrSetField(grabbable, "InjectOptionalTwoGrabTransformer", "_twoGrabTransformer", transformer);
        InvokeOrSetField(grabbable, "InjectOptionalTargetTransform", "_targetTransform", modelRoot);
        InvokeOrSetField(grabbable, "InjectOptionalRigidbody", "_rigidbody", rigidbody);
        InvokeOrSetField(grabbable, "InjectOptionalKinematicWhileSelected", "_kinematicWhileSelected", true);
        InvokeOrSetField(grabbable, "InjectOptionalThrowWhenUnselected", "_throwWhenUnselected", false);
    }

    void UpdateGrabColliderForFocusedRenderers(IEnumerable<Renderer> focusedRenderers)
    {
        if (!useFocusedBoundsForGrabCollider || !modelRoot)
        {
            return;
        }

        BoxCollider collider = modelRoot.GetComponent<BoxCollider>();
        if (!collider)
        {
            return;
        }

        Bounds bounds;
        if (!TryGetRendererBounds(focusedRenderers, out bounds))
        {
            return;
        }

        Vector3 localMin = modelRoot.InverseTransformPoint(bounds.min);
        Vector3 localMax = modelRoot.InverseTransformPoint(bounds.max);
        Vector3 center = (localMin + localMax) * 0.5f;
        Vector3 size = new Vector3(
            Mathf.Abs(localMax.x - localMin.x),
            Mathf.Abs(localMax.y - localMin.y),
            Mathf.Abs(localMax.z - localMin.z));

        collider.center = center;
        collider.size = size;
    }

    Behaviour GetOrAddBehaviour(GameObject target, Type componentType)
    {
        Component existing = target.GetComponent(componentType);

        if (existing)
        {
            return existing as Behaviour;
        }

        return target.AddComponent(componentType) as Behaviour;
    }

    static Type FindType(string fullName)
    {
        Type type = Type.GetType(fullName);

        if (type != null)
        {
            return type;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(fullName);

            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null)
        {
            return;
        }

        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field != null)
        {
            if (value != null && field.FieldType.IsEnum && value is int)
            {
                value = Enum.ToObject(field.FieldType, value);
            }

            field.SetValue(target, value);
        }
    }

    static void InvokeOrSetField(object target, string methodName, string fieldName, object value)
    {
        if (target == null)
        {
            return;
        }

        if (InvokeMethod(target, methodName, value))
        {
            return;
        }

        SetPrivateField(target, fieldName, value);
    }

    static bool InvokeMethod(object target, string methodName, params object[] values)
    {
        MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (MethodInfo method in methods)
        {
            if (method.Name != methodName)
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != values.Length)
            {
                continue;
            }

            bool compatible = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (values[i] == null)
                {
                    continue;
                }

                Type parameterType = parameters[i].ParameterType;
                Type valueType = values[i].GetType();

                if (!parameterType.IsAssignableFrom(valueType)
                    && !(parameterType.IsEnum && values[i] is int))
                {
                    compatible = false;
                    break;
                }

                if (parameterType.IsEnum && values[i] is int intValue)
                {
                    values[i] = Enum.ToObject(parameterType, intValue);
                }
            }

            if (!compatible)
            {
                continue;
            }

            method.Invoke(target, values);
            return true;
        }

        return false;
    }

    void SetGrabBehavioursEnabled(bool enabled)
    {
        foreach (Behaviour grabBehaviour in allGrabBehaviours)
        {
            if (grabBehaviour)
            {
                grabBehaviour.enabled = enabled;
            }
        }
    }

    static bool TryGetRendererBounds(IEnumerable<Renderer> renderers, out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;

        if (renderers == null)
        {
            return false;
        }

        foreach (Renderer targetRenderer in renderers)
        {
            if (!targetRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(targetRenderer.bounds);
        }

        return hasBounds;
    }

    void CacheModelContent()
    {
        allRenderers.Clear();
        allColliders.Clear();
        alwaysVisibleRenderers.Clear();
        alwaysVisibleColliders.Clear();
        allGrabBehaviours.Clear();

        foreach (Renderer modelRenderer in modelRoot.GetComponentsInChildren<Renderer>(includeInactiveRenderers))
        {
            allRenderers.Add(modelRenderer);
        }

        foreach (Collider modelCollider in modelRoot.GetComponentsInChildren<Collider>(includeInactiveRenderers))
        {
            allColliders.Add(modelCollider);
        }

        CacheGrabBehaviours();

        AddRenderersFromObjects(alwaysVisibleRenderers, alwaysVisibleObjects);

        if (alwaysVisibleObjects == null)
        {
            return;
        }

        foreach (GameObject target in alwaysVisibleObjects)
        {
            if (!target)
            {
                continue;
            }

            foreach (Collider targetCollider in target.GetComponentsInChildren<Collider>(includeInactiveRenderers))
            {
                alwaysVisibleColliders.Add(targetCollider);
            }
        }
    }

    void CacheGrabBehaviours()
    {
        if (!autoFindGrabBehaviours || !modelRoot)
        {
            return;
        }

        foreach (Behaviour behaviour in modelRoot.GetComponentsInChildren<Behaviour>(includeInactiveRenderers))
        {
            if (IsGrabBehaviour(behaviour))
            {
                allGrabBehaviours.Add(behaviour);
            }
        }
    }

    void BindButtons()
    {
        for (int i = 0; i < parts.Length; i++)
        {
            int partIndex = i;
            Button button = parts[i] != null ? parts[i].button : null;

            if (!button)
            {
                continue;
            }

            button.onClick.AddListener(() => FocusPart(partIndex));
        }

        if (showAllButton)
        {
            showAllButton.onClick.RemoveListener(ShowAll);
            showAllButton.onClick.AddListener(ShowAll);
        }
    }

    void AddKeywordMatches(HashSet<Renderer> renderers, string[] keywords)
    {
        if (keywords == null || keywords.Length == 0)
        {
            return;
        }

        foreach (Renderer modelRenderer in allRenderers)
        {
            if (modelRenderer && MatchesAnyKeyword(modelRenderer, keywords))
            {
                renderers.Add(modelRenderer);
            }
        }
    }

    bool MatchesAnyKeyword(Renderer modelRenderer, string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            if (ContainsIgnoreCase(modelRenderer.name, keyword))
            {
                return true;
            }

            foreach (Material material in modelRenderer.sharedMaterials)
            {
                if (material && ContainsIgnoreCase(material.name, keyword))
                {
                    return true;
                }
            }
        }

        return false;
    }

    void AddRenderersFromObjects(HashSet<Renderer> renderers, GameObject[] targets)
    {
        if (targets == null)
        {
            return;
        }

        foreach (GameObject target in targets)
        {
            if (!target)
            {
                continue;
            }

            foreach (Renderer targetRenderer in target.GetComponentsInChildren<Renderer>(includeInactiveRenderers))
            {
                renderers.Add(targetRenderer);
            }
        }
    }

    static void AddRenderers(HashSet<Renderer> renderers, Renderer[] targets)
    {
        if (targets == null)
        {
            return;
        }

        foreach (Renderer target in targets)
        {
            if (target)
            {
                renderers.Add(target);
            }
        }
    }

    void AddGrabBehaviours(HashSet<Behaviour> grabBehaviours, Behaviour[] targets)
    {
        if (targets == null)
        {
            return;
        }

        foreach (Behaviour target in targets)
        {
            if (!target)
            {
                continue;
            }

            grabBehaviours.Add(target);
            allGrabBehaviours.Add(target);
        }
    }

    void AddGrabBehavioursFromObjects(HashSet<Behaviour> grabBehaviours, GameObject[] targets)
    {
        if (targets == null)
        {
            return;
        }

        foreach (GameObject target in targets)
        {
            if (!target)
            {
                continue;
            }

            foreach (Behaviour behaviour in target.GetComponentsInChildren<Behaviour>(includeInactiveRenderers))
            {
                if (!behaviour)
                {
                    continue;
                }

                grabBehaviours.Add(behaviour);
                allGrabBehaviours.Add(behaviour);
            }
        }
    }

    void AddGrabBehavioursForRenderers(HashSet<Behaviour> grabBehaviours, IEnumerable<Renderer> renderers)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer targetRenderer in renderers)
        {
            if (!targetRenderer)
            {
                continue;
            }

            AddMatchingGrabBehavioursInChildren(grabBehaviours, targetRenderer.transform);

            Transform current = targetRenderer.transform;

            while (current)
            {
                AddMatchingGrabBehavioursOnTransform(grabBehaviours, current);

                if (current == modelRoot)
                {
                    break;
                }

                current = current.parent;
            }
        }
    }

    void AddMatchingGrabBehavioursInChildren(HashSet<Behaviour> grabBehaviours, Transform target)
    {
        foreach (Behaviour behaviour in target.GetComponentsInChildren<Behaviour>(includeInactiveRenderers))
        {
            if (IsGrabBehaviour(behaviour))
            {
                grabBehaviours.Add(behaviour);
                allGrabBehaviours.Add(behaviour);
            }
        }
    }

    void AddMatchingGrabBehavioursOnTransform(HashSet<Behaviour> grabBehaviours, Transform target)
    {
        foreach (Behaviour behaviour in target.GetComponents<Behaviour>())
        {
            if (IsGrabBehaviour(behaviour))
            {
                grabBehaviours.Add(behaviour);
                allGrabBehaviours.Add(behaviour);
            }
        }
    }

    bool IsGrabBehaviour(Behaviour behaviour)
    {
        if (!behaviour || behaviour == this || grabBehaviourTypeNameKeywords == null)
        {
            return false;
        }

        Type behaviourType = behaviour.GetType();

        foreach (string keyword in grabBehaviourTypeNameKeywords)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            if (ContainsIgnoreCase(behaviourType.Name, keyword) || ContainsIgnoreCase(behaviourType.FullName, keyword))
            {
                return true;
            }
        }

        return false;
    }

    static HashSet<Collider> ResolveCollidersForRenderers(HashSet<Renderer> renderers)
    {
        HashSet<Collider> colliders = new HashSet<Collider>();

        foreach (Renderer modelRenderer in renderers)
        {
            if (!modelRenderer)
            {
                continue;
            }

            foreach (Collider targetCollider in modelRenderer.GetComponentsInChildren<Collider>(true))
            {
                colliders.Add(targetCollider);
            }

            foreach (Collider targetCollider in modelRenderer.GetComponentsInParent<Collider>(true))
            {
                colliders.Add(targetCollider);
            }
        }

        return colliders;
    }

    static bool ContainsIgnoreCase(string value, string fragment)
    {
        return !string.IsNullOrEmpty(value)
            && value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

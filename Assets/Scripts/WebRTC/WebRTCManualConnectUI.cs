using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class WebRTCManualConnectUI : MonoBehaviour
{
    private const string SceneName = "OTF_Simple";

    // Meta / QDS dropdown names from your hierarchy
    private const string DropdownContainerName = "DropDown1LineTextOnly";
    private const string DropdownListName = "DropDownList";
    private const string DropdownOptionPrefix = "DropDownListButton_IconAndLabel2Lines_Toggle";

    private const int DefaultPort = 8080;
    private const int DiscoveryPort = 8081;

    private const string DiscoveryMessage = "XERT_WEBRTC_DISCOVER";
    private const string DiscoveryResponseType = "XERT_WEBRTC_SERVER";

    [Header("Optional manual references")]
    [SerializeField] private HologramSender sender;
    [SerializeField] private WebRTCSignalingClient signaling;
    [SerializeField] private TMP_Text statusText;

    private Transform dropdownRoot;
    private Transform discoveryListRoot;
    private TMP_Text dropdownHeaderText;

    private readonly List<Transform> dropdownOptionRows = new();
    private readonly List<DiscoveryServer> discoveredServerList = new();
    private readonly Dictionary<string, DiscoveryServer> discoveredServers = new();

    private readonly Dictionary<Button, UnityAction> buttonActions = new();
    private readonly Dictionary<Toggle, UnityAction<bool>> toggleActions = new();

    private Coroutine discoveryCoroutine;
    private bool hasAutoConnected = false;

    [Serializable]
    private class DiscoveryServer
    {
        public string type;
        public string name;
        public string wsUrl;
        public int port;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneName)
            return;

        if (FindFirstObjectByType<WebRTCManualConnectUI>() != null)
            return;

        GameObject bridgeObject = new GameObject("WebRTC Manual Connect UI");
        bridgeObject.AddComponent<WebRTCManualConnectUI>();
    }

    private void Awake()
    {
        sender = sender != null ? sender : FindFirstObjectByType<HologramSender>();
        signaling = signaling != null ? signaling : FindFirstObjectByType<WebRTCSignalingClient>();

        if (sender != null)
            sender.waitForManualStart = true;

        EnsureEventSystem();

        ResolveMetaDropdown();
        StartDiscovery();

        SetStatus("Searching for WebRTC server...");
    }

    private void OnDestroy()
    {
        if (discoveryCoroutine != null)
            StopCoroutine(discoveryCoroutine);

        foreach (var pair in buttonActions)
        {
            if (pair.Key != null)
                pair.Key.onClick.RemoveListener(pair.Value);
        }

        foreach (var pair in toggleActions)
        {
            if (pair.Key != null)
                pair.Key.onValueChanged.RemoveListener(pair.Value);
        }

        buttonActions.Clear();
        toggleActions.Clear();

        if (signaling != null)
            signaling.Connected -= OnSignalingConnected;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("WebRTC UI EventSystem");
        eventSystemObject.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private void ResolveMetaDropdown()
    {
        GameObject dropdownObject = FindSceneGameObjectByName(DropdownContainerName);

        if (dropdownObject == null)
        {
            Debug.LogWarning("Could not find Meta dropdown object: " + DropdownContainerName);
            return;
        }

        dropdownRoot = dropdownObject.transform;

        Transform list = FindChildRecursive(dropdownRoot, DropdownListName);
        if (list == null)
        {
            Debug.LogWarning("Could not find child object: " + DropdownListName);
            return;
        }

        discoveryListRoot = list;
        dropdownHeaderText = FindHeaderText(dropdownRoot);

        dropdownOptionRows.Clear();

        foreach (Transform child in discoveryListRoot)
        {
            if (child.name.StartsWith(DropdownOptionPrefix, StringComparison.OrdinalIgnoreCase))
                dropdownOptionRows.Add(child);
        }

        Debug.Log("[WebRTCManualConnectUI] Meta dropdown rows found: " + dropdownOptionRows.Count);

        // Important:
        // Do NOT disable Meta/QDS rows.
        // The prefab can break if its row objects are disabled.
        for (int i = 0; i < dropdownOptionRows.Count; i++)
        {
            dropdownOptionRows[i].gameObject.SetActive(true);

            TMP_Text text = dropdownOptionRows[i].GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = "";

            Button button = dropdownOptionRows[i].GetComponentInChildren<Button>(true);
            if (button != null)
                button.interactable = false;

            Toggle toggle = dropdownOptionRows[i].GetComponentInChildren<Toggle>(true);
            if (toggle != null)
                toggle.interactable = false;
        }

        if (dropdownHeaderText != null)
            dropdownHeaderText.text = "Searching...";
    }

    private void StartDiscovery()
    {
        if (discoveryCoroutine != null)
            StopCoroutine(discoveryCoroutine);

        discoveryCoroutine = StartCoroutine(DiscoveryLoop());
    }

    private IEnumerator DiscoveryLoop()
    {
        UdpClient udp = null;

        try
        {
            udp = new UdpClient();
            udp.EnableBroadcast = true;
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.ReceiveTimeout = 100;
        }
        catch (Exception ex)
        {
            Debug.LogError("[WebRTCManualConnectUI] UDP discovery setup failed: " + ex.Message);
            SetStatus("UDP discovery failed.");
        }

        if (udp == null)
            yield break;

        IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
        byte[] discoverBytes = Encoding.UTF8.GetBytes(DiscoveryMessage);

        while (true)
        {
            try
            {
                udp.Send(discoverBytes, discoverBytes.Length, broadcastEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WebRTCManualConnectUI] UDP discovery send failed: " + ex.Message);
            }

            float endTime = Time.realtimeSinceStartup + 1.0f;

            while (Time.realtimeSinceStartup < endTime)
            {
                try
                {
                    while (udp.Available > 0)
                    {
                        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                        byte[] data = udp.Receive(ref remote);
                        string message = Encoding.UTF8.GetString(data);

                        HandleDiscoveryMessage(message, remote);
                    }
                }
                catch (SocketException)
                {
                    // Ignore timeout / no packet cases.
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[WebRTCManualConnectUI] UDP discovery receive failed: " + ex.Message);
                }

                yield return null;
            }

            yield return new WaitForSeconds(1.0f);
        }
    }

    private void HandleDiscoveryMessage(string message, IPEndPoint remote)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        DiscoveryServer server = null;

        try
        {
            server = JsonUtility.FromJson<DiscoveryServer>(message);
        }
        catch
        {
            // Ignore JSON parsing error and try fallback.
        }

        if (server == null || string.IsNullOrEmpty(server.type))
        {
            if (!message.Contains(DiscoveryResponseType))
                return;

            server = new DiscoveryServer
            {
                type = DiscoveryResponseType,
                name = remote.Address.ToString(),
                wsUrl = "ws://" + remote.Address + ":" + DefaultPort,
                port = DefaultPort
            };
        }

        if (server.type != DiscoveryResponseType)
            return;

        if (string.IsNullOrEmpty(server.name))
            server.name = remote.Address.ToString();

        if (server.port <= 0)
            server.port = DefaultPort;

        if (string.IsNullOrEmpty(server.wsUrl))
            server.wsUrl = "ws://" + remote.Address + ":" + server.port;

        string key = server.wsUrl;

        if (discoveredServers.ContainsKey(key))
            discoveredServers[key] = server;
        else
            discoveredServers.Add(key, server);

        RebuildDiscoveredServerList();
        UpdateMetaDropdownList();
        TryAutoConnect();
    }

    private void RebuildDiscoveredServerList()
    {
        discoveredServerList.Clear();

        foreach (var pair in discoveredServers)
            discoveredServerList.Add(pair.Value);

        discoveredServerList.Sort((a, b) =>
        {
            string aName = string.IsNullOrEmpty(a.name) ? a.wsUrl : a.name;
            string bName = string.IsNullOrEmpty(b.name) ? b.wsUrl : b.name;
            return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void UpdateMetaDropdownList()
    {
        if (dropdownRoot != null)
            dropdownRoot.gameObject.SetActive(true);

        if (discoveryListRoot != null)
            discoveryListRoot.gameObject.SetActive(true);

        if (discoveredServerList.Count == 0)
        {
            if (dropdownHeaderText != null)
                dropdownHeaderText.text = "No server found";

            SetStatus("No WebRTC server found.");
            return;
        }

        if (dropdownHeaderText != null)
            dropdownHeaderText.text = GetServerLabel(discoveredServerList[0]);

        for (int i = 0; i < dropdownOptionRows.Count; i++)
        {
            Transform row = dropdownOptionRows[i];

            // Keep Meta/QDS row alive.
            row.gameObject.SetActive(true);

            bool hasServer = i < discoveredServerList.Count;

            TMP_Text rowText = row.GetComponentInChildren<TMP_Text>(true);
            Button button = row.GetComponentInChildren<Button>(true);
            Toggle toggle = row.GetComponentInChildren<Toggle>(true);

            if (!hasServer)
            {
                if (rowText != null)
                    rowText.text = "";

                if (button != null)
                    button.interactable = false;

                if (toggle != null)
                    toggle.interactable = false;

                continue;
            }

            DiscoveryServer server = discoveredServerList[i];

            if (rowText != null)
                rowText.text = GetServerLabel(server);

            int capturedIndex = i;

            if (button != null)
            {
                button.interactable = true;

                if (buttonActions.TryGetValue(button, out UnityAction oldAction))
                    button.onClick.RemoveListener(oldAction);

                UnityAction newAction = () => SelectDiscoveredServer(capturedIndex);
                button.onClick.AddListener(newAction);
                buttonActions[button] = newAction;
            }

            if (toggle != null)
            {
                toggle.interactable = true;

                if (toggleActions.TryGetValue(toggle, out UnityAction<bool> oldAction))
                    toggle.onValueChanged.RemoveListener(oldAction);

                UnityAction<bool> newAction = isOn =>
                {
                    if (isOn)
                        SelectDiscoveredServer(capturedIndex);
                };

                toggle.onValueChanged.AddListener(newAction);
                toggleActions[toggle] = newAction;
            }
        }

        SetStatus("Found " + discoveredServerList.Count + " WebRTC server(s).");
    }

    private void TryAutoConnect()
    {
        if (hasAutoConnected)
            return;

        if (discoveredServerList.Count == 0)
            return;

        hasAutoConnected = true;

        DiscoveryServer firstServer = discoveredServerList[0];

        string url = GetServerUrl(firstServer);

        if (dropdownHeaderText != null)
            dropdownHeaderText.text = GetServerLabel(firstServer);

        ConnectToServer(url);
    }

    private void SelectDiscoveredServer(int index)
    {
        if (index < 0 || index >= discoveredServerList.Count)
            return;

        DiscoveryServer server = discoveredServerList[index];
        string url = GetServerUrl(server);

        if (dropdownHeaderText != null)
            dropdownHeaderText.text = GetServerLabel(server);

        ConnectToServer(url);
    }

    private void ConnectToServer(string signalingUrl)
    {
        if (sender == null)
        {
            SetStatus("Missing HologramSender.");
            return;
        }

        if (signaling == null)
        {
            SetStatus("Missing WebRTCSignalingClient.");
            return;
        }

        signaling.Connected -= OnSignalingConnected;
        signaling.Connected += OnSignalingConnected;

        signaling.signalingUrl = signalingUrl;

        SetStatus("Connecting to " + signalingUrl);

        sender.BeginStreaming();
    }

    private void OnSignalingConnected()
    {
        SetStatus("Connected.");
    }

    private string GetServerUrl(DiscoveryServer server)
    {
        if (server == null)
            return string.Empty;

        if (!string.IsNullOrEmpty(server.wsUrl))
            return server.wsUrl;

        if (server.port <= 0)
            server.port = DefaultPort;

        return "ws://" + server.name + ":" + server.port;
    }

    private string GetServerLabel(DiscoveryServer server)
    {
        if (server == null)
            return "Unknown server";

        if (!string.IsNullOrEmpty(server.name))
            return server.name;

        if (!string.IsNullOrEmpty(server.wsUrl))
            return server.wsUrl;

        return "WebRTC Server";
    }

    private void SetStatus(string message)
    {
        Debug.Log("[WebRTCManualConnectUI] " + message);

        if (statusText != null)
            statusText.text = message;
    }

    private static GameObject FindSceneGameObjectByName(string objectName)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj == null)
                continue;

            if (obj.name != objectName)
                continue;

            if (!obj.scene.IsValid())
                continue;

            return obj;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform result = FindChildRecursive(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static TMP_Text FindHeaderText(Transform dropdownRoot)
    {
        if (dropdownRoot == null)
            return null;

        TMP_Text[] texts = dropdownRoot.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text == null)
                continue;

            if (IsChildOfNamedParent(text.transform, DropdownListName))
                continue;

            string lowerName = text.gameObject.name.ToLowerInvariant();

            if (lowerName.Contains("header") ||
                lowerName.Contains("label") ||
                lowerName.Contains("text"))
            {
                return text;
            }
        }

        foreach (TMP_Text text in texts)
        {
            if (text != null && !IsChildOfNamedParent(text.transform, DropdownListName))
                return text;
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    private static bool IsChildOfNamedParent(Transform child, string parentName)
    {
        Transform current = child;

        while (current != null)
        {
            if (current.name == parentName)
                return true;

            current = current.parent;
        }

        return false;
    }
}
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class WebRTCSignalingClient : MonoBehaviour
{
    [Serializable]
    public class SignalMessage
    {
        public string type;
        public string role;
        public string sdp;
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
    }

    [Header("Signaling")]
    public string signalingUrl = "ws://127.0.0.1:8080";
    public string role = "sender";

    public event Action Connected;
    public event Action<string> OfferReceived;
    public event Action<string> AnswerReceived;
    public event Action<string, string, int> IceReceived;

    private ClientWebSocket websocket;
    private CancellationTokenSource cts;

    private readonly ConcurrentQueue<string> inboundMessages = new ConcurrentQueue<string>();
    private bool connectedEventPending = false;
    private bool connectInProgress = false;

    public bool IsConnected => websocket != null && websocket.State == WebSocketState.Open;
    public bool IsConnecting => connectInProgress;

    public async void Connect()
    {
        if (connectInProgress || IsConnected)
            return;

        try
        {
            connectInProgress = true;

            cts?.Cancel();
            cts?.Dispose();
            websocket?.Dispose();

            cts = new CancellationTokenSource();
            websocket = new ClientWebSocket();

            Uri uri = new Uri(signalingUrl);
            await websocket.ConnectAsync(uri, cts.Token);

            if (websocket.State == WebSocketState.Open)
            {
                Debug.Log($"Connected to signaling server: {signalingUrl}");

                connectedEventPending = true;

                await SendAsync(new SignalMessage
                {
                    type = "register",
                    role = role
                });

                _ = ReceiveLoop(websocket, cts.Token);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("WebSocket connect failed: " + ex);
        }
        finally
        {
            connectInProgress = false;
        }
    }

    public async void Send(SignalMessage msg)
    {
        await SendAsync(msg);
    }

    private async Task SendAsync(SignalMessage msg)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
            return;

        try
        {
            string json = JsonUtility.ToJson(msg);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            ArraySegment<byte> buffer = new ArraySegment<byte>(bytes);

            if (msg.type == "offer")
                Debug.Log($"Sending signal: type=offer, bytes={bytes.Length}, sdpLength={(msg.sdp != null ? msg.sdp.Length : 0)}");
            else if (msg.type == "ice")
                Debug.Log($"Sending signal: type=ice, bytes={bytes.Length}, candidateLength={(msg.candidate != null ? msg.candidate.Length : 0)}");
            else
                Debug.Log($"Sending signal: type={msg.type}, bytes={bytes.Length}, role={msg.role}");

            await websocket.SendAsync(buffer, WebSocketMessageType.Text, true, cts.Token);
        }
        catch (Exception ex)
        {
            Debug.LogError("WebSocket send failed: " + ex);
        }
    }

    private async Task ReceiveLoop(ClientWebSocket activeSocket, CancellationToken token)
    {
        byte[] buffer = new byte[16384];

        while (activeSocket != null && activeSocket.State == WebSocketState.Open)
        {
            try
            {
                ArraySegment<byte> segment = new ArraySegment<byte>(buffer);
                WebSocketReceiveResult result = await activeSocket.ReceiveAsync(segment, token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("WebSocket closed by server");
                    await activeSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                int count = result.Count;

                while (!result.EndOfMessage)
                {
                    if (count >= buffer.Length)
                    {
                        Debug.LogError("Incoming WebSocket message too large.");
                        return;
                    }

                    segment = new ArraySegment<byte>(buffer, count, buffer.Length - count);
                    result = await activeSocket.ReceiveAsync(segment, token);
                    count += result.Count;
                }

                string json = Encoding.UTF8.GetString(buffer, 0, count);
                inboundMessages.Enqueue(json);
            }
            catch (Exception ex)
            {
                Debug.LogError("WebSocket receive failed: " + ex);
                break;
            }
        }
    }

    private void Update()
    {
        if (connectedEventPending)
        {
            connectedEventPending = false;
            Connected?.Invoke();
        }

        while (inboundMessages.TryDequeue(out string json))
        {
            Debug.Log("Signal received: " + json);
            HandleIncomingMessage(json);
        }
    }

    private void HandleIncomingMessage(string json)
    {
        SignalMessage msg = JsonUtility.FromJson<SignalMessage>(json);
        if (msg == null) return;

        switch (msg.type)
        {
            case "offer":
                OfferReceived?.Invoke(msg.sdp);
                break;

            case "answer":
                AnswerReceived?.Invoke(msg.sdp);
                break;

            case "ice":
                IceReceived?.Invoke(msg.candidate, msg.sdpMid, msg.sdpMLineIndex);
                break;

            default:
                Debug.LogWarning("Unknown signaling message type: " + msg.type);
                break;
        }
    }

    private async void OnApplicationQuit()
    {
        try
        {
            if (cts != null)
                cts.Cancel();

            if (websocket != null &&
                (websocket.State == WebSocketState.Open || websocket.State == WebSocketState.CloseReceived))
            {
                await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Quit", CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("WebSocket close warning: " + ex.Message);
        }
    }
}

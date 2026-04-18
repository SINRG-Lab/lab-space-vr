using System;
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
    public string signalingUrl = "ws://192.168.1.100:8080";
    public string role = "sender";

    public event Action Connected;
    public event Action<string> OfferReceived;
    public event Action<string> AnswerReceived;
    public event Action<string, string, int> IceReceived;

    private ClientWebSocket websocket;
    private CancellationTokenSource cts;
    private bool isConnected;

    public async void Connect()
    {
        try
        {
            cts = new CancellationTokenSource();
            websocket = new ClientWebSocket();

            Uri uri = new Uri(signalingUrl);
            await websocket.ConnectAsync(uri, cts.Token);

            isConnected = websocket.State == WebSocketState.Open;

            if (isConnected)
            {
                Debug.Log($"Connected to signaling server: {signalingUrl}");

                await SendAsync(new SignalMessage
                {
                    type = "register",
                    role = role
                });

                _ = ReceiveLoop();
                Connected?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("WebSocket connect failed: " + ex.Message);
        }
    }

    public async void Send(SignalMessage msg)
    {
        await SendAsync(msg);
    }

    public async Task SendAsync(SignalMessage msg)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning($"WebSocket send skipped. Socket not open for message type '{msg?.type ?? "null"}'.");
            return;
        }

        try
        {
            string json = JsonUtility.ToJson(msg);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            ArraySegment<byte> buffer = new ArraySegment<byte>(bytes);

            Debug.Log("Sending signal: " + DescribeMessage(msg, bytes.Length));
            await websocket.SendAsync(buffer, WebSocketMessageType.Text, true, cts.Token);
        }
        catch (Exception ex)
        {
            Debug.LogError("WebSocket send failed: " + ex.Message);
        }
    }

    private async Task ReceiveLoop()
    {
        byte[] buffer = new byte[8192];

        while (websocket != null && websocket.State == WebSocketState.Open)
        {
            try
            {
                ArraySegment<byte> segment = new ArraySegment<byte>(buffer);
                WebSocketReceiveResult result = await websocket.ReceiveAsync(segment, cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("WebSocket closed by server");
                    await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cts.Token);
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
                    result = await websocket.ReceiveAsync(segment, cts.Token);
                    count += result.Count;
                }

                string json = Encoding.UTF8.GetString(buffer, 0, count);
                Debug.Log("Signal received: " + json);

                HandleIncomingMessage(json);
            }
            catch (Exception ex)
            {
                Debug.LogError("WebSocket receive failed: " + ex.Message);
                break;
            }
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
        }
    }

    private string DescribeMessage(SignalMessage msg, int payloadBytes)
    {
        if (msg == null)
            return $"<null> ({payloadBytes} bytes)";

        string summary = $"type={msg.type}, bytes={payloadBytes}";

        if (!string.IsNullOrEmpty(msg.role))
            summary += $", role={msg.role}";

        if (!string.IsNullOrEmpty(msg.sdp))
            summary += $", sdpLength={msg.sdp.Length}";

        if (!string.IsNullOrEmpty(msg.candidate))
            summary += $", candidateLength={msg.candidate.Length}";

        return summary;
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

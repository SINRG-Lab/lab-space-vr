using System.Collections;
using UnityEngine;
using Unity.WebRTC;
using UnityEngine.SceneManagement;

public class HologramSender : MonoBehaviour
{
    public HologramFinalOutput finalOutput;
    public WebRTCSignalingClient signaling;
    public string stunServer = "stun:stun.l.google.com:19302";
    public bool waitForManualStart = false;

    [Header("Stream Settings")]
    public int streamWidth = 960;
    public int streamHeight = 960;
    public ulong maxBitrate = 4_000_000;
    public uint maxFramerate = 24;

    private RTCPeerConnection peerConnection;
    private VideoStreamTrack videoTrack;
    private RTCRtpSender videoSender;
    private Coroutine webRtcUpdateCoroutine;
    private bool initialized;

    void Start()
    {
        if (!waitForManualStart && SceneManager.GetActiveScene().name != "OTF_Simple")
            BeginStreaming();
    }

    public void BeginStreaming()
    {
        if (signaling == null)
        {
            Debug.LogError("HologramSender requires a WebRTCSignalingClient.");
            return;
        }

        if (!initialized)
        {
            initialized = true;

            signaling.role = "sender";
            signaling.Connected += OnSignalingConnected;
            signaling.AnswerReceived += OnAnswerReceived;
            signaling.IceReceived += OnIceReceived;

            webRtcUpdateCoroutine = StartCoroutine(WebRTC.Update());
        }

        signaling.Connect();
    }

    void OnSignalingConnected()
    {
        if (peerConnection != null)
            return;

        StartCoroutine(CreateAndSendOffer());
    }

    RTCPeerConnection CreatePeerConnection()
    {
        RTCConfiguration config = default;
        config.iceServers = new[]
        {
            new RTCIceServer { urls = new[] { stunServer } }
        };

        var pc = new RTCPeerConnection(ref config);

        pc.OnIceCandidate = candidate =>
        {
            if (candidate == null) return;

            signaling.Send(new WebRTCSignalingClient.SignalMessage
            {
                type = "ice",
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = candidate.SdpMLineIndex ?? 0
            });
        };

        return pc;
    }

    IEnumerator CreateAndSendOffer()
    {
        if (finalOutput == null || finalOutput.compositorCamera == null)
        {
            Debug.LogError("Missing compositor camera.");
            yield break;
        }

        peerConnection = CreatePeerConnection();

        videoTrack = finalOutput.compositorCamera.CaptureStreamTrack(streamWidth, streamHeight);
        videoSender = peerConnection.AddTrack(videoTrack);
        ApplyVideoSenderParameters();

        var offerOp = peerConnection.CreateOffer();
        yield return offerOp;

        if (offerOp.IsError)
        {
            Debug.LogError("CreateOffer failed: " + offerOp.Error.message);
            yield break;
        }

        RTCSessionDescription offer = offerOp.Desc;

        var setLocalOp = peerConnection.SetLocalDescription(ref offer);
        yield return setLocalOp;

        if (setLocalOp.IsError)
        {
            Debug.LogError("SetLocalDescription failed: " + setLocalOp.Error.message);
            yield break;
        }

        signaling.Send(new WebRTCSignalingClient.SignalMessage
        {
            type = "offer",
            sdp = offer.sdp
        });
    }

    private void ApplyVideoSenderParameters()
    {
        if (videoSender == null)
        {
            Debug.LogWarning("videoSender is null.");
            return;
        }

        var parameters = videoSender.GetParameters();

        if (parameters.encodings == null || parameters.encodings.Length == 0)
        {
            Debug.LogWarning("No sender encodings found.");
            return;
        }

        for (int i = 0; i < parameters.encodings.Length; i++)
        {
            parameters.encodings[i].maxBitrate = maxBitrate;
            parameters.encodings[i].maxFramerate = maxFramerate;
            parameters.encodings[i].scaleResolutionDownBy = 1.0;
        }

        var error = videoSender.SetParameters(parameters);

        if (error.errorType != RTCErrorType.None)
            Debug.LogError("SetParameters failed: " + error.message);
        else
            Debug.Log("Sender video parameters applied.");
    }

    void OnAnswerReceived(string sdp)
    {
        StartCoroutine(SetRemoteAnswer(sdp));
    }

    IEnumerator SetRemoteAnswer(string sdp)
    {
        if (peerConnection == null) yield break;

        RTCSessionDescription answer = new RTCSessionDescription
        {
            type = RTCSdpType.Answer,
            sdp = sdp
        };

        var op = peerConnection.SetRemoteDescription(ref answer);
        yield return op;

        if (op.IsError)
            Debug.LogError("SetRemoteDescription failed: " + op.Error.message);
    }

    void OnIceReceived(string candidate, string sdpMid, int sdpMLineIndex)
    {
        if (peerConnection == null) return;

        var init = new RTCIceCandidateInit
        {
            candidate = candidate,
            sdpMid = sdpMid,
            sdpMLineIndex = sdpMLineIndex
        };

        peerConnection.AddIceCandidate(new RTCIceCandidate(init));
    }

    void OnDestroy()
    {
        if (signaling != null)
        {
            signaling.Connected -= OnSignalingConnected;
            signaling.AnswerReceived -= OnAnswerReceived;
            signaling.IceReceived -= OnIceReceived;
        }

        videoTrack?.Dispose();

        if (peerConnection != null)
        {
            peerConnection.Close();
            peerConnection.Dispose();
        }

        if (webRtcUpdateCoroutine != null)
            StopCoroutine(webRtcUpdateCoroutine);
    }
}

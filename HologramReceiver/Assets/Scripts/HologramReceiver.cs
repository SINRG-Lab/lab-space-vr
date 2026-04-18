using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;
using System.Collections;

public class HologramReceiver : MonoBehaviour
{
    [Header("References")]
    public WebRTCSignalingClient signaling;
    public RawImage outputImage;

    [Header("ICE")]
    public string stunServer = "stun:stun.l.google.com:19302";

    private RTCPeerConnection peerConnection;
    private VideoStreamTrack remoteVideoTrack;
    private Coroutine webRtcUpdateCoroutine;

    private readonly List<RTCIceCandidateInit> pendingIceCandidates = new();
    private bool remoteDescriptionApplied;

    private void Start()
    {
        if (signaling == null)
        {
            Debug.LogError("HologramReceiver: signaling reference is missing.");
            return;
        }

        if (outputImage == null)
        {
            Debug.LogError("HologramReceiver: outputImage reference is missing.");
            return;
        }

        signaling.role = "receiver";
        signaling.Connected += OnSignalingConnected;
        signaling.OfferReceived += OnOfferReceived;
        signaling.IceReceived += OnIceReceived;

        webRtcUpdateCoroutine = StartCoroutine(WebRTC.Update());
        signaling.Connect();

        outputImage.texture = null;
        outputImage.color = Color.black;
    }

    private void OnSignalingConnected()
    {
        Debug.Log("Receiver connected to signaling server.");
    }

    private RTCPeerConnection CreatePeerConnection()
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

        pc.OnConnectionStateChange = state =>
        {
            Debug.Log("Receiver PeerConnection state: " + state);
        };

        pc.OnTrack = e =>
        {
            Debug.Log("Receiver track received: " + e.Track.Kind);

            if (e.Track is VideoStreamTrack videoTrack)
            {
                remoteVideoTrack = videoTrack;
                remoteVideoTrack.OnVideoReceived += texture =>
                {
                    Debug.Log($"Receiver first video frame: {texture.width}x{texture.height}");
                    outputImage.texture = texture;
                    outputImage.color = Color.white;
                    ResizeRawImageToAspect(texture);
                };
            }
        };

        return pc;
    }

    private void OnOfferReceived(string sdp)
    {
        Debug.Log("Receiver offer received. SDP length: " + (sdp?.Length ?? 0));
        StartCoroutine(HandleOffer(sdp));
    }

    private IEnumerator HandleOffer(string sdp)
    {
        if (peerConnection != null)
        {
            if (remoteVideoTrack != null)
            {
                remoteVideoTrack.Dispose();
                remoteVideoTrack = null;
            }

            peerConnection.Close();
            peerConnection.Dispose();
            peerConnection = null;
        }

        pendingIceCandidates.Clear();
        remoteDescriptionApplied = false;

        peerConnection = CreatePeerConnection();

        RTCSessionDescription offer = new RTCSessionDescription
        {
            type = RTCSdpType.Offer,
            sdp = sdp
        };

        var setRemoteOp = peerConnection.SetRemoteDescription(ref offer);
        yield return setRemoteOp;

        if (setRemoteOp.IsError)
        {
            Debug.LogError("Receiver SetRemoteDescription(offer) failed: " + setRemoteOp.Error.message);
            yield break;
        }

        remoteDescriptionApplied = true;
        Debug.Log("Receiver remote offer applied.");

        foreach (var candidate in pendingIceCandidates)
        {
            peerConnection.AddIceCandidate(new RTCIceCandidate(candidate));
        }
        pendingIceCandidates.Clear();

        var answerOp = peerConnection.CreateAnswer();
        yield return answerOp;

        if (answerOp.IsError)
        {
            Debug.LogError("Receiver CreateAnswer failed: " + answerOp.Error.message);
            yield break;
        }

        RTCSessionDescription answer = answerOp.Desc;
        Debug.Log("Receiver answer created. SDP length: " + answer.sdp.Length);

        var setLocalOp = peerConnection.SetLocalDescription(ref answer);
        yield return setLocalOp;

        if (setLocalOp.IsError)
        {
            Debug.LogError("Receiver SetLocalDescription(answer) failed: " + setLocalOp.Error.message);
            yield break;
        }

        signaling.Send(new WebRTCSignalingClient.SignalMessage
        {
            type = "answer",
            sdp = answer.sdp
        });

        Debug.Log("Receiver answer sent.");
    }

    private void OnIceReceived(string candidate, string sdpMid, int sdpMLineIndex)
    {
        RTCIceCandidateInit init = new RTCIceCandidateInit
        {
            candidate = candidate,
            sdpMid = sdpMid,
            sdpMLineIndex = sdpMLineIndex
        };

        if (peerConnection == null || !remoteDescriptionApplied)
        {
            pendingIceCandidates.Add(init);
            Debug.Log("Receiver queued ICE candidate.");
            return;
        }

        peerConnection.AddIceCandidate(new RTCIceCandidate(init));
    }

    private void ResizeRawImageToAspect(Texture texture)
    {
        if (texture == null || outputImage == null) return;

        RectTransform rt = outputImage.rectTransform;
        RectTransform parent = rt.parent as RectTransform;
        if (parent == null) return;

        float texAspect = (float)texture.width / texture.height;
        float parentW = parent.rect.width;
        float parentH = parent.rect.height;

        if (parentW <= 0 || parentH <= 0) return;

        float parentAspect = parentW / parentH;

        if (texAspect > parentAspect)
        {
            float height = parentW / texAspect;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, parentW);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
        else
        {
            float width = parentH * texAspect;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, parentH);
        }

        rt.anchoredPosition = Vector2.zero;
    }
}

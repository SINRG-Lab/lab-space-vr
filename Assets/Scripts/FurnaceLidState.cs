using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class FurnaceLidState : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform lidTransform;
    [SerializeField] private FurnaceProcedureManager procedureManager;

    [Header("Lid Poses")]
    [SerializeField] private Vector3 openLocalEuler = new(-40f, 90f, -90f);
    [SerializeField] private Vector3 closedLocalEuler = new(-90f, 90f, -90f);
    [SerializeField, Min(0.1f)] private float closeToleranceDegrees = 3f;
    [SerializeField, Min(0.1f)] private float reopenToleranceDegrees = 7f;

    [Header("Events")]
    [SerializeField] private UnityEvent onClosed = new();
    [SerializeField] private UnityEvent onOpened = new();

    private bool initialized;
    private bool isClosed;

    public bool IsClosed => isClosed;

    private void Awake()
    {
        if (!lidTransform)
        {
            lidTransform = transform;
        }

    }

    private void Start()
    {
        if (!procedureManager)
        {
            procedureManager = FurnaceProcedureManager.Instance;
        }

        RefreshState(playFeedback: false);
    }

    private void LateUpdate()
    {
        RefreshState(playFeedback: true);
    }

    private void RefreshState(bool playFeedback)
    {
        Quaternion closedRotation = Quaternion.Euler(closedLocalEuler);
        float angleFromClosed = Quaternion.Angle(lidTransform.localRotation, closedRotation);
        float threshold = isClosed ? reopenToleranceDegrees : closeToleranceDegrees;
        bool nextClosed = angleFromClosed <= threshold;

        if (initialized && nextClosed == isClosed)
        {
            RepublishAfterProcedureReset();
            return;
        }

        bool wasInitialized = initialized;
        initialized = true;
        isClosed = nextClosed;
        PublishState();

        if (!wasInitialized)
        {
            return;
        }

        if (isClosed)
        {
            onClosed.Invoke();
            if (playFeedback)
            {
                FurnaceInteractionFeedback.PlayActionConfirmed();
            }
        }
        else
        {
            onOpened.Invoke();
        }
    }

    private void PublishState()
    {
        if (procedureManager)
        {
            procedureManager.SetFurnaceClosed(isClosed);
        }
    }

    public void SetClosedForDevelopment(bool closed)
    {
        lidTransform.localRotation = closed
            ? Quaternion.Euler(closedLocalEuler)
            : Quaternion.Euler(openLocalEuler);
        Physics.SyncTransforms();
        RefreshState(playFeedback: false);
    }

    public void ResetForDevelopment()
    {
        lidTransform.localRotation = Quaternion.Euler(openLocalEuler);
        Physics.SyncTransforms();
        RefreshState(playFeedback: false);
    }

    private void RepublishAfterProcedureReset()
    {
        if (procedureManager &&
            procedureManager.GetGate(FurnaceProcedureManager.Gate.FurnaceClosed) != isClosed)
        {
            PublishState();
        }
    }

    private void OnValidate()
    {
        reopenToleranceDegrees = Mathf.Max(reopenToleranceDegrees, closeToleranceDegrees);
    }
}

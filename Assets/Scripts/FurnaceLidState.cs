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
    [SerializeField, Min(0.1f)] private float openToleranceDegrees = 3f;
    [SerializeField, Min(0.1f)] private float leaveOpenToleranceDegrees = 7f;

    [Header("Events")]
    [SerializeField] private UnityEvent onClosed = new();
    [SerializeField] private UnityEvent onOpened = new();

    private bool initialized;
    private bool isClosed;
    private bool isOpen;

    public bool IsClosed => isClosed;
    public bool IsOpen => isOpen;

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
        Quaternion openRotation = Quaternion.Euler(openLocalEuler);
        float angleFromClosed = Quaternion.Angle(lidTransform.localRotation, closedRotation);
        float angleFromOpen = Quaternion.Angle(lidTransform.localRotation, openRotation);
        float closedThreshold = isClosed ? reopenToleranceDegrees : closeToleranceDegrees;
        float openThreshold = isOpen ? leaveOpenToleranceDegrees : openToleranceDegrees;
        bool nextClosed = angleFromClosed <= closedThreshold;
        bool nextOpen = angleFromOpen <= openThreshold;

        if (initialized && nextClosed == isClosed && nextOpen == isOpen)
        {
            RepublishAfterProcedureReset();
            return;
        }

        bool wasInitialized = initialized;
        bool wasClosed = isClosed;
        bool wasOpen = isOpen;
        initialized = true;
        isClosed = nextClosed;
        isOpen = nextOpen;
        PublishState();

        if (!wasInitialized)
        {
            return;
        }

        if (!wasClosed && isClosed)
        {
            onClosed.Invoke();
            if (playFeedback)
            {
                FurnaceInteractionFeedback.PlayActionConfirmed();
            }
        }
        if (!wasOpen && isOpen)
        {
            onOpened.Invoke();
            if (playFeedback)
            {
                FurnaceInteractionFeedback.PlayActionConfirmed();
            }
        }
    }

    private void PublishState()
    {
        if (procedureManager)
        {
            procedureManager.SetFurnaceClosed(isClosed);
            procedureManager.SetFurnaceOpen(isOpen);
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
            procedureManager.SetFurnaceClosed(isClosed);
        }

        if (procedureManager &&
            procedureManager.GetGate(FurnaceProcedureManager.Gate.FurnaceOpen) != isOpen)
        {
            procedureManager.SetFurnaceOpen(isOpen);
        }
    }

    private void OnValidate()
    {
        reopenToleranceDegrees = Mathf.Max(reopenToleranceDegrees, closeToleranceDegrees);
        leaveOpenToleranceDegrees = Mathf.Max(leaveOpenToleranceDegrees, openToleranceDegrees);
    }
}

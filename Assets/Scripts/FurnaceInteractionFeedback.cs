using UnityEngine;

[DisallowMultipleComponent]
public sealed class FurnaceInteractionFeedback : MonoBehaviour
{
    public static FurnaceInteractionFeedback Instance { get; private set; }

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip targetAvailableClip;
    [SerializeField] private AudioClip actionConfirmedClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.45f;
    [SerializeField, Min(0f)] private float targetCueCooldown = 0.08f;

    private float nextTargetCueTime;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogWarning(
                $"Only one {nameof(FurnaceInteractionFeedback)} should be active. Disabling {name}.",
                this);
            enabled = false;
            return;
        }

        Instance = this;

        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    public static void PlayTargetAvailable()
    {
        if (Instance)
        {
            Instance.PlayTargetCue();
        }
    }

    public static void PlayActionConfirmed()
    {
        if (Instance)
        {
            Instance.PlayClip(Instance.actionConfirmedClip, 1f);
        }
    }

    public static void PlayProcedureComplete()
    {
        if (Instance)
        {
            Instance.PlayClip(Instance.actionConfirmedClip, 1.12f);
        }
    }

    private void PlayTargetCue()
    {
        if (Time.unscaledTime < nextTargetCueTime)
        {
            return;
        }

        nextTargetCueTime = Time.unscaledTime + targetCueCooldown;
        PlayClip(targetAvailableClip, 1f);
    }

    private void PlayClip(AudioClip clip, float pitch)
    {
        if (!audioSource || !clip || volume <= 0f)
        {
            return;
        }

        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

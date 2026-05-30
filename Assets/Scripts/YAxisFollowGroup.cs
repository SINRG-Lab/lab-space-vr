using UnityEngine;

public class YAxisFollowGroup : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Transform[] followers;
    [SerializeField] bool preserveInitialOffset = true;
    public float additionalYOffset = 0f;
    [SerializeField] float followSpeed = 0f;

    float[] yOffsets;

    void Awake()
    {
        CacheOffsets();
    }

    void LateUpdate()
    {
        if (!target)
        {
            return;
        }

        if (yOffsets == null || yOffsets.Length != followers.Length)
        {
            CacheOffsets();
        }

        for (int i = 0; i < followers.Length; i++)
        {
            Transform follower = followers[i];
            if (!follower)
            {
                continue;
            }

            Vector3 position = follower.position;
            float targetY = target.position.y + yOffsets[i] + additionalYOffset;
            position.y = followSpeed > 0f
                ? Mathf.Lerp(position.y, targetY, Time.deltaTime * followSpeed)
                : targetY;
            follower.position = position;
        }
    }

    void CacheOffsets()
    {
        if (followers == null)
        {
            followers = new Transform[0];
        }

        yOffsets = new float[followers.Length];

        for (int i = 0; i < followers.Length; i++)
        {
            yOffsets[i] = preserveInitialOffset && target && followers[i]
                ? followers[i].position.y - target.position.y
                : 0f;
        }
    }
}

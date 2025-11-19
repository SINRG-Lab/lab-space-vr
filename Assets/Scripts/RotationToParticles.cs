using UnityEngine;

public class RotationToParticles : MonoBehaviour
{
    [Header("Grabbable / Rotating Object")]
    public Transform target;

    [Header("Particle System")]
    public ParticleSystem particleSystem;

    [Header("Rotation → Value Mapping")]
    public float minAngle = 0f;       // angle where flow is 0
    public float maxAngle = 90f;      // angle where flow is 5000

    [Header("Speed Mapping")]
    public float minSpeed = 0f;       // speed at minAngle
    public float maxSpeed = 5000f;      // particles/sec at maxAngle
    public Axis axis = Axis.Z;

    [Header("Emission Mapping")]
    public float minEmission = 0f;       // speed at minAngle
    public float maxEmission = 10f;  

    public enum Axis { X, Y, Z }

    void Update()
    {
        if (target == null || particleSystem == null) return;

        // 1. Read angle on chosen axis
        float rawAngle = GetLocalAxisAngle(target, axis);

        // 2. Normalize to -180..180 so negatives work nicely
        float angle = NormalizeAngle(rawAngle);

        // 3. Angle → 0..1
        float t = Mathf.InverseLerp(minAngle, maxAngle, angle);
        t = Mathf.Clamp01(t);

        // 4. 0..1 → minSpeed..maxSpeed (0–5000)
        float speed = Mathf.Lerp(minSpeed, maxSpeed, t);
        float rate = Mathf.Lerp(minEmission, maxEmission, t);

        // 5. Apply to particle start speed
        var main = particleSystem.main;
        main.startSpeed = speed;   // implicit cast to MinMaxCurve
        var emission = particleSystem.emission;
        emission.rateOverTime = rate;
    }

    float GetLocalAxisAngle(Transform t, Axis axis)
    {
        Vector3 e = t.localEulerAngles;
        switch (axis)
        {
            case Axis.X: return e.x;
            case Axis.Y: return e.y;
            case Axis.Z: return e.z;
        }
        return 0f;
    }

    float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }

}

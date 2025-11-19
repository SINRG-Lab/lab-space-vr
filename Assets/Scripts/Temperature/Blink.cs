using UnityEngine;

public class Blink : MonoBehaviour
{
    [Header("Source (IncreaseTemperature script)")]
    public IncreaseTemperature increaseTemperature;

    [Header("Blinking Indicator")]
    public Renderer[] blinkRenderers;        // the OTHER object that should blink
    public Material blinkMaterialA;       // e.g. normal / idle material
    public Material blinkMaterialB;       // e.g. hot / warning material
    public float blinkInterval = 0.2f;

    private float blinkTimer = 0f;
    private bool blinkState = false; 

    // Update is called once per frame
    void Update()
    {
        bool isHeating = increaseTemperature.isIncreasingTemperature;

         if (isHeating)
        {
            // Count time while heating
            blinkTimer += Time.deltaTime; // * GlobalSimSpeed.Multiplier; // if you use one

            if (blinkTimer >= blinkInterval)
            {
                blinkTimer = 0f;
                blinkState = !blinkState;

                Material m = blinkState ? blinkMaterialA : blinkMaterialB;

                foreach (var r in blinkRenderers)
                {
                    if (r != null && m != null)
                        r.material = m;
                }
            }
        }
        else
        {
            foreach (var r in blinkRenderers)
            {
                if (r != null)
                    r.material = blinkMaterialA;
            }
        }
    }
}

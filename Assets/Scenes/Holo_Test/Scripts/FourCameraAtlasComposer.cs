using UnityEngine;

public class FourCameraDiagonalComposer : MonoBehaviour
{
    public RenderTexture rightRT;
    public RenderTexture leftRT;
    public RenderTexture topRT;
    public RenderTexture bottomRT;

    public RenderTexture outputRT;   // the final “hologram template” frame
    public Material composeMat;      // uses Hidden/FourViewDiagonal

    void LateUpdate()
    {
        if (!composeMat || !outputRT) return;

        composeMat.SetTexture("_RightTex",  rightRT);
        composeMat.SetTexture("_LeftTex",   leftRT);
        composeMat.SetTexture("_TopTex",    topRT);
        composeMat.SetTexture("_BottomTex", bottomRT);

        Graphics.Blit(null, outputRT, composeMat);
    }
}
using UnityEngine;

public class PyramidCompose : MonoBehaviour
{
    public RenderTexture RT_0_Top;
    public RenderTexture RT_1_Left;
    public RenderTexture RT_2_Bottom;
    public RenderTexture RT_3_Right;

    public RenderTexture outputRT;
    public Material mat; // uses Hidden/FourViewDiagonalInsetFull_Oriented

    void LateUpdate()
    {
        mat.SetTexture("_TopTex", RT_0_Top);
        mat.SetTexture("_LeftTex", RT_1_Left);
        mat.SetTexture("_BottomTex", RT_2_Bottom);
        mat.SetTexture("_RightTex", RT_3_Right);

        // Rot index: 0=0°, 1=90°, 2=180°, 3=270° (clockwise)
        mat.SetFloat("_TopRot", 2);    mat.SetFloat("_TopFlipX", 1);    mat.SetFloat("_TopFlipY", 0);
        mat.SetFloat("_BottomRot", 0); mat.SetFloat("_BottomFlipX", 1); mat.SetFloat("_BottomFlipY", 0);
        mat.SetFloat("_LeftRot", 1);   mat.SetFloat("_LeftFlipX", 1);   mat.SetFloat("_LeftFlipY", 0);
        mat.SetFloat("_RightRot", 3);  mat.SetFloat("_RightFlipX", 1);  mat.SetFloat("_RightFlipY", 0);

        Graphics.Blit(null, outputRT, mat);
    }
}
using UnityEngine;

public class HandLayerController : MonoBehaviour
{
    [Header("Layer Objects")]
    [SerializeField] GameObject skinLayer;
    [SerializeField] GameObject muscleLayer;
    [SerializeField] GameObject boneLayer;
    [SerializeField] GameObject nerveLayer;

    [Header("Initial Visibility")]
    [SerializeField] bool skinVisible = true;
    [SerializeField] bool muscleVisible = true;
    [SerializeField] bool boneVisible = true;
    [SerializeField] bool nerveVisible = true;

    void Reset()
    {
        AutoAssignMissingLayers();
    }

    void Awake()
    {
        AutoAssignMissingLayers();
        ApplyVisibility();
    }

    public void ToggleSkin()
    {
        SetSkinVisible(!IsVisible(skinLayer));
    }

    public void ToggleMuscle()
    {
        SetMuscleVisible(!IsVisible(muscleLayer));
    }

    public void ToggleBone()
    {
        SetBoneVisible(!IsVisible(boneLayer));
    }

    public void ToggleNerve()
    {
        SetNerveVisible(!IsVisible(nerveLayer));
    }

    public void SetSkinVisible(bool visible)
    {
        skinVisible = visible;
        SetLayerVisible(skinLayer, visible);
    }

    public void SetMuscleVisible(bool visible)
    {
        muscleVisible = visible;
        SetLayerVisible(muscleLayer, visible);
    }

    public void SetBoneVisible(bool visible)
    {
        boneVisible = visible;
        SetLayerVisible(boneLayer, visible);
    }

    public void SetNerveVisible(bool visible)
    {
        nerveVisible = visible;
        SetLayerVisible(nerveLayer, visible);
    }

    public void SetAllVisible(bool visible)
    {
        SetLayerVisibility(visible, visible, visible, visible);
    }

    public void ShowSkinOnly()
    {
        SetLayerVisibility(true, false, false, false);
    }

    public void ShowMuscleOnly()
    {
        SetLayerVisibility(false, true, false, false);
    }

    public void ShowBoneOnly()
    {
        SetLayerVisibility(false, false, true, false);
    }

    public void ShowNerveOnly()
    {
        SetLayerVisibility(false, false, false, true);
    }

    public void ShowMuscleAndBone()
    {
        SetLayerVisibility(false, true, true, false);
    }

    public void SetLayerVisibility(bool showSkin, bool showMuscle, bool showBone)
    {
        SetLayerVisibility(showSkin, showMuscle, showBone, nerveVisible);
    }

    public void SetLayerVisibility(bool showSkin, bool showMuscle, bool showBone, bool showNerve)
    {
        skinVisible = showSkin;
        muscleVisible = showMuscle;
        boneVisible = showBone;
        nerveVisible = showNerve;
        ApplyVisibility();
    }

    void ApplyVisibility()
    {
        SetLayerVisible(skinLayer, skinVisible);
        SetLayerVisible(muscleLayer, muscleVisible);
        SetLayerVisible(boneLayer, boneVisible);
        SetLayerVisible(nerveLayer, nerveVisible);
    }

    void AutoAssignMissingLayers()
    {
        skinLayer = skinLayer ? skinLayer : FindChildByName("rand_0_skin");
        muscleLayer = muscleLayer ? muscleLayer : FindChildByName("rand_0_muscle");
        boneLayer = boneLayer ? boneLayer : FindChildByName("rand_0_bone");
        nerveLayer = nerveLayer ? nerveLayer : FindChildByName("rand_0_nerve");
    }

    GameObject FindChildByName(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    static void SetLayerVisible(GameObject layer, bool visible)
    {
        if (layer)
        {
            layer.SetActive(visible);
        }
    }

    static bool IsVisible(GameObject layer)
    {
        return layer && layer.activeSelf;
    }
}

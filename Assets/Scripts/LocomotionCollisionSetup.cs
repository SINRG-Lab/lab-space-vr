using UnityEngine;
using MetaCharacterController = Oculus.Interaction.Locomotion.CharacterController;

[DefaultExecutionOrder(-1000)]
public sealed class LocomotionCollisionSetup : MonoBehaviour
{
    [SerializeField] private MetaCharacterController characterController;
    [SerializeField] private string walkableLayerName = "Walkable";

    private void Awake()
    {
        if (!characterController)
        {
            characterController = FindFirstObjectByType<MetaCharacterController>(
                FindObjectsInactive.Include);
        }

        if (!characterController)
        {
            Debug.LogWarning(
                "Locomotion setup could not find Meta's character controller.",
                this);
            return;
        }

        int walkableLayer = LayerMask.NameToLayer(walkableLayerName);
        if (walkableLayer < 0)
        {
            Debug.LogWarning(
                $"Locomotion setup could not find layer '{walkableLayerName}'.",
                this);
            return;
        }

        characterController.LayerMask |= 1 << walkableLayer;
    }
}

using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private PlayerInventory playerInventory;

    [Header("UI Controllers")]
    [SerializeField] private MaskMeterController maskMeterController;
    [SerializeField] private CheeseUIController cheeseUIController;

    void Start()
    {
        if (playerInventory == null)
            playerInventory = FindFirstObjectByType<PlayerInventory>();

        if (maskMeterController == null)
            maskMeterController = FindFirstObjectByType<MaskMeterController>();

        if (cheeseUIController == null)
            cheeseUIController = FindFirstObjectByType<CheeseUIController>();

        UpdateCheeseUI();
    }

    public void OnCheeseCollected()
    {
        UpdateCheeseUI();
    }

    private void UpdateCheeseUI()
    {
        if (cheeseUIController != null && playerInventory != null)
        {
            cheeseUIController.UpdateCheeseUI(
                playerInventory.smallCheeseCount,
                playerInventory.HasBigCheese
            );
        }
    }

    public float GetMaskDurability()
    {
        if (maskMeterController != null)
            return maskMeterController.GetMaskDurabilityNormalized();

        return 1f;
    }

    public bool IsMaskDepleted()
    {
        if (maskMeterController != null)
            return maskMeterController.IsMaskDepleted();

        return false;
    }
}

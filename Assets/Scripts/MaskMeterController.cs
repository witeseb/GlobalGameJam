using UnityEngine;
using UnityEngine.UI;

public class MaskMeterController : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private PlayerMask playerMask;

    [Header("UI Elements")]
    [SerializeField] private Image heatBarFill;

    [Header("Mask Settings")]
    [SerializeField] private float maxMaskDurability = 100f;
    [SerializeField] private float maskDrainRate = 10f;
    [SerializeField] private float maskRegenRate = 15f;

    private float currentMaskDurability;

    public float CurrentMaskDurability => currentMaskDurability;
    public float MaxMaskDurability => maxMaskDurability;

    void Start()
    {
        if (playerMask == null)
            playerMask = FindFirstObjectByType<PlayerMask>();

        currentMaskDurability = maxMaskDurability;
        UpdateMaskBar();
    }

    void Update()
    {
        UpdateMaskLogic();
        UpdateMaskBar();
    }

    private void UpdateMaskLogic()
    {
        if (playerMask == null)
            return;

        if (playerMask.IsMasked)
        {
            currentMaskDurability -= maskDrainRate * Time.deltaTime;
        }
        else
        {
            currentMaskDurability += maskRegenRate * Time.deltaTime;
        }

        currentMaskDurability = Mathf.Clamp(currentMaskDurability, 0f, maxMaskDurability);
    }

    private void UpdateMaskBar()
    {
        if (heatBarFill != null)
        {
            heatBarFill.fillAmount = currentMaskDurability / maxMaskDurability;
        }
    }

    public float GetMaskDurabilityNormalized()
    {
        return currentMaskDurability / maxMaskDurability;
    }

    public bool IsMaskDepleted()
    {
        return currentMaskDurability <= 0f;
    }
}

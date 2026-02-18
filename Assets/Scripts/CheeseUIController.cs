using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CheeseUIController : MonoBehaviour
{
    [Header("Small Cheese Counter")]
    [SerializeField] private TextMeshProUGUI smallCheeseCountText;
    [SerializeField] private Image smallCheeseIcon;

    [Header("Big Cheese Icons")]
    [SerializeField] private Image bigCheeseNotCollectedIcon;
    [SerializeField] private Image bigCheeseCollectedIcon;

    void Start()
    {
        UpdateCheeseUI(0, false);
    }

    public void UpdateCheeseUI(int smallCheeseCount, bool hasBigCheese)
    {
        if (smallCheeseCountText != null)
        {
            smallCheeseCountText.text = smallCheeseCount.ToString();
        }

        if (bigCheeseNotCollectedIcon != null)
        {
            bigCheeseNotCollectedIcon.gameObject.SetActive(!hasBigCheese);
        }

        if (bigCheeseCollectedIcon != null)
        {
            bigCheeseCollectedIcon.gameObject.SetActive(hasBigCheese);
        }
    }
}


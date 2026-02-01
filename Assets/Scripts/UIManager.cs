using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
	public static UIManager Instance { get; private set; }

	[Header("Mask Heat UI")]
	[SerializeField] private Image maskHeatFill;

	[Header("Small Cheese UI")]
	[SerializeField] private Transform smallCheeseContainer;

	[SerializeField]
	private SmallCheeseIcon[] smallCheeseIcons;

	[Header("Big Cheese Icons")]
	[SerializeField] private GameObject noBigCheeseIcon;
	[SerializeField] private GameObject hasBigCheeseIcon;

	private PlayerMask playerMask;
	private PlayerInventory playerInventory;

	private void Awake()
	{
		if (Instance != null)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
	}

	private void Start()
	{
		playerMask = FindFirstObjectByType<PlayerMask>();
		playerInventory = FindFirstObjectByType<PlayerInventory>();

		if (playerMask == null)
			Debug.LogError("[UIManager] PlayerMask not found!");

		if (playerInventory == null)
			Debug.LogError("[UIManager] PlayerInventory not found!");

		InitSmallCheeseIcons();
		StartCoroutine(DelayedClamp());
	}

	private void Update()
	{
		UpdateMaskHeat();
		UpdateCheeseUI();
		UpdateSmallCheeseUI();
	}

	// ================= MASK =================
	private void UpdateMaskHeat()
	{
		if (maskHeatFill == null || playerMask == null)
			return;

		maskHeatFill.fillAmount = playerMask.GetHeatNormalized();

		maskHeatFill.enabled =
			playerMask.IsMasked ||
			playerMask.GetHeatNormalized() > 0.01f;
	}

	// ================= CHEESE =================
	private void UpdateCheeseUI()
	{
		if (playerInventory == null)
			return;

		bool hasBig = playerInventory.HasBigCheese;

		if (noBigCheeseIcon != null)
			noBigCheeseIcon.SetActive(!hasBig);

		if (hasBigCheeseIcon != null)
			hasBigCheeseIcon.SetActive(hasBig);
	}

	void UpdateSmallCheeseUI()
	{
		if (playerInventory == null || smallCheeseIcons == null)
			return;

		int collected = playerInventory.smallCheeseCount;

		for (int i = 0; i < smallCheeseIcons.Length; i++)
		{
			smallCheeseIcons[i].SetCollected(i < collected);
		}
	}

	void InitSmallCheeseIcons()
	{
		if (smallCheeseContainer == null)
		{
			Debug.LogError("[UIManager] SmallCheeseContainer NOT assigned!");
			return;
		}

		smallCheeseIcons =
			smallCheeseContainer.GetComponentsInChildren<SmallCheeseIcon>(true);

		Debug.Log($"[UIManager] Small cheese UI slots: {smallCheeseIcons.Length}");
	}

	void ClampSmallCheeseUI()
	{
		Debug.Log("[UIManager] ClampSmallCheeseUI called");

		if (smallCheeseIcons == null)
		{
			Debug.LogError("[UIManager] smallCheeseIcons is NULL (InitSmallCheeseIcons not called?)");
			return;
		}

		Debug.Log($"[UIManager] smallCheeseIcons length = {smallCheeseIcons.Length}");

		CheeseSpawner spawner = FindFirstObjectByType<CheeseSpawner>();

		if (spawner == null)
		{
			Debug.LogWarning("[UIManager] CheeseSpawner NOT FOUND in scene");
			return;
		}

		Debug.Log("[UIManager] CheeseSpawner found: " + spawner.name);
		Debug.Log($"[UIManager] Spawner MaxSmallCheese = {spawner.MaxSmallCheese}");

		if (spawner.MaxSmallCheese <= 0)
		{
			Debug.LogWarning(
				"[UIManager] MaxSmallCheese is 0 or less. " +
				"This usually means ClampSmallCheeseUI ran BEFORE SpawnSmallCheese()."
			);
		}

		int max = Mathf.Clamp(spawner.MaxSmallCheese, 0, smallCheeseIcons.Length);

		Debug.Log($"[UIManager] Clamping UI to {max} slots");

		for (int i = 0; i < smallCheeseIcons.Length; i++)
		{
			bool active = i < max;
			smallCheeseIcons[i].gameObject.SetActive(active);

			Debug.Log(
				$"[UIManager] Slot {i} → {(active ? "ACTIVE" : "DISABLED")}"
			);
		}
	}

	IEnumerator DelayedClamp()
	{
		yield return null; // wait one frame
		ClampSmallCheeseUI();
	}
}

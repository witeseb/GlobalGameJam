using UnityEngine;

public class PlayerMask : MonoBehaviour
{
	public bool IsMasked { get; private set; }

	[Header("Visuals")]
	public GameObject unmaskedModel;
	public GameObject maskedModel;

	[Header("Mask Heat Settings")]
	public float maxHeat = 5f;
	public float heatIncreaseRate = 1f;
	public float heatDecreaseRate = 1.5f;
	public float overheatCooldown = 2f;

	public float CurrentHeat { get; private set; }

	private bool isOverheated;
	private float cooldownTimer;

	private void Update()
	{
		if (IsMasked)
		{
			IncreaseHeat();
		}
		else
		{
			CoolDown();
		}

		HandleOverheatCooldown();
	}

	public void ToggleMask()
	{
		SetMask(!IsMasked);
	}

	private void SetMask(bool value)
	{
		IsMasked = value;
		Debug.Log($"Mask state: {value}");
		Debug.Log($"ToggleMask called by {gameObject.name}");

		unmaskedModel.SetActive(!IsMasked);
		maskedModel.SetActive(IsMasked);
	}

	void IncreaseHeat()
	{
		if (isOverheated)
			return;

		CurrentHeat += heatIncreaseRate * Time.deltaTime;

		if (CurrentHeat >= maxHeat)
		{
			Overheat();
		}
	}

	void CoolDown()
	{
		CurrentHeat = Mathf.Max(0f, CurrentHeat - heatDecreaseRate * Time.deltaTime);
	}

	void Overheat()
	{
		Debug.Log("MASK OVERHEATED!");

		isOverheated = true;
		cooldownTimer = overheatCooldown;

		if (IsMasked)
			SetMask(false);
	}

	void HandleOverheatCooldown()
	{
		if (!isOverheated)
			return;

		cooldownTimer -= Time.deltaTime;

		if (cooldownTimer <= 0f)
		{
			isOverheated = false;
			Debug.Log("Mask cooled down, usable again");
		}
	}

	public float GetHeatNormalized()
	{
		return Mathf.Clamp01(CurrentHeat / maxHeat);
	}

}

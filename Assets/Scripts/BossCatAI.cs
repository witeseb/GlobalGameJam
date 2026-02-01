using UnityEngine;

public class BossCatAI : MonoBehaviour
{
	[Header("Detection")]
	public VisionSensor vision;
	public float suspicionIncreaseRate = 1.2f;
	public float suspicionDecreaseRate = 0.6f;
	public float maxSuspicion = 3f;

	[Header("UI")]
	public GameObject suspicionMeterUI;

	private Transform player;
	private PlayerMask playerMask;

	private float suspicion;
	private bool playerWasMaskedLastFrame;

	void Start()
	{
		var playerObj = FindFirstObjectByType<PlayerController>();
		player = playerObj.transform;
		playerMask = playerObj.GetComponent<PlayerMask>();

		suspicion = 0f;

		if (suspicionMeterUI != null)
			suspicionMeterUI.SetActive(false);
	}

	void Update()
	{
		HandleDetection();
		playerWasMaskedLastFrame = playerMask.IsMasked;
	}

	void HandleDetection()
	{
		if (!vision.CanSeeTarget())
		{
			DecreaseSuspicion();
			return;
		}

		// Mask OFF -> instant chase
		if (!playerMask.IsMasked)
		{
			TriggerChase();
			return;
		}

		// Mask toggled in vision -> instant chase
		if (!playerWasMaskedLastFrame && playerMask.IsMasked)
		{
			TriggerChase();
			return;
		}

		// Masked but lingering -> suspicion
		IncreaseSuspicion();
	}

	void IncreaseSuspicion()
	{
		suspicion += Time.deltaTime * suspicionIncreaseRate;
		suspicion = Mathf.Clamp(suspicion, 0f, maxSuspicion);

		if (suspicionMeterUI != null)
			suspicionMeterUI.SetActive(true);

		if (suspicion >= maxSuspicion)
		{
			TriggerChase();
		}
	}

	void DecreaseSuspicion()
	{
		suspicion -= Time.deltaTime * suspicionDecreaseRate;
		suspicion = Mathf.Max(suspicion, 0f);

		if (suspicion <= 0f && suspicionMeterUI != null)
			suspicionMeterUI.SetActive(false);
	}

	void TriggerChase()
	{
		Debug.Log("BOSS CAT: CHASE!");

		// TODO:
		// - Lock hallway
		// - Speed boost
		// - Game Over trigger
	}
}

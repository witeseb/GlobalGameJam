using UnityEngine;

public class RegularCatAI : MonoBehaviour
{
	public enum RegularCatState
	{
		Idle,
		Confused,
		Chasing
	}

	[Header("Detection")]
	public VisionSensor vision;
	public float unmaskedDetectionDelay = 2f;

	[Header("Strike System")]
	public int maxStrikes = 3;
	public int currentStrikes = 0;

	[Header("Confusion")]
	public float confusedDuration = 2f;

	[Header("Chase")]
	public float chaseSpeed = 4f;
	public float chaseTurnSpeed = 8f;

	[Header("UI")]
	public GameObject questionMarkIcon;
	public GameObject chaseIcon;

	private Transform player;
	private PlayerMask playerMask;
	private CatPatrol patrol;

	private RegularCatState state = RegularCatState.Idle;

	private float detectionTimer;
	private float confusedTimer;
	private bool playerWasMaskedLastFrame;

	void Start()
	{
		var playerObj = FindFirstObjectByType<PlayerController>();
		player = playerObj.transform;
		playerMask = playerObj.GetComponent<PlayerMask>();
		patrol = GetComponent<CatPatrol>();

		if (questionMarkIcon != null)
			questionMarkIcon.SetActive(false);

		if (chaseIcon != null)
			chaseIcon.SetActive(false);

		EnterIdle();
	}

	void Update()
	{
		switch (state)
		{
			case RegularCatState.Idle:
				HandleDetection();
				break;

			case RegularCatState.Confused:
				HandleConfused();
				break;

			case RegularCatState.Chasing:
				HandleChase();
				break;
		}

		playerWasMaskedLastFrame = playerMask.IsMasked;
	}

	void HandleDetection()
	{
		if (!vision.CanSeeTarget())
		{
			detectionTimer = 0f;
			return;
		}

		// Mask removed in vision -> instant chase
		if (playerWasMaskedLastFrame && !playerMask.IsMasked)
		{
			TriggerChase();
			return;
		}

		if (!playerMask.IsMasked)
		{
			detectionTimer += Time.deltaTime;
			if (detectionTimer >= unmaskedDetectionDelay)
				TriggerChase();
		}
		else
		{
			detectionTimer = 0f;

			if (!playerWasMaskedLastFrame)
				AddStrike();
		}
	}

	void AddStrike()
	{
		currentStrikes++;

		if (currentStrikes >= maxStrikes)
			TriggerChase();
		else
			EnterConfusedState();
	}

	void EnterConfusedState()
	{
		state = RegularCatState.Confused;
		confusedTimer = confusedDuration;

		patrol?.StopPatrol();

		if (questionMarkIcon != null)
			questionMarkIcon.SetActive(true);
	}

	void HandleConfused()
	{
		confusedTimer -= Time.deltaTime;
		if (confusedTimer <= 0f)
			EnterIdle();
	}

	void TriggerChase()
	{
		detectionTimer = 0f;
		state = RegularCatState.Chasing;

		patrol?.StopPatrol();

		if (questionMarkIcon != null)
			questionMarkIcon.SetActive(false);

		if (chaseIcon != null)
			chaseIcon.SetActive(true);

		Debug.Log("Regular Cat: PERMANENT CHASE!");
	}

	void EnterIdle()
	{
		state = RegularCatState.Idle;

		if (questionMarkIcon != null)
			questionMarkIcon.SetActive(false);

		if (chaseIcon != null)
			chaseIcon.SetActive(false);

		patrol?.ResumePatrol();
	}

	void HandleChase()
	{
		if (!player)
			return;

		Vector3 direction = player.position - transform.position;
		direction.y = 0f;

		Quaternion targetRot = Quaternion.LookRotation(direction);
		transform.rotation = Quaternion.Slerp(
			transform.rotation,
			targetRot,
			Time.deltaTime * chaseTurnSpeed
		);

		// Move forward
		transform.position += transform.forward * chaseSpeed * Time.deltaTime;

		// Catch check
		if (vision.CanSeeTarget())
		{
			float distance = Vector3.Distance(transform.position, player.position);
			if (distance < 1.2f)
			{
				Debug.Log("Player caught by regular cat!");
				// TODO: Game Over
			}
		}
	}
}

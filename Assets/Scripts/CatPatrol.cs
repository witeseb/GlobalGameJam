using UnityEngine;

public class CatPatrol : MonoBehaviour
{
	[Header("Patrol")]
	public Transform[] patrolPoints;
	public float moveSpeed = 2f;
	public float waitAtPointTime = 1.5f;
	public float rotationSpeed = 6f;

	private int currentIndex;
	private float waitTimer;
	private bool isWaiting;

	public bool IsPatrolling { get; private set; } = true;

	void Update()
	{
		if (!IsPatrolling || patrolPoints == null || patrolPoints.Length == 0)
			return;

		Patrol();
	}

	void Patrol()
	{
		Transform target = patrolPoints[currentIndex];

		Vector3 direction = target.position - transform.position;
		direction.y = 0f;

		float distance = direction.magnitude;

		if (distance < 0.2f)
		{
			if (!isWaiting)
			{
				isWaiting = true;
				waitTimer = waitAtPointTime;
			}

			waitTimer -= Time.deltaTime;
			if (waitTimer <= 0f)
			{
				currentIndex = (currentIndex + 1) % patrolPoints.Length;
				isWaiting = false;
			}
			return;
		}

		// Rotate toward target
		Quaternion targetRot = Quaternion.LookRotation(direction);
		transform.rotation = Quaternion.Slerp(
			transform.rotation,
			targetRot,
			Time.deltaTime * rotationSpeed
		);

		// Move forward (CORRECT movement)
		transform.position += transform.forward * moveSpeed * Time.deltaTime;
	}

	// ===== External Control =====
	public void StopPatrol()
	{
		IsPatrolling = false;
	}

	public void ResumePatrol()
	{
		IsPatrolling = true;
	}
}

using UnityEngine;

public class CatPatrol : MonoBehaviour
{
	[Header("Patrol")]
	public Transform[] patrolPoints;
	public float moveSpeed = 2f;
	public float waitAtPointTime = 1.5f;
	public float rotationSpeed = 6f;

	private int currentIndex = -1;
	private int nextIndex;
	private float waitTimer;
	private bool isWaiting;

	public bool IsPatrolling { get; private set; } = true;

    [Header("Obstacle Detection")]
    public float obstacleCheckDistance = 1.5f;
    public LayerMask obstacleMask;

    void Start()
	{
		ChooseNextPoint();
	}

	void Update()
	{
		if (!IsPatrolling || patrolPoints == null || patrolPoints.Length == 0)
			return;

		Patrol();
	}

	void Patrol()
	{
		Transform target = patrolPoints[nextIndex];

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
				currentIndex = nextIndex;
				ChooseNextPoint();
				isWaiting = false;
			}
			return;
		}

		Quaternion targetRot = Quaternion.LookRotation(direction);
		transform.rotation = Quaternion.Slerp(
			transform.rotation,
			targetRot,
			Time.deltaTime * rotationSpeed
		);

        if (!IsObstacleAhead())
        {
            transform.position += transform.forward * moveSpeed * Time.deltaTime;
        }
        else
        {
            // Obstacle detected - choose a new patrol point
            currentIndex = nextIndex;
            ChooseNextPoint();
            isWaiting = false;
        }
        bool IsObstacleAhead()
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;

            return Physics.Raycast(
                rayOrigin,
                transform.forward,
                obstacleCheckDistance,
                obstacleMask
            );
        }
    }

	void ChooseNextPoint()
	{
		if (patrolPoints.Length == 1)
		{
			nextIndex = 0;
			return;
		}

		int newIndex;
		do
		{
			newIndex = Random.Range(0, patrolPoints.Length);
		}
		while (newIndex == currentIndex);

		nextIndex = newIndex;
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

using UnityEngine;

public class BossHallwayPatrol : MonoBehaviour
{
	public Transform pointA;
	public Transform pointB;

	public float moveSpeed = 1.5f;
	public float turnSpeed = 4f;
	public float waitTimeAtEnds = 0.5f;

	private Transform currentTarget;
	private float waitTimer;
	private bool waiting;

	void Start()
	{
		currentTarget = pointB;
	}

	void Update()
	{
		if (waiting)
		{
			waitTimer -= Time.deltaTime;
			if (waitTimer <= 0f)
				waiting = false;
			return;
		}

		Vector3 direction = (currentTarget.position - transform.position);
		direction.y = 0f;

		if (direction.magnitude < 0.2f)
		{
			waiting = true;
			waitTimer = waitTimeAtEnds;
			currentTarget = currentTarget == pointA ? pointB : pointA;
			return;
		}

		Quaternion targetRot = Quaternion.LookRotation(direction);
		transform.rotation = Quaternion.Slerp(
			transform.rotation,
			targetRot,
			Time.deltaTime * turnSpeed
		);

		transform.position += transform.forward * moveSpeed * Time.deltaTime;
	}
}

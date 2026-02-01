using UnityEngine;

public class VisionSensor : MonoBehaviour
{
	[Header("Vision Settings")]
	public float viewRadius = 10f;
	[Range(0f, 360f)]
	public float viewAngle = 120f;

	[Header("Layer Masks")]
	public LayerMask targetMask;     // Player
	public LayerMask obstacleMask;   // Walls, props

	[Header("Debug")]
	public bool drawGizmos = true;

	public Transform CurrentTarget { get; private set; }

	public bool CanSeeTarget()
	{
		Collider[] targets = Physics.OverlapSphere(
			transform.position,
			viewRadius,
			targetMask
		);

		if (targets.Length == 0)
		{
			CurrentTarget = null;
			return false;
		}

		Transform target = targets[0].transform;
		Vector3 dirToTarget = (target.position - transform.position).normalized;

		float angle = Vector3.Angle(transform.forward, dirToTarget);
		if (angle > viewAngle * 0.5f)
		{
			CurrentTarget = null;
			return false;
		}

		float distance = Vector3.Distance(transform.position, target.position);

		if (Physics.Raycast(
			transform.position,
			dirToTarget,
			distance,
			obstacleMask))
		{
			CurrentTarget = null;
			return false;
		}

		CurrentTarget = target;
		return true;
	}

	private void OnDrawGizmosSelected()
	{
		if (!drawGizmos) return;

		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, viewRadius);

		Vector3 leftBoundary = DirFromAngle(-viewAngle / 2);
		Vector3 rightBoundary = DirFromAngle(viewAngle / 2);

		Gizmos.color = Color.blue;
		Gizmos.DrawLine(transform.position, transform.position + leftBoundary * viewRadius);
		Gizmos.DrawLine(transform.position, transform.position + rightBoundary * viewRadius);

		if (CurrentTarget != null)
		{
			Gizmos.color = Color.red;
			Gizmos.DrawLine(transform.position, CurrentTarget.position);
		}
	}

	private Vector3 DirFromAngle(float angle)
	{
		float rad = (angle + transform.eulerAngles.y) * Mathf.Deg2Rad;
		return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
	}
}

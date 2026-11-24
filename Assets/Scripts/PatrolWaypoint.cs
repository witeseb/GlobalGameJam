using UnityEngine;

// A point cats can patrol to. Put these under a PatrolNetwork.
// Room points: the cat stops and looks around. Junction points: it passes through.
public class PatrolWaypoint : MonoBehaviour
{
	public enum Kind { Room, Junction }

	public Kind kind = Kind.Room;

	public bool LookAround => kind == Kind.Room;

	private void OnDrawGizmos()
	{
		Gizmos.color = kind == Kind.Room ? new Color(0.2f, 0.8f, 1f) : new Color(1f, 0.6f, 0.1f);
		Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, kind == Kind.Room ? 0.35f : 0.25f);
	}
}

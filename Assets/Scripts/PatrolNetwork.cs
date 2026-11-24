using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// All patrol waypoints of a level (the PatrolWaypoint children of this object).
// Each cat builds a territory from it: the waypoints within walking distance of where it starts.
// In Level the waypoints are placed by hand; in Level_Proc LevelGenerator fills them in.
public class PatrolNetwork : MonoBehaviour
{
	private readonly List<PatrolWaypoint> waypoints = new List<PatrolWaypoint>();
	private bool dirty = true;

	public IReadOnlyList<PatrolWaypoint> Waypoints
	{
		get
		{
			if (dirty)
				Refresh();
			return waypoints;
		}
	}

	public void Refresh()
	{
		waypoints.Clear();
		GetComponentsInChildren(false, waypoints);
		dirty = false;
	}

	private void OnTransformChildrenChanged()
	{
		dirty = true;
	}

	// Waypoints reachable from home within maxPathDistance (walking distance on the NavMesh),
	// nearest first. Always returns at least minPoints reachable waypoints if the level has them.
	public List<PatrolWaypoint> BuildTerritory(Vector3 home, NavMeshQueryFilter filter, float maxPathDistance, int minPoints)
	{
		var territory = new List<PatrolWaypoint>();
		if (!NavMesh.SamplePosition(home, out NavMeshHit start, 2f, filter))
			return territory;

		var scored = new List<(PatrolWaypoint waypoint, float distance)>();
		var path = new NavMeshPath();

		foreach (var waypoint in Waypoints)
		{
			Vector3 p = waypoint.transform.position;
			Vector3 probe = new Vector3(p.x, start.position.y, p.z);
			if (!NavMesh.SamplePosition(probe, out NavMeshHit end, 2f, filter))
				continue;
			if (!NavMesh.CalculatePath(start.position, end.position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
				continue;

			scored.Add((waypoint, PathLength(path)));
		}

		scored.Sort((a, b) => a.distance.CompareTo(b.distance));
		foreach (var (waypoint, distance) in scored)
			if (distance <= maxPathDistance || territory.Count < minPoints)
				territory.Add(waypoint);

		return territory;
	}

	private static float PathLength(NavMeshPath path)
	{
		float length = 0f;
		var corners = path.corners;
		for (int i = 1; i < corners.Length; i++)
			length += Vector3.Distance(corners[i - 1], corners[i]);
		return length;
	}
}

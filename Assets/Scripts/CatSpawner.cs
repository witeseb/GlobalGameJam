using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Spawns a regular cat at the children of catSpawnContainer.
// In Level_Proc, LevelGenerator fills the container; elsewhere place the points by hand.
public class CatSpawner : MonoBehaviour
{
	[SerializeField] private Transform catSpawnContainer;
	public GameObject catPrefab;
	[Tooltip("0 = one cat per spawn point. Otherwise a random subset of this size.")]
	public int maxCats = 0;
	[Tooltip("Parent for the spawned cats (optional).")]
	[SerializeField] private Transform spawnParent;

	private readonly List<RegularCatAI> cats = new List<RegularCatAI>();
	public IReadOnlyList<RegularCatAI> Cats => cats;

	void Start()
	{
		SpawnCats();
	}

	void SpawnCats()
	{
		if (catSpawnContainer == null || catPrefab == null)
		{
			Debug.LogError("[CatSpawner] catSpawnContainer or catPrefab is not assigned!");
			return;
		}

		var points = new List<Transform>();
		foreach (Transform t in catSpawnContainer)
			points.Add(t);

		int count = maxCats > 0 ? Mathf.Min(maxCats, points.Count) : points.Count;

		var agentPrefab = catPrefab.GetComponent<NavMeshAgent>();
		var filter = new NavMeshQueryFilter
		{
			agentTypeID = agentPrefab != null ? agentPrefab.agentTypeID : 0,
			areaMask = NavMesh.AllAreas
		};

		for (int i = 0; i < count; i++)
		{
			int index = Random.Range(0, points.Count);
			Transform point = points[index];
			points.RemoveAt(index);

			Vector3 position = point.position;
			if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, filter))
				position = hit.position;
			else
				Debug.LogWarning($"[CatSpawner] Spawn point {point.name} is not near the NavMesh.");

			var cat = Instantiate(catPrefab, position, point.rotation, spawnParent);
			cat.name = $"Cat_{i}";

			var ai = cat.GetComponent<RegularCatAI>();
			if (ai != null)
				cats.Add(ai);
		}

		Debug.Log($"[CatSpawner] Spawned {cats.Count} cats");
	}

	void OnDrawGizmosSelected()
	{
		if (catSpawnContainer == null)
			return;

		Gizmos.color = Color.red;
		foreach (Transform t in catSpawnContainer)
			Gizmos.DrawWireSphere(t.position, 0.4f);
	}
}

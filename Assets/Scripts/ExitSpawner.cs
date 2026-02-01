using UnityEngine;

public class ExitSpawner : MonoBehaviour
{
	[Header("Exit")]
	public GameObject exitPrefab;

	[Header("Spawn Points Container")]
	[SerializeField] private Transform spawnPointsContainer;

	private Transform[] spawnPoints;

	void Awake()
	{
		InitSpawnPoints();
	}

	void Start()
	{
		SpawnExit();
	}

	// ================= INIT =================
	void InitSpawnPoints()
	{
		if (spawnPointsContainer == null)
		{
			Debug.LogError("ExitSpawner: No spawnPointsContainer assigned!");
			return;
		}

		int count = spawnPointsContainer.childCount;
		spawnPoints = new Transform[count];

		for (int i = 0; i < count; i++)
			spawnPoints[i] = spawnPointsContainer.GetChild(i);

		Debug.Log($"ExitSpawner: Found {spawnPoints.Length} exit spawn points");
	}

	// ================= SPAWN =================
	void SpawnExit()
	{
		if (exitPrefab == null)
		{
			Debug.LogError("ExitSpawner: exitPrefab is NULL!");
			return;
		}

		if (spawnPoints == null || spawnPoints.Length == 0)
		{
			Debug.LogError("ExitSpawner: No spawn points found!");
			return;
		}

		int index = Random.Range(0, spawnPoints.Length);
		Transform spawn = spawnPoints[index];

		Instantiate(exitPrefab, spawn.position, spawn.rotation);
	}

	// ================= DEBUG =================
	void OnDrawGizmosSelected()
	{
		if (spawnPointsContainer == null)
			return;

		Gizmos.color = Color.green;
		foreach (Transform t in spawnPointsContainer)
		{
			Gizmos.DrawCube(t.position, Vector3.one * 0.4f);
		}
	}
}

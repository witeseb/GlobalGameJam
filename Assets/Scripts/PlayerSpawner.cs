using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
	[SerializeField] private Transform spawnPointsContainer;

	private Transform[] spawnPoints;

	void Awake()
	{
		InitSpawnPoints();
	}

	void Start()
	{
		TeleportPlayer();
	}

	// ================= INIT =================
	void InitSpawnPoints()
	{
		if (spawnPointsContainer == null)
		{
			Debug.LogError("PlayerSpawnManager: spawnPointsContainer is NULL!");
			return;
		}

		int count = spawnPointsContainer.childCount;
		spawnPoints = new Transform[count];

		for (int i = 0; i < count; i++)
			spawnPoints[i] = spawnPointsContainer.GetChild(i);

		Debug.Log($"PlayerSpawnManager: Found {spawnPoints.Length} player spawn points");
	}

	// ================= TELEPORT =================
	void TeleportPlayer()
	{
		if (spawnPoints == null || spawnPoints.Length == 0)
		{
			Debug.LogError("PlayerSpawnManager: No spawn points found!");
			return;
		}

		PlayerController player = FindFirstObjectByType<PlayerController>();
		if (player == null)
		{
			Debug.LogError("PlayerSpawnManager: PlayerController NOT FOUND!");
			return;
		}

		CharacterController cc = player.GetComponent<CharacterController>();

		int index = Random.Range(0, spawnPoints.Length);
		Transform spawn = spawnPoints[index];

		// IMPORTANT: Disable CC before teleporting
		cc.enabled = false;
		player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
		cc.enabled = true;

		Debug.Log($"Player teleported to spawn point {index}");
	}

	// ================= DEBUG =================
	void OnDrawGizmosSelected()
	{
		if (spawnPointsContainer == null)
			return;

		Gizmos.color = Color.blue;
		foreach (Transform t in spawnPointsContainer)
			Gizmos.DrawSphere(t.position, 0.25f);
	}
}

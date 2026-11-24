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
		RespawnPlayer();
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
	// Teleports the player to a random spawn point. Also used after a cat catches the player.
	public void RespawnPlayer()
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

		int index = Random.Range(0, spawnPoints.Length);
		Transform spawn = spawnPoints[index];

		// Disables the CharacterController while teleporting and clears leftover velocity.
		player.Teleport(spawn.position, spawn.rotation);

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

using System.Collections.Generic;
using UnityEngine;

public class CheeseSpawner : MonoBehaviour
{
	[Header("Spawn Containers")]
	[SerializeField] private Transform bigCheeseContainer;
	[SerializeField] private Transform smallCheeseContainer;

	[Header("Prefabs")]
	public GameObject bigCheesePrefab;
	public GameObject smallCheesePrefab;

	[Header("Small Cheese Count")]
	public int minSmallCheese = 3;
	public int maxSmallCheese = 6;

	private Transform[] bigCheeseSpawnPoints;
	private Transform[] smallCheeseSpawnPoints;

	void Awake()
	{
		InitSpawnPoints();
	}

	void Start()
	{
		SpawnBigCheese();
		SpawnSmallCheese();
	}

	// ================= INIT =================
	void InitSpawnPoints()
	{
		if (bigCheeseContainer == null)
		{
			Debug.LogError("[CheeseSpawner] BigCheeseContainer NOT assigned!");
		}
		else
		{
			bigCheeseSpawnPoints = GetChildren(bigCheeseContainer);
			Debug.Log($"[CheeseSpawner] Big cheese points: {bigCheeseSpawnPoints.Length}");
		}

		if (smallCheeseContainer == null)
		{
			Debug.LogError("[CheeseSpawner] SmallCheeseContainer NOT assigned!");
		}
		else
		{
			smallCheeseSpawnPoints = GetChildren(smallCheeseContainer);
			Debug.Log($"[CheeseSpawner] Small cheese points: {smallCheeseSpawnPoints.Length}");
		}
	}


	Transform[] GetChildren(Transform parent)
	{
		Transform[] points = new Transform[parent.childCount];
		for (int i = 0; i < parent.childCount; i++)
			points[i] = parent.GetChild(i);
		return points;
	}

	// ================= BIG CHEESE =================
	void SpawnBigCheese()
	{
		if (bigCheeseSpawnPoints == null || bigCheeseSpawnPoints.Length == 0)
		{
			Debug.LogError("No BIG cheese spawn points!");
			return;
		}

		Transform spawn = bigCheeseSpawnPoints[Random.Range(0, bigCheeseSpawnPoints.Length)];
		Instantiate(bigCheesePrefab, spawn.position, spawn.rotation);
	}

	// ================= SMALL CHEESE =================
	void SpawnSmallCheese()
	{
		if (smallCheeseSpawnPoints == null || smallCheeseSpawnPoints.Length == 0)
			return;

		List<Transform> available = new List<Transform>(smallCheeseSpawnPoints);

		int count = Random.Range(minSmallCheese, maxSmallCheese + 1);
		count = Mathf.Min(count, available.Count);

		for (int i = 0; i < count; i++)
		{
			int index = Random.Range(0, available.Count);
			Transform spawn = available[index];

			Instantiate(smallCheesePrefab, spawn.position, spawn.rotation);
			available.RemoveAt(index);
		}
	}

	// ================= DEBUG =================
	void OnDrawGizmosSelected()
	{
		if (bigCheeseContainer != null)
		{
			Gizmos.color = Color.yellow;
			foreach (Transform t in bigCheeseContainer)
				Gizmos.DrawSphere(t.position, 0.2f);
		}

		if (smallCheeseContainer != null)
		{
			Gizmos.color = Color.cyan;
			foreach (Transform t in smallCheeseContainer)
				Gizmos.DrawSphere(t.position, 0.15f);
		}
	}
}

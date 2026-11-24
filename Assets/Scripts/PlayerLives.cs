using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lives against regular cats. A catch freezes the player, takes a life,
// then respawns them with a short invulnerability window. 0 lives = game over.
public class PlayerLives : MonoBehaviour
{
	[SerializeField] private int maxLives = 3;
	[Tooltip("Seconds the player is frozen while the catch plays.")]
	[SerializeField] private float caughtFreezeTime = 1.2f;
	[Tooltip("Seconds after respawning during which the player can't be caught again.")]
	[SerializeField] private float invulnerableTime = 2f;
	[Tooltip("Teleport the player to a spawn point after being caught.")]
	[SerializeField] private bool respawnAfterCatch = true;
	[Tooltip("Placeholder until a game over screen exists: reload the scene after this many seconds. Negative = don't reload.")]
	[SerializeField] private float reloadSceneDelay = 3f;

	public int MaxLives => maxLives;
	public int CurrentLives { get; private set; }
	public bool IsGameOver { get; private set; }
	public bool CanBeCaught => !IsGameOver && !isBeingCaught && Time.time >= invulnerableUntil;

	// (currentLives, maxLives)
	public event Action<int, int> OnLivesChanged;
	public event Action OnCaught;
	public event Action OnGameOver;

	private PlayerController controller;
	private PlayerSpawnManager spawnManager;
	private bool isBeingCaught;
	private float invulnerableUntil;

	void Awake()
	{
		CurrentLives = maxLives;
		controller = GetComponent<PlayerController>();
	}

	void Start()
	{
		spawnManager = FindAnyObjectByType<PlayerSpawnManager>();
		OnLivesChanged?.Invoke(CurrentLives, maxLives);
	}

	// Called by a cat that reached the player. Returns false if the catch doesn't count
	// (already being caught, just respawned, or game over).
	public bool TryCatch()
	{
		if (!CanBeCaught)
			return false;

		StartCoroutine(CatchRoutine());
		return true;
	}

	private IEnumerator CatchRoutine()
	{
		isBeingCaught = true;
		CurrentLives = Mathf.Max(CurrentLives - 1, 0);

		Debug.Log($"[PlayerLives] Caught! Lives left: {CurrentLives}/{maxLives}");

		if (controller != null)
			controller.SetControlEnabled(false);

		OnLivesChanged?.Invoke(CurrentLives, maxLives);
		OnCaught?.Invoke();

		if (CurrentLives <= 0)
		{
			IsGameOver = true;
			Debug.Log("[PlayerLives] GAME OVER");
			OnGameOver?.Invoke();

			// TODO: replace with a game over screen.
			if (reloadSceneDelay >= 0f)
			{
				yield return new WaitForSeconds(reloadSceneDelay);
				ReloadScene();
			}
			yield break;
		}

		yield return new WaitForSeconds(caughtFreezeTime);

		if (respawnAfterCatch && spawnManager != null)
			spawnManager.RespawnPlayer();

		if (controller != null)
			controller.SetControlEnabled(true);

		invulnerableUntil = Time.time + invulnerableTime;
		isBeingCaught = false;
	}

	private static void ReloadScene()
	{
		Scene scene = SceneManager.GetActiveScene();
		if (scene.buildIndex >= 0)
		{
			SceneManager.LoadScene(scene.buildIndex);
			return;
		}

#if UNITY_EDITOR
		// Scenes not in Build Settings (e.g. Level_Proc) can only be reloaded this way in the Editor.
		UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
			scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
		Debug.LogError($"[PlayerLives] Scene '{scene.name}' is not in Build Settings; can't reload.");
#endif
	}
}

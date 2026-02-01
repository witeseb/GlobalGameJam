using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
	[Header("Interaction")]
	public float interactRange = 2.5f;
	public LayerMask interactableLayer;

	[Header("Debug")]
	public bool showDebug = true;

	private PlayerInventory inventory;
	private InputAction interactAction;
	private Camera playerCamera;

	void Awake()
	{
		inventory = GetComponent<PlayerInventory>();
		playerCamera = Camera.main;

		if (inventory == null)
			Debug.LogError("PlayerInteractor: No PlayerInventory found in parent!");

		if (playerCamera == null)
			Debug.LogError("PlayerInteractor: Main Camera not found!");

		interactAction = InputSystem.actions.FindAction("Interact");

		if (interactAction == null)
			Debug.LogError("PlayerInteractor: Interact action not found! Check Input Actions.");
	}

	void OnEnable()
	{
		if (interactAction != null)
			interactAction.Enable();
	}

	void OnDisable()
	{
		if (interactAction != null)
			interactAction.Disable();
	}

	void Update()
	{
		if (interactAction != null && interactAction.WasPressedThisFrame())
		{
			TryInteract();
		}
	}

	void TryInteract()
	{
		if (playerCamera == null)
			return;

		Vector3 origin = playerCamera.transform.position;
		Vector3 direction = playerCamera.transform.forward;

		// Runtime debug ray
		if (showDebug)
		{
			Debug.DrawRay(origin, direction * interactRange, Color.cyan, 0.5f);
		}

		if (Physics.Raycast(origin, direction, out RaycastHit hit, interactRange, interactableLayer))
		{
			if (showDebug)
				Debug.Log($"[Interactor] Hit: {hit.collider.name}");

			IInteractable interactable = hit.collider.GetComponent<IInteractable>();

			if (interactable != null)
			{
				interactable.Interact(inventory);
			}
			else if (showDebug)
			{
				Debug.LogWarning($"{hit.collider.name} is on interactable layer but has no IInteractable");
			}
		}
		else if (showDebug)
		{
			Debug.Log("[Interactor] Nothing hit");
		}
	}

	// Scene view gizmo
	void OnDrawGizmos()
	{
		Camera cam = Camera.main;
		if (cam == null)
			return;

		Gizmos.color = Color.cyan;
		Gizmos.DrawLine(cam.transform.position, cam.transform.position + cam.transform.forward * interactRange);
		Gizmos.DrawWireSphere(cam.transform.position + cam.transform.forward * interactRange, 0.05f);
	}
}

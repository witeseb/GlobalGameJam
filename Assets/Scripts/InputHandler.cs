using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    public PlayerController playerController;
	public PlayerMask playerMask;

	public InputAction _moveAction;
	public InputAction _sprintAction;
	public InputAction _lookAction;
	public InputAction _jumpAction;
	public InputAction _maskAction;


	private void Awake()
	{
		playerController = FindFirstObjectByType<PlayerController>();

		if (playerController == null)
		{
			Debug.LogError("[InputHandler] PlayerController NOT FOUND in scene!");
			enabled = false;
			return;
		}

		playerMask = playerController.GetComponent<PlayerMask>();

		if (playerMask == null)
		{
			Debug.LogWarning("[InputHandler] PlayerMask NOT FOUND on PlayerController (Mask input will do nothing)");
		}
	}


	private void OnEnable()
	{
		_moveAction?.Enable();
		_lookAction?.Enable();
		_jumpAction?.Enable();
		_maskAction?.Enable();
		_sprintAction?.Enable();
	}

	private void OnDisable()
	{
		_moveAction?.Disable();
		_lookAction?.Disable();
		_jumpAction?.Disable();
		_maskAction?.Disable();
		_sprintAction?.Disable();
	}

	private void Start()
	{
		_moveAction = InputSystem.actions.FindAction("Move");
		_lookAction = InputSystem.actions.FindAction("Look");
		_jumpAction = InputSystem.actions.FindAction("Jump");
		_maskAction = InputSystem.actions.FindAction("Mask");
		_sprintAction = InputSystem.actions.FindAction("Sprint");

		ValidateInputAction(_moveAction, "Move");
		ValidateInputAction(_lookAction, "Look");
		ValidateInputAction(_jumpAction, "Jump");
		ValidateInputAction(_maskAction, "Mask");
		ValidateInputAction(_sprintAction, "Sprint");

		if (_jumpAction != null)
			_jumpAction.performed += ctx => playerController.Jump();

		if (_maskAction != null && playerMask != null)
			_maskAction.started += ctx => playerMask.ToggleMask();

		Cursor.visible = false;
	}

	private void Update()
	{
		if (playerController == null)
			return;

		HandleMovement();
		HandleLook();
	}

	private void HandleMovement()
	{
		Vector2 inputVector = _moveAction.ReadValue<Vector2>();
		bool isSprinting = _sprintAction.IsPressed();
		playerController.Move(inputVector, isSprinting);
	}

	private void HandleLook()
	{
		Vector2 inputVector = _lookAction.ReadValue<Vector2>();
		float horizontal = inputVector.x;
		float vertical = inputVector.y;
		playerController.Look(inputVector);
	}

	private void ValidateInputAction(InputAction action, string actionName)
	{
		if (action == null)
		{
			Debug.LogError($"[InputHandler] InputAction '{actionName}' NOT FOUND in Input Actions asset!");
		}
	}
}

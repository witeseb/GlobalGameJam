using UnityEngine;

public class PlayerController : MonoBehaviour
{
	private CharacterController characterController;

	[Header("Movement Settings")]
	public float walkSpeed = 6f;
	public float sprintSpeed = 8f;
	public float mouseSensitivity = 2f;
	public float jumpForce = 10f;
	public float gravity = -40.0f;

	[Header("Stamina")]
	public float maxStamina = 100f;
	public float staminaDrainRate = 25f;
	public float staminaRegenRate = 20f;
	public float staminaRegenDelay = 0.5f;

	[Header("Camera Settings")]
	public Camera playerCamera;
	[SerializeField] private float normalFOV = 60f;
	[SerializeField] private float sprintFOV = 70f;
	[SerializeField] private float fovSpeed = 8f;

	[Header("Camera Bob")]
	[SerializeField] private float walkBobSpeed = 10f;
	[SerializeField] private float walkBobAmount = 0.05f;
	[SerializeField] private float sprintBobSpeed = 16f;
	[SerializeField] private float sprintBobAmount = 0.09f;
	[SerializeField] private float bobSmoothness = 8f;

	[Header("States")]
	public bool IsSprinting { get; private set; }
	public float CurrentStamina { get; private set; }

	[Header("Internal Variables")]
	private float _verticalVelocity;
	private float _xRotation;
	private float _regenTimer;
	private float _bobTimer;
	private Vector3 _cameraStartLocalPos;

	void Start()
	{
		characterController = FindFirstObjectByType<CharacterController>();

		if (characterController == null)
		{
			Debug.LogError("[PlayerController] CharacterController NOT FOUND in scene!");
			enabled = false;
			return;
		}

		playerCamera = FindFirstObjectByType<Camera>();

		if (playerCamera == null)
		{
			Debug.LogError("[PlayerController] Camera NOT FOUND as child of CharacterController!");
			enabled = false;
			return;
		}

		CurrentStamina = maxStamina;
		_cameraStartLocalPos = playerCamera.transform.localPosition;
	}


	void Update()
	{
		float targetFOV = IsSprinting ? sprintFOV : normalFOV;
		playerCamera.fieldOfView =
			Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * fovSpeed);
	}

	public void Move(Vector2 movementVector, bool sprintInput)
	{
		if (characterController == null)
		{
			Debug.LogError("[PlayerController] Move() called but CharacterController is NULL");
			return;
		}

		if (playerCamera == null)
		{
			Debug.LogError("[PlayerController] Move() called but Camera is NULL");
			return;
		}
		
		if (characterController.isGrounded && _verticalVelocity < 0)
			_verticalVelocity = -2f;

		bool canSprint =
			sprintInput &&
			characterController.isGrounded &&
			CurrentStamina > 0f &&
			movementVector.sqrMagnitude > 0.1f;

		IsSprinting = canSprint && characterController.isGrounded;

		float currentSpeed = IsSprinting ? sprintSpeed : walkSpeed;

		Vector3 move =
			transform.forward * movementVector.y +
			transform.right * movementVector.x;

		characterController.Move(move * currentSpeed * Time.deltaTime);

		HandleStamina(sprintInput);
		HandleCameraBob(movementVector);

		_verticalVelocity += gravity * Time.deltaTime;
		characterController.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
	}

	public void Look(Vector2 lookVector)
	{
		if (playerCamera == null)
		{
			Debug.LogError("[PlayerController] Look() called but Camera is NULL");
			return;
		}

		float mouseX = lookVector.x * mouseSensitivity * Time.deltaTime;
		float mouseY = lookVector.y * mouseSensitivity * Time.deltaTime;

		_xRotation -= mouseY;
		_xRotation = Mathf.Clamp(_xRotation, -60f, 60f);

		playerCamera.transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
		transform.Rotate(Vector3.up * mouseX);
	}

	public void Jump()
	{
		if (characterController.isGrounded)
		{
			_verticalVelocity = jumpForce;
		}
	}

	private void HandleStamina(bool sprintInput)
	{
		if (IsSprinting)
		{
			CurrentStamina -= staminaDrainRate * Time.deltaTime;
			CurrentStamina = Mathf.Max(CurrentStamina, 0f);
			_regenTimer = staminaRegenDelay;
		}
		else
		{
			if (!sprintInput)
				_regenTimer -= Time.deltaTime;

			if (_regenTimer <= 0f)
			{
				CurrentStamina += staminaRegenRate * Time.deltaTime;
				CurrentStamina = Mathf.Min(CurrentStamina, maxStamina);
			}
		}
	}

	private void HandleCameraBob(Vector2 movementVector)
	{
		bool isMoving =
			movementVector.sqrMagnitude > 0.1f &&
			characterController.isGrounded;

		if (!isMoving)
		{
			_bobTimer = 0f;
			playerCamera.transform.localPosition = Vector3.Lerp(
				playerCamera.transform.localPosition,
				_cameraStartLocalPos,
				Time.deltaTime * bobSmoothness
			);
			return;
		}

		float bobSpeed = IsSprinting ? sprintBobSpeed : walkBobSpeed;
		float bobAmount = IsSprinting ? sprintBobAmount : walkBobAmount;

		_bobTimer += Time.deltaTime * bobSpeed;

		float bobOffsetY = Mathf.Sin(_bobTimer) * bobAmount;

		Vector3 targetPos = _cameraStartLocalPos + new Vector3(0f, bobOffsetY, 0f);

		playerCamera.transform.localPosition = Vector3.Lerp(
			playerCamera.transform.localPosition,
			targetPos,
			Time.deltaTime * bobSmoothness
		);
	}


	public float GetStaminaNormalized()
	{
		return CurrentStamina / maxStamina;
	}
}

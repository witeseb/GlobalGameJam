using UnityEngine;

public class PlayerController : MonoBehaviour
{
	private CharacterController characterController;
	private PlayerInventory inventory;
	private PlayerMask playerMask;

	[Header("Movement Settings")]
	public float walkSpeed = 6f;
	public float sprintSpeed = 8f;
	public float mouseSensitivity = 2f;
	public float jumpForce = 10f;
	public float gravity = -40.0f;

	[Header("Scurry Feel")]
	[Tooltip("How fast the mouse reaches target speed (units/s^2). High = twitchy starts.")]
	[SerializeField] private float acceleration = 25f;
	[Tooltip("How fast the mouse stops when input is released. Lower than acceleration = small skid.")]
	[SerializeField] private float deceleration = 15f;
	[Tooltip("Acceleration multiplier while airborne.")]
	[SerializeField] private float airControl = 0.4f;

	[Header("Dash (start of sprint)")]
	[SerializeField] private float dashSpeedMultiplier = 1.5f;
	[SerializeField] private float dashDuration = 0.18f;
	[SerializeField] private float dashCooldown = 0.6f;

	[Header("Stamina")]
	public float maxStamina = 100f;
	public float staminaDrainRate = 25f;
	public float staminaRegenRate = 20f;
	public float staminaRegenDelay = 0.5f;

	[Header("Freeze (stand still to be harder to notice)")]
	[Tooltip("Seconds of standing completely still before the mouse counts as frozen.")]
	[SerializeField] private float freezeDelay = 0.5f;
	[Tooltip("Horizontal speed below which the mouse counts as still.")]
	[SerializeField] private float freezeSpeedThreshold = 0.1f;
	[Tooltip("FOV narrows by this much while frozen, as feedback.")]
	[SerializeField] private float frozenFOVOffset = -4f;

	[Header("Weight")]
	[SerializeField] private float bigCheeseSpeedMultiplier = 0.75f;
	[SerializeField] private float bigCheeseJumpMultiplier = 0.7f;
	[SerializeField] private float bigCheeseStaminaDrainMultiplier = 1.5f;
	[SerializeField] private float maskedSpeedMultiplier = 0.85f;

	[Header("Camera Settings")]
	public Camera playerCamera;
	[SerializeField] private float normalFOV = 60f;
	[SerializeField] private float sprintFOV = 70f;
	[SerializeField] private float fovSpeed = 8f;

	[Header("Camera Bob (quick little steps)")]
	[SerializeField] private float walkBobSpeed = 11f;
	[SerializeField] private float walkBobAmount = 0.03f;
	[SerializeField] private float sprintBobSpeed = 15f;
	[SerializeField] private float sprintBobAmount = 0.045f;
	[Tooltip("Side-to-side camera roll in degrees at full bob.")]
	[SerializeField] private float bobRollAmount = 0.5f;
	[Tooltip("How fast the bob strength follows the player's actual speed.")]
	[SerializeField] private float bobSmoothness = 8f;
	[Tooltip("How fast bob speed/amount blend between walk and sprint.")]
	[SerializeField] private float bobBlendSpeed = 6f;
	[Tooltip("Final smoothing on the camera offset and roll (seconds). Higher = softer.")]
	[SerializeField] private float bobSmoothTime = 0.06f;

	[Header("Landing Dip")]
	[SerializeField] private float landingDipAmount = 0.1f;
	[Tooltip("Fall speed that gives the full dip.")]
	[SerializeField] private float landingDipMaxFallSpeed = 8f;
	[SerializeField] private float landingRecoverSpeed = 10f;

	public bool IsSprinting { get; private set; }
	public bool IsDashing => _dashTimer > 0f;
	public bool IsFrozen { get; private set; }
	public float CurrentStamina { get; private set; }
	public bool ControlEnabled { get; private set; } = true;

	private Vector3 _horizontalVelocity;
	private float _verticalVelocity;
	private float _xRotation;
	private float _regenTimer;
	private float _dashTimer;
	private float _dashCooldownTimer;
	private bool _sprintHeldLastFrame;
	private float _stillTimer;
	private bool _wasGrounded = true;
	private float _bobTimer;
	private float _bobWeight;
	private float _bobRoll;
	private float _bobRollVelocity;
	private float _currentBobSpeed;
	private float _currentBobAmount;
	private Vector3 _cameraOffset;
	private Vector3 _cameraOffsetVelocity;
	private float _landingOffset;
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

		inventory = GetComponent<PlayerInventory>();
		playerMask = GetComponent<PlayerMask>();

		CurrentStamina = maxStamina;
		_cameraStartLocalPos = playerCamera.transform.localPosition;
		_currentBobSpeed = walkBobSpeed;
		_currentBobAmount = walkBobAmount;
	}


	void Update()
	{
		float targetFOV = IsSprinting ? sprintFOV : normalFOV;
		if (IsFrozen)
			targetFOV += frozenFOVOffset;

		playerCamera.fieldOfView =
			Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * fovSpeed);
	}

	private bool CarryingBigCheese => inventory != null && inventory.HasBigCheese;

	private float SpeedMultiplier
	{
		get
		{
			float m = 1f;
			if (CarryingBigCheese) m *= bigCheeseSpeedMultiplier;
			if (playerMask != null && playerMask.IsMasked) m *= maskedSpeedMultiplier;
			return m;
		}
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

		if (!ControlEnabled)
		{
			movementVector = Vector2.zero;
			sprintInput = false;
		}

		bool grounded = characterController.isGrounded;
		bool hasMoveInput = movementVector.sqrMagnitude > 0.1f;

		if (grounded && _verticalVelocity < 0)
			_verticalVelocity = -2f;

		bool canSprint =
			sprintInput &&
			grounded &&
			CurrentStamina > 0f &&
			hasMoveInput;

		IsSprinting = canSprint;

		HandleDash(sprintInput);

		// Target speed
		float targetSpeed = IsSprinting ? sprintSpeed : walkSpeed;
		if (IsDashing)
			targetSpeed = sprintSpeed * dashSpeedMultiplier;
		targetSpeed *= SpeedMultiplier;

		Vector3 inputDir =
			transform.forward * movementVector.y +
			transform.right * movementVector.x;
		inputDir = Vector3.ClampMagnitude(inputDir, 1f);

		Vector3 targetVelocity = inputDir * targetSpeed;

		// Accelerate / decelerate towards target (twitchy start, short skid)
		float rate = hasMoveInput ? acceleration : deceleration;
		if (IsDashing)
			rate *= 3f;
		if (!grounded)
			rate *= airControl;

		_horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, rate * Time.deltaTime);

		_verticalVelocity += gravity * Time.deltaTime;
		float fallSpeed = -_verticalVelocity;

		Vector3 motion = (_horizontalVelocity + Vector3.up * _verticalVelocity) * Time.deltaTime;
		CollisionFlags flags = characterController.Move(motion);

		// Stop sliding into walls: drop velocity that the controller couldn't use
		if ((flags & CollisionFlags.Sides) != 0)
		{
			Vector3 actual = characterController.velocity;
			actual.y = 0f;
			if (actual.sqrMagnitude < _horizontalVelocity.sqrMagnitude)
				_horizontalVelocity = actual;
		}

		grounded = characterController.isGrounded;
		if (grounded && !_wasGrounded)
			OnLanded(fallSpeed);
		_wasGrounded = grounded;

		HandleStamina(sprintInput);
		HandleFreeze(hasMoveInput, grounded);
		HandleCameraBob(grounded);
	}

	public void Look(Vector2 lookVector)
	{
		if (playerCamera == null)
		{
			Debug.LogError("[PlayerController] Look() called but Camera is NULL");
			return;
		}

		if (!ControlEnabled)
			lookVector = Vector2.zero;

		float mouseX = lookVector.x * mouseSensitivity * Time.deltaTime;
		float mouseY = lookVector.y * mouseSensitivity * Time.deltaTime;

		_xRotation -= mouseY;
		_xRotation = Mathf.Clamp(_xRotation, -60f, 60f);

		playerCamera.transform.localRotation = Quaternion.Euler(_xRotation, 0f, _bobRoll);
		transform.Rotate(Vector3.up * mouseX);
	}

	// Used while the player is caught: input is ignored but gravity still applies.
	public void SetControlEnabled(bool value)
	{
		ControlEnabled = value;
		if (!value)
		{
			_horizontalVelocity = Vector3.zero;
			_dashTimer = 0f;
			IsSprinting = false;
		}
	}

	// Moves the player instantly (CharacterController must be off while teleporting)
	// and clears any movement carried over from before.
	public void Teleport(Vector3 position, Quaternion rotation)
	{
		// May run before Start() (e.g. from PlayerSpawnManager.Start), so don't rely on the cached field.
		CharacterController cc = characterController != null ? characterController : GetComponent<CharacterController>();

		bool ccWasEnabled = cc != null && cc.enabled;
		if (cc != null)
			cc.enabled = false;

		transform.SetPositionAndRotation(position, rotation);

		if (cc != null)
			cc.enabled = ccWasEnabled;

		_horizontalVelocity = Vector3.zero;
		_verticalVelocity = 0f;
		_dashTimer = 0f;
		_stillTimer = 0f;
		_wasGrounded = true;
		IsFrozen = false;
		_xRotation = 0f;
	}

	public void Jump()
	{
		if (!ControlEnabled)
			return;

		if (characterController.isGrounded)
		{
			float jumpMultiplier = CarryingBigCheese ? bigCheeseJumpMultiplier : 1f;
			_verticalVelocity = jumpForce * jumpMultiplier;
			_stillTimer = 0f;
			IsFrozen = false;
		}
	}

	private void HandleDash(bool sprintInput)
	{
		_dashCooldownTimer -= Time.deltaTime;
		_dashTimer -= Time.deltaTime;

		bool sprintPressedThisFrame = sprintInput && !_sprintHeldLastFrame;
		_sprintHeldLastFrame = sprintInput;

		if (sprintPressedThisFrame && IsSprinting && _dashCooldownTimer <= 0f)
		{
			_dashTimer = dashDuration;
			_dashCooldownTimer = dashCooldown;
		}

		if (!IsSprinting)
			_dashTimer = 0f;
	}

	private void HandleStamina(bool sprintInput)
	{
		if (IsSprinting)
		{
			float drain = staminaDrainRate;
			if (CarryingBigCheese)
				drain *= bigCheeseStaminaDrainMultiplier;

			CurrentStamina -= drain * Time.deltaTime;
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

	private void HandleFreeze(bool hasMoveInput, bool grounded)
	{
		bool still =
			grounded &&
			!hasMoveInput &&
			_horizontalVelocity.magnitude < freezeSpeedThreshold;

		if (still)
			_stillTimer += Time.deltaTime;
		else
			_stillTimer = 0f;

		IsFrozen = _stillTimer >= freezeDelay;
	}

	private void OnLanded(float fallSpeed)
	{
		float t = Mathf.Clamp01(fallSpeed / landingDipMaxFallSpeed);
		_landingOffset = -landingDipAmount * t;
	}

	private void HandleCameraBob(bool grounded)
	{
		// Bob strength follows the actual speed, so it ramps up with acceleration
		// and fades out during the skid instead of popping on/off with the key.
		float speed01 = walkSpeed > 0f
			? Mathf.Clamp01(_horizontalVelocity.magnitude / walkSpeed)
			: 0f;
		float targetWeight = grounded ? speed01 : 0f;
		_bobWeight = Mathf.Lerp(_bobWeight, targetWeight, Time.deltaTime * bobSmoothness);

		// Blend walk <-> sprint values instead of switching on the frame sprint starts
		float blend = Time.deltaTime * bobBlendSpeed;
		_currentBobSpeed = Mathf.Lerp(_currentBobSpeed, IsSprinting ? sprintBobSpeed : walkBobSpeed, blend);
		_currentBobAmount = Mathf.Lerp(_currentBobAmount, IsSprinting ? sprintBobAmount : walkBobAmount, blend);

		// Step rate follows speed too, so no stepping while standing or skidding to a stop
		_bobTimer += Time.deltaTime * _currentBobSpeed * speed01;

		// Vertical: -cos(2t) = two smooth dips per cycle (one per step), no sharp kink.
		// Roll: one gentle sway per cycle.
		float bobY = -Mathf.Cos(_bobTimer * 2f) * _currentBobAmount * _bobWeight;
		float targetRoll = Mathf.Sin(_bobTimer) * bobRollAmount * _bobWeight;

		_landingOffset = Mathf.Lerp(_landingOffset, 0f, Time.deltaTime * landingRecoverSpeed);

		// Final smoothing pass on everything that moves the camera
		Vector3 targetOffset = new Vector3(0f, bobY + _landingOffset, 0f);
		_cameraOffset = Vector3.SmoothDamp(_cameraOffset, targetOffset, ref _cameraOffsetVelocity, bobSmoothTime);
		_bobRoll = Mathf.SmoothDamp(_bobRoll, targetRoll, ref _bobRollVelocity, bobSmoothTime);

		playerCamera.transform.localPosition = _cameraStartLocalPos + _cameraOffset;
	}


	public float GetStaminaNormalized()
	{
		return CurrentStamina / maxStamina;
	}
}

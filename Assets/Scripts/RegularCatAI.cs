using UnityEngine;

// Regular cat brain. Movement goes through CatPatrol (NavMeshAgent).
//
//   Idle (patrol) --seen--> Confused (strikes) / Chasing
//   Chasing --lost sight--> Searching --nothing found--> Idle
//   Chasing --reached player--> Catching (player loses a life) --> Idle
public class RegularCatAI : MonoBehaviour
{
    public enum RegularCatState
    {
        Idle,
        Confused,
        Chasing,
        Searching,
        Catching
    }

    [Header("Detection")]
    public VisionSensor vision;
    public float unmaskedDetectionDelay = 2f;
    [Tooltip("Detection speed multiplier while the player is frozen (standing still).")]
    public float frozenDetectionMultiplier = 0.35f;
    [Tooltip("Detection speed multiplier while searching (the cat is on alert).")]
    public float searchDetectionMultiplier = 2f;

    [Header("Strike System")]
    public int maxStrikes = 3;
    public int currentStrikes = 0;

    [Header("Confusion")]
    public float confusedDuration = 2f;

    [Header("Chase")]
    public float chaseSpeed = 4f;
    [Tooltip("How often (seconds) the chase path is recalculated.")]
    public float repathInterval = 0.2f;
    [Tooltip("Seconds the cat keeps tracking the player's real position after losing sight, so it follows around corners.")]
    public float trackAfterLostSight = 0.5f;
    [Tooltip("Seconds without seeing the player before the chase turns into a search.")]
    public float loseSightTime = 2f;
    [Tooltip("Seconds the cat waits at the closest point when it can't reach the player (e.g. under furniture).")]
    public float unreachableGiveUpTime = 2f;

    [Header("Catch")]
    [Tooltip("Horizontal distance at which the cat catches the player.")]
    public float catchRange = 0.9f;
    [Tooltip("Seconds the catch takes before the cat goes back to patrolling.")]
    public float catchDuration = 1.2f;
    public float lungeSpeed = 4f;
    public float lungeTime = 0.2f;

    [Header("Search")]
    public float searchSpeed = 2.5f;
    [Tooltip("Random spots near the last known position the cat checks after reaching it.")]
    public int searchPointCount = 2;
    public float searchRadius = 4f;
    public float lookAroundTime = 1.5f;
    public float lookAroundAngle = 70f;
    [Tooltip("Hard limit on the whole search.")]
    public float maxSearchTime = 12f;
    public float turnSpeed = 6f;

    [Header("UI")]
    public GameObject questionMarkIcon;
    public GameObject chaseIcon;

    private static readonly int CatchHash = Animator.StringToHash("Catch");

    private Transform player;
    private PlayerController playerController;
    private PlayerMask playerMask;
    private PlayerLives playerLives;
    private CatPatrol patrol;

    private RegularCatState state = RegularCatState.Idle;
    public RegularCatState State => state;

    private float detectionTimer;
    private float confusedTimer;
    private bool playerWasMaskedLastFrame;

    // Chase
    private Vector3 lastKnownPosition;
    private float lostSightTimer;
    private float repathTimer;
    private float unreachableTimer;

    // Search
    private float searchTimer;
    private int searchPointsLeft;
    private float lookTimer;
    private float lookBaseYaw;

    // Catch
    private float catchTimer;
    private float lungeTimer;

    void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();
        player = playerController.transform;
        playerMask = playerController.GetComponent<PlayerMask>();
        playerLives = playerController.GetComponent<PlayerLives>();
        patrol = GetComponent<CatPatrol>();

        if (playerLives == null)
            Debug.LogWarning("[RegularCatAI] No PlayerLives on the player: catches won't cost lives.");
        else
        {
            playerLives.OnCaught += HandlePlayerCaught;
            playerLives.OnGameOver += HandleGameOver;
        }

        EnterIdle();
    }

    void OnDestroy()
    {
        if (playerLives != null)
        {
            playerLives.OnCaught -= HandlePlayerCaught;
            playerLives.OnGameOver -= HandleGameOver;
        }
    }

    void Update()
    {
        switch (state)
        {
            case RegularCatState.Idle:
                HandleDetection(1f);
                break;

            case RegularCatState.Confused:
                HandleConfused();
                break;

            case RegularCatState.Chasing:
                HandleChase();
                break;

            case RegularCatState.Searching:
                HandleSearch();
                break;

            case RegularCatState.Catching:
                HandleCatching();
                break;
        }

        playerWasMaskedLastFrame = playerMask.IsMasked;
    }

    // ================= DETECTION =================
    void HandleDetection(float alertMultiplier)
    {
        if (!vision.CanSeeTarget())
        {
            detectionTimer = 0f;
            return;
        }

        // Mask removed in vision -> instant chase
        if (playerWasMaskedLastFrame && !playerMask.IsMasked)
        {
            TriggerChase();
            return;
        }

        if (!playerMask.IsMasked)
        {
            float rate = playerController.IsFrozen ? frozenDetectionMultiplier : 1f;
            detectionTimer += Time.deltaTime * rate * alertMultiplier;
            if (detectionTimer >= unmaskedDetectionDelay)
                TriggerChase();
        }
        else
        {
            detectionTimer = 0f;

            if (!playerWasMaskedLastFrame)
                AddStrike();
        }
    }

    void AddStrike()
    {
        currentStrikes++;

        if (currentStrikes >= maxStrikes)
            TriggerChase();
        else
            EnterConfusedState();
    }

    // ================= CONFUSED =================
    void EnterConfusedState()
    {
        state = RegularCatState.Confused;
        confusedTimer = confusedDuration;

        patrol.StopPatrol();
        SetIcons(question: true, chase: false);
    }

    void HandleConfused()
    {
        confusedTimer -= Time.deltaTime;
        if (confusedTimer <= 0f)
            EnterIdle();
    }

    // ================= CHASE =================
    void TriggerChase()
    {
        state = RegularCatState.Chasing;
        detectionTimer = 0f;
        lostSightTimer = 0f;
        repathTimer = 0f;
        unreachableTimer = 0f;
        lastKnownPosition = player.position;

        patrol.StopPatrol();
        SetIcons(question: false, chase: true);
    }

    void HandleChase()
    {
        if (!player)
            return;

        bool seen = vision.CanSeeTarget();
        lostSightTimer = seen ? 0f : lostSightTimer + Time.deltaTime;

        // While seen (and briefly after) chase the real position; then only the last known one.
        if (seen || lostSightTimer < trackAfterLostSight)
            lastKnownPosition = player.position;

        if (!seen && lostSightTimer >= loseSightTime)
        {
            EnterSearch();
            return;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            patrol.MoveTo(lastKnownPosition, chaseSpeed);
        }

        if (TryCatchPlayer())
            return;

        // The player is somewhere the cat can't path to (e.g. under furniture):
        // wait at the closest point, staring at them, then give up and search.
        if (patrol.PathIsPartial && patrol.HasArrived)
        {
            patrol.FaceDirection(player.position - transform.position, turnSpeed);
            unreachableTimer += Time.deltaTime;
            if (unreachableTimer >= unreachableGiveUpTime)
                EnterSearch();
        }
        else
        {
            unreachableTimer = 0f;
        }
    }

    // ================= CATCH =================
    bool TryCatchPlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        float vertical = Mathf.Abs(toPlayer.y);
        toPlayer.y = 0f;

        if (toPlayer.magnitude > catchRange || vertical > 1.5f)
            return false;

        // No catching through walls.
        Vector3 from = transform.position + Vector3.up * 0.3f;
        Vector3 to = player.position + Vector3.up * 0.1f;
        if (Physics.Linecast(from, to, vision.obstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        // Player just respawned / already being caught: doesn't count.
        if (playerLives != null && !playerLives.TryCatch())
            return false;

        EnterCatching();
        return true;
    }

    void EnterCatching()
    {
        state = RegularCatState.Catching;
        catchTimer = catchDuration;
        lungeTimer = lungeTime;

        // StopPatrol, not just Stop: PlayerLives.OnCaught fires (and resumes patrol via
        // HandlePlayerCaught) inside TryCatch, before this cat switches to Catching.
        patrol.StopPatrol();
        transform.rotation = Quaternion.LookRotation(Flat(player.position - transform.position));
        SetIcons(question: false, chase: true);

        Animator animator = patrol.Animator;
        if (animator != null && HasParameter(animator, CatchHash))
            animator.SetTrigger(CatchHash);

        Debug.Log($"[RegularCatAI] {name} caught the player!");
    }

    void HandleCatching()
    {
        // Short pounce forward (stays on the NavMesh).
        if (lungeTimer > 0f)
        {
            lungeTimer -= Time.deltaTime;
            patrol.Agent.Move(transform.forward * lungeSpeed * Time.deltaTime);
        }

        catchTimer -= Time.deltaTime;
        if (catchTimer <= 0f)
        {
            currentStrikes = 0;
            EnterIdle();
        }
    }

    // ================= SEARCH =================
    void EnterSearch()
    {
        state = RegularCatState.Searching;
        searchTimer = maxSearchTime;
        searchPointsLeft = searchPointCount;
        lookTimer = 0f;
        detectionTimer = 0f;

        patrol.StopPatrol();
        SetIcons(question: true, chase: false);

        if (!patrol.MoveTo(lastKnownPosition, searchSpeed))
            NextSearchPoint();
    }

    void HandleSearch()
    {
        // On alert: spots the player faster than while patrolling.
        HandleDetection(searchDetectionMultiplier);
        if (state != RegularCatState.Searching)
            return;

        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            EnterIdle();
            return;
        }

        if (lookTimer > 0f)
        {
            lookTimer -= Time.deltaTime;
            LookAround();
            if (lookTimer <= 0f)
                NextSearchPoint();
            return;
        }

        if (patrol.HasArrived)
        {
            patrol.Stop();
            lookTimer = lookAroundTime;
            lookBaseYaw = transform.eulerAngles.y;
        }
    }

    void LookAround()
    {
        // Sweep left, right, back to centre over lookAroundTime.
        float t = 1f - lookTimer / lookAroundTime;
        float yaw = lookBaseYaw + Mathf.Sin(t * Mathf.PI * 2f) * lookAroundAngle;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.Euler(0f, yaw, 0f),
            Time.deltaTime * turnSpeed
        );
    }

    void NextSearchPoint()
    {
        while (searchPointsLeft > 0)
        {
            searchPointsLeft--;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * searchRadius;
                Vector3 candidate = lastKnownPosition + new Vector3(offset.x, 0f, offset.y);
                if (patrol.MoveTo(candidate, searchSpeed) && !patrol.PathIsPartial)
                    return;
            }
        }

        EnterIdle();
    }

    // ================= IDLE / EVENTS =================
    void EnterIdle()
    {
        state = RegularCatState.Idle;
        detectionTimer = 0f;
        SetIcons(question: false, chase: false);
        patrol.ResumePatrol();
    }

    // Any catch resets the situation: other chasing/searching cats calm down
    // instead of hunting the spot the player was teleported away from.
    void HandlePlayerCaught()
    {
        if (state == RegularCatState.Chasing || state == RegularCatState.Searching)
            EnterIdle();
    }

    void HandleGameOver()
    {
        patrol.StopPatrol();
        SetIcons(question: false, chase: false);
        enabled = false;
    }

    // ================= HELPERS =================
    void SetIcons(bool question, bool chase)
    {
        if (questionMarkIcon != null)
            questionMarkIcon.SetActive(question);

        if (chaseIcon != null)
            chaseIcon.SetActive(chase);
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v : Vector3.forward;
    }

    static bool HasParameter(Animator animator, int hash)
    {
        foreach (var p in animator.parameters)
            if (p.nameHash == hash)
                return true;
        return false;
    }
}

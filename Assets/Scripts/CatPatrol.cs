using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Moves the cat with a NavMeshAgent (so it paths around walls) and patrols.
//   Territory: wander between the PatrolNetwork waypoints near where the cat started,
//              preferring the ones it visited longest ago; look around at room points.
//   Route:     walk patrolPoints in order (authored set-piece patrols).
// RegularCatAI also uses MoveTo/Stop to drive chase and search movement.
[RequireComponent(typeof(NavMeshAgent))]
public class CatPatrol : MonoBehaviour
{
    public enum PatrolMode
    {
        Territory,
        Route
    }

    [Header("Patrol")]
    [Tooltip("Territory: wander between nearby PatrolNetwork waypoints. Route: walk Patrol Points in order.")]
    public PatrolMode mode = PatrolMode.Territory;
    [Tooltip("Used in Route mode, and as a fallback when there is no PatrolNetwork.")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;
    [Tooltip("Wait at route points and room waypoints.")]
    public float waitAtPointTime = 1.5f;

    [Header("Territory")]
    [Tooltip("Waypoints within this walking distance (m) of the cat's start form its territory.")]
    public float territoryRadius = 30f;
    [Tooltip("Territory always has at least this many waypoints (the nearest ones), if the level has them.")]
    public int minTerritoryPoints = 4;
    [Tooltip("Next waypoint is picked at random among this many least-recently-visited ones.")]
    public int choiceCount = 3;
    public float junctionWaitTime = 0.2f;
    public float lookAroundAngle = 60f;
    public float lookTurnSpeed = 5f;

    [Header("NavMesh")]
    [Tooltip("How far from a target point to look for the NavMesh.")]
    public float pointSnapRadius = 2f;
    [Tooltip("Distance at which a destination counts as reached.")]
    public float arriveDistance = 0.2f;

    [Header("Animation")]
    [Tooltip("Agent speed above which the walk animation plays.")]
    public float walkAnimThreshold = 0.1f;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

    private NavMeshAgent agent;
    private Animator animator;

    // Shared patrol state
    private float waitTimer;
    private float waitDuration;
    private bool isWaiting;
    private bool lookAroundWhileWaiting;
    private float lookBaseYaw;
    private bool hasDestination;

    // Route
    private int currentIndex;

    // Territory
    private bool territoryResolved;
    private bool useTerritory;
    private Vector3 home;
    private List<PatrolWaypoint> territory;
    private PatrolWaypoint currentWaypoint;
    private readonly Dictionary<PatrolWaypoint, float> lastVisit = new Dictionary<PatrolWaypoint, float>();

    public NavMeshAgent Agent => agent;
    public Animator Animator => animator;
    public bool IsPatrolling { get; private set; } = true;
    public IReadOnlyList<PatrolWaypoint> Territory => territory;

    public bool HasArrived =>
        agent.isOnNavMesh &&
        !agent.pathPending &&
        (!agent.hasPath || agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, arriveDistance));

    // True when the target can't be fully reached (e.g. the player is under furniture).
    public bool PathIsPartial => agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete;

    bool HasRoute => patrolPoints != null && patrolPoints.Length > 0;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        home = transform.position;
    }

    void Update()
    {
        UpdateWalkAnimation();

        if (!IsPatrolling || !agent.isOnNavMesh)
            return;

        // Decided on the first patrol frame, so generated levels have their waypoints and NavMesh by then.
        if (!territoryResolved)
            ResolvePatrolMode();

        if (!useTerritory && !HasRoute)
            return;

        TickPatrol();
    }

    // ================= PATROL =================
    void ResolvePatrolMode()
    {
        territoryResolved = true;
        useTerritory = false;

        if (mode == PatrolMode.Route && HasRoute)
            return;

        var network = FindAnyObjectByType<PatrolNetwork>();
        if (network != null)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            territory = network.BuildTerritory(home, filter, territoryRadius, minTerritoryPoints);
            useTerritory = territory.Count >= 2;
        }

        if (!useTerritory && !HasRoute)
            Debug.LogWarning($"[CatPatrol] {name}: no PatrolNetwork waypoints nearby and no patrol points; the cat will stand still.");
    }

    void TickPatrol()
    {
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (lookAroundWhileWaiting)
                LookAround();

            if (waitTimer <= 0f)
            {
                isWaiting = false;
                if (!useTerritory)
                    currentIndex = (currentIndex + 1) % patrolPoints.Length;
                GoToNext();
            }
            return;
        }

        if (!hasDestination)
        {
            GoToNext();
            return;
        }

        if (HasArrived)
            BeginWait();
    }

    void BeginWait()
    {
        isWaiting = true;
        lookBaseYaw = transform.eulerAngles.y;

        if (useTerritory && currentWaypoint != null)
        {
            lastVisit[currentWaypoint] = Time.time;
            lookAroundWhileWaiting = currentWaypoint.LookAround;
            waitDuration = currentWaypoint.LookAround ? waitAtPointTime : junctionWaitTime;
        }
        else
        {
            lookAroundWhileWaiting = false;
            waitDuration = waitAtPointTime;
        }

        waitTimer = waitDuration;
    }

    void GoToNext()
    {
        if (useTerritory)
            GoToNextWaypoint();
        else
            GoToCurrentRoutePoint();
    }

    void GoToCurrentRoutePoint()
    {
        Transform point = patrolPoints[currentIndex];
        if (point != null && MoveTo(point.position, moveSpeed))
            return;

        // Missing or off-NavMesh point: skip it (one per frame, so a fully broken list can't hang).
        currentIndex = (currentIndex + 1) % patrolPoints.Length;
        hasDestination = false;
    }

    void GoToNextWaypoint()
    {
        // Least recently visited first (never visited = oldest); then random among the first few.
        var candidates = new List<PatrolWaypoint>(territory.Count);
        foreach (var waypoint in territory)
            if (waypoint != null && waypoint != currentWaypoint)
                candidates.Add(waypoint);

        if (candidates.Count == 0)
            return;

        candidates.Sort((a, b) => LastVisit(a).CompareTo(LastVisit(b)));
        var next = candidates[Random.Range(0, Mathf.Min(choiceCount, candidates.Count))];

        if (MoveTo(next.transform.position, moveSpeed))
        {
            currentWaypoint = next;
        }
        else
        {
            // Unreachable right now: count it as visited so another one is picked next frame.
            lastVisit[next] = Time.time;
            hasDestination = false;
        }
    }

    float LastVisit(PatrolWaypoint waypoint) =>
        lastVisit.TryGetValue(waypoint, out float time) ? time : float.NegativeInfinity;

    void LookAround()
    {
        // Sweep left, right and back to centre over the wait.
        float t = waitDuration > 0f ? 1f - waitTimer / waitDuration : 1f;
        float yaw = lookBaseYaw + Mathf.Sin(t * Mathf.PI * 2f) * lookAroundAngle;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.Euler(0f, yaw, 0f),
            Time.deltaTime * lookTurnSpeed
        );
    }

    void UpdateWalkAnimation()
    {
        if (animator != null)
            animator.SetBool(IsWalkingHash, agent.velocity.sqrMagnitude > walkAnimThreshold * walkAnimThreshold);
    }

    // ===== Movement API (used by RegularCatAI) =====

    // Paths to the NavMesh point nearest worldPos. Returns false if there is no NavMesh nearby.
    public bool MoveTo(Vector3 worldPos, float speed)
    {
        if (!agent.isOnNavMesh || !TrySampleNavMesh(worldPos, out Vector3 target))
            return false;

        agent.speed = speed;
        agent.isStopped = false;
        hasDestination = agent.SetDestination(target);
        return hasDestination;
    }

    public void Stop()
    {
        hasDestination = false;
        if (agent.isOnNavMesh)
            agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    public bool TrySampleNavMesh(Vector3 worldPos, out Vector3 result)
    {
        // Sample at the cat's own height so points authored at the wrong Y (or a roof above)
        // still snap to the floor the cat is walking on.
        Vector3 probe = new Vector3(worldPos.x, transform.position.y, worldPos.z);
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

        if (NavMesh.SamplePosition(probe, out NavMeshHit hit, pointSnapRadius, filter))
        {
            result = hit.position;
            return true;
        }

        result = worldPos;
        return false;
    }

    // Turns the cat in place (only while it isn't walking a path).
    public void FaceDirection(Vector3 direction, float turnSpeed)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction),
            Time.deltaTime * turnSpeed
        );
    }

    // ===== External Control =====
    public void StopPatrol()
    {
        IsPatrolling = false;
        isWaiting = false;
        Stop();
    }

    public void ResumePatrol()
    {
        IsPatrolling = true;
        isWaiting = false;
        hasDestination = false;
        agent.speed = moveSpeed;
    }

    void OnDrawGizmosSelected()
    {
        if (territory == null)
            return;

        Gizmos.color = new Color(1f, 0.3f, 0.8f);
        foreach (var waypoint in territory)
            if (waypoint != null)
                Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, waypoint.transform.position + Vector3.up * 0.1f);
    }
}

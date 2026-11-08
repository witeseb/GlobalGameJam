using System.Collections.Generic;
using UnityEngine;

// Grid-based level generator.
// 1. Places rooms of random size on a grid (with spacing between them).
// 2. Connects rooms: minimum spanning tree + a few extra links for loops.
// 3. Carves hallways with A* (prefers straight runs and reusing existing hallways).
// 4. Builds rooms from tile prefabs and hallways from the Hall_* pieces.
// 5. Fills the spawn point containers used by PlayerSpawnManager / CheeseSpawner / ExitSpawner.
// Runs before those scripts' Awake so their containers are already populated.
[DefaultExecutionOrder(-100)]
public class LevelGenerator : MonoBehaviour
{
	public enum CellType { Empty, Room, Hallway }

	[System.Serializable]
	public class Room
	{
		public int id;
		public RectInt rect;

		public Vector2Int Center => new Vector2Int(rect.x + rect.width / 2, rect.y + rect.height / 2);
		public int Area => rect.width * rect.height;
	}

	// Compass order N, E, S, W. Bit i of a hallway mask = open towards Compass[i].
	static readonly Vector2Int[] Compass = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
	// Tile_Wall is authored on the SOUTH edge; yaw that puts it on N / E / S / W (same order as Compass).
	static readonly float[] WallYaw = { 180f, 270f, 0f, 90f };

	[Header("Grid")]
	[SerializeField, Min(4)] private int gridWidth = 30;
	[SerializeField, Min(4)] private int gridHeight = 30;
	[SerializeField] private float cellSize = 2.5f;
	[SerializeField] private float ceilingHeight = 2.5f;
	[Tooltip("How far the wall mesh sits outside its cell edge (matches Tile_Wall).")]
	[SerializeField] private float wallOutset = 0.1f;

	[Header("Rooms")]
	[SerializeField, Min(1)] private int roomCount = 8;
	[SerializeField] private Vector2Int minRoomSize = new Vector2Int(2, 2);
	[SerializeField] private Vector2Int maxRoomSize = new Vector2Int(6, 5);
	[Tooltip("Empty cells kept between rooms, leaving space for hallways.")]
	[SerializeField, Min(1)] private int roomSpacing = 2;
	[SerializeField, Min(1)] private int maxPlacementAttempts = 500;

	[Header("Hallways")]
	[Tooltip("Chance to add a non-essential link between nearby rooms (creates loops).")]
	[SerializeField, Range(0f, 1f)] private float extraConnectionChance = 0.25f;
	[Tooltip("Extra path cost for each turn. Higher = straighter hallways.")]
	[SerializeField, Min(0f)] private float turnCost = 2f;
	[Tooltip("Cost of walking through an existing hallway cell (lower = more merging).")]
	[SerializeField, Min(0.1f)] private float hallwayReuseCost = 0.5f;
	[Tooltip("Extra cost for a new hallway cell touching a room wall.")]
	[SerializeField, Min(0f)] private float roomAdjacentPenalty = 1.5f;
	[Tooltip("Extra cost for a new hallway cell running right next to an existing hallway.")]
	[SerializeField, Min(0f)] private float parallelHallwayPenalty = 1f;

	[Header("Generation")]
	[Tooltip("0 = new random layout every run.")]
	[SerializeField] private int seed = 0;
	[SerializeField] private bool generateOnAwake = true;

	[Header("Room Tile Prefabs")]
	[SerializeField] private GameObject floorPrefab;
	[SerializeField] private GameObject roofPrefab;
	[SerializeField] private GameObject wallPrefab;
	[SerializeField] private GameObject columnPrefab;

	[Header("Hallway Piece Prefabs (fallback: built from tiles)")]
	[SerializeField] private GameObject hallStraight;
	[SerializeField] private GameObject hallCorner;
	[SerializeField] private GameObject hallT;
	[SerializeField] private GameObject hallCross;
	[SerializeField] private GameObject hallDeadEnd;

	[Header("Lights")]
	[SerializeField] private GameObject ceilingLightPrefab;
	[Tooltip("Roughly one room light per this many cells along each axis.")]
	[SerializeField, Min(1)] private int lightSpacing = 3;
	[Tooltip("Minimum distance (cells) between hallway lights.")]
	[SerializeField, Min(1)] private int hallwayLightSpacing = 4;

	[Header("Spawn Point Containers (filled on generate)")]
	[SerializeField] private Transform playerSpawnContainer;
	[SerializeField] private Transform exitPointContainer;
	[SerializeField] private Transform bigCheeseContainer;
	[SerializeField] private Transform smallCheeseContainer;
	[SerializeField, Min(0)] private int smallCheesePoints = 10;
	[SerializeField] private float playerSpawnHeight = 1f;
	[SerializeField] private float exitHeight = 0.6f;
	[SerializeField] private float cheeseHeight = 0f;

	private CellType[,] grid;
	private int[,] roomIds;
	private readonly List<Room> rooms = new List<Room>();
	private readonly List<Vector2Int> connections = new List<Vector2Int>(); // pairs of room ids
	private readonly HashSet<(Vector2Int, Vector2Int)> doorways = new HashSet<(Vector2Int, Vector2Int)>();
	private Transform generatedRoot;
	private System.Random rng;

	public IReadOnlyList<Room> Rooms => rooms;
	public IReadOnlyList<Vector2Int> Connections => connections;
	public CellType[,] Grid => grid;
	public float CellSize => cellSize;
	public int LastSeed { get; private set; }
	public Room StartRoom { get; private set; }
	public Room ExitRoom { get; private set; }
	public Room CheeseRoom { get; private set; }
	public int UnreachableRooms { get; private set; }

	bool UseHallPieces => hallStraight && hallCorner && hallT && hallCross && hallDeadEnd;

	void Awake()
	{
		if (generateOnAwake)
			Generate();
	}

	// ================= GENERATE =================
	[ContextMenu("Generate")]
	public void Generate()
	{
		Clear();

		LastSeed = seed != 0 ? seed : System.Environment.TickCount;
		rng = new System.Random(LastSeed);

		grid = new CellType[gridWidth, gridHeight];
		roomIds = new int[gridWidth, gridHeight];
		for (int x = 0; x < gridWidth; x++)
			for (int y = 0; y < gridHeight; y++)
				roomIds[x, y] = -1;

		PlaceRooms();
		ConnectRooms();
		CarveHallways();

		generatedRoot = new GameObject("Generated").transform;
		generatedRoot.SetParent(transform, false);

		BuildGeometry();
		PlaceLights();
		PlaceSpawnPoints();

		if (Application.isPlaying)
			StaticBatchingUtility.Combine(generatedRoot.gameObject);

		Debug.Log($"[LevelGenerator] seed {LastSeed}: {rooms.Count}/{roomCount} rooms, {connections.Count} connections, {doorways.Count} doorways");
		if (UnreachableRooms > 0)
			Debug.LogWarning($"[LevelGenerator] {UnreachableRooms} room(s) are not reachable from the start room.");
	}

	[ContextMenu("Clear")]
	public void Clear()
	{
		rooms.Clear();
		connections.Clear();
		doorways.Clear();
		StartRoom = ExitRoom = CheeseRoom = null;
		UnreachableRooms = 0;

		var existing = transform.Find("Generated");
		if (existing != null)
			DestroyObject(existing.gameObject);
		generatedRoot = null;

		ClearChildren(playerSpawnContainer);
		ClearChildren(exitPointContainer);
		ClearChildren(bigCheeseContainer);
		ClearChildren(smallCheeseContainer);
	}

	// ================= ROOMS =================
	void PlaceRooms()
	{
		for (int attempt = 0; attempt < maxPlacementAttempts && rooms.Count < roomCount; attempt++)
		{
			int w = rng.Next(minRoomSize.x, maxRoomSize.x + 1);
			int h = rng.Next(minRoomSize.y, maxRoomSize.y + 1);

			// Keep a one-cell border around the grid so hallways can run along the edge.
			if (w > gridWidth - 2 || h > gridHeight - 2)
				continue;

			var rect = new RectInt(rng.Next(1, gridWidth - w), rng.Next(1, gridHeight - h), w, h);
			if (!FitsWithSpacing(rect))
				continue;

			var room = new Room { id = rooms.Count, rect = rect };
			rooms.Add(room);

			foreach (var c in rect.allPositionsWithin)
			{
				grid[c.x, c.y] = CellType.Room;
				roomIds[c.x, c.y] = room.id;
			}
		}

		if (rooms.Count < roomCount)
			Debug.LogWarning($"[LevelGenerator] Only fit {rooms.Count}/{roomCount} rooms. Increase the grid size or reduce room size/spacing.");
	}

	bool FitsWithSpacing(RectInt rect)
	{
		foreach (var r in rooms)
		{
			var padded = new RectInt(r.rect.x - roomSpacing, r.rect.y - roomSpacing,
				r.rect.width + roomSpacing * 2, r.rect.height + roomSpacing * 2);
			if (padded.Overlaps(rect))
				return false;
		}
		return true;
	}

	// ================= CONNECTIONS =================
	// Prim's minimum spanning tree over room centers, then a few extra links between near neighbours.
	void ConnectRooms()
	{
		int n = rooms.Count;
		if (n < 2)
			return;

		var inTree = new bool[n];
		var bestDist = new float[n];
		var bestFrom = new int[n];
		for (int i = 0; i < n; i++) { bestDist[i] = float.MaxValue; bestFrom[i] = -1; }
		bestDist[0] = 0f;

		for (int step = 0; step < n; step++)
		{
			int next = -1;
			for (int i = 0; i < n; i++)
				if (!inTree[i] && (next < 0 || bestDist[i] < bestDist[next]))
					next = i;

			inTree[next] = true;
			if (bestFrom[next] >= 0)
				connections.Add(new Vector2Int(bestFrom[next], next));

			for (int i = 0; i < n; i++)
			{
				if (inTree[i])
					continue;
				float d = RoomDistance(rooms[next], rooms[i]);
				if (d < bestDist[i]) { bestDist[i] = d; bestFrom[i] = next; }
			}
		}

		// Extra links: each room may also link to one of its two nearest neighbours.
		for (int i = 0; i < n; i++)
		{
			var nearest = new List<int>();
			for (int j = 0; j < n; j++)
				if (j != i)
					nearest.Add(j);
			nearest.Sort((a, b) => RoomDistance(rooms[i], rooms[a]).CompareTo(RoomDistance(rooms[i], rooms[b])));

			for (int k = 0; k < Mathf.Min(2, nearest.Count); k++)
			{
				int j = nearest[k];
				if (HasConnection(i, j) || rng.NextDouble() >= extraConnectionChance)
					continue;
				connections.Add(new Vector2Int(i, j));
			}
		}

		// Carve short links first so longer ones can reuse their hallways.
		connections.Sort((a, b) => RoomDistance(rooms[a.x], rooms[a.y]).CompareTo(RoomDistance(rooms[b.x], rooms[b.y])));
	}

	bool HasConnection(int a, int b)
	{
		foreach (var c in connections)
			if ((c.x == a && c.y == b) || (c.x == b && c.y == a))
				return true;
		return false;
	}

	// ================= HALLWAYS =================
	void CarveHallways()
	{
		foreach (var c in connections)
		{
			var path = FindPath(rooms[c.x], rooms[c.y]);
			if (path == null)
			{
				Debug.LogWarning($"[LevelGenerator] No path between room {c.x} and room {c.y}");
				continue;
			}

			foreach (var cell in path)
				if (grid[cell.x, cell.y] == CellType.Empty)
					grid[cell.x, cell.y] = CellType.Hallway;

			// A doorway is every step of the path that crosses between a room and a hallway.
			for (int i = 1; i < path.Count; i++)
			{
				var a = path[i - 1];
				var b = path[i];
				if ((grid[a.x, a.y] == CellType.Room) != (grid[b.x, b.y] == CellType.Room))
					doorways.Add(EdgeKey(a, b));
			}
		}
	}

	// A* over (cell, arrival direction) so turns can be penalised.
	// Rooms other than the two being connected are impassable.
	List<Vector2Int> FindPath(Room from, Room to)
	{
		int stateCount = gridWidth * gridHeight * 4;
		var cost = new float[stateCount];
		var prev = new int[stateCount];
		for (int i = 0; i < stateCount; i++) { cost[i] = float.MaxValue; prev[i] = -1; }

		const float minStep = 0.2f; // cheapest possible step (inside an endpoint room); keeps the heuristic admissible
		Vector2Int start = from.Center;
		Vector2Int goal = to.Center;
		var open = new MinHeap();

		for (int d = 0; d < 4; d++)
		{
			int s = StateIndex(start, d);
			cost[s] = 0f;
			open.Push(Heuristic(start, goal, minStep), s);
		}

		int goalState = -1;
		while (open.Count > 0)
		{
			open.Pop(out float f, out int state);
			int cellIndex = state / 4;
			int dir = state % 4;
			var cell = new Vector2Int(cellIndex % gridWidth, cellIndex / gridWidth);
			float g = cost[state];

			if (f > g + Heuristic(cell, goal, minStep) + 0.0001f)
				continue; // stale entry

			if (cell == goal) { goalState = state; break; }

			bool insideEndpoint = IsEndpointRoom(cell, from, to);
			for (int nd = 0; nd < 4; nd++)
			{
				var next = cell + Compass[nd];
				if (!InBounds(next))
					continue;

				float step = StepCost(next, from, to);
				if (step < 0f)
					continue;
				if (nd != dir && !insideEndpoint)
					step += turnCost;

				int ns = StateIndex(next, nd);
				float ng = g + step;
				if (ng < cost[ns])
				{
					cost[ns] = ng;
					prev[ns] = state;
					open.Push(ng + Heuristic(next, goal, minStep), ns);
				}
			}
		}

		if (goalState < 0)
			return null;

		var path = new List<Vector2Int>();
		for (int s = goalState; s >= 0; s = prev[s])
		{
			int ci = s / 4;
			var c = new Vector2Int(ci % gridWidth, ci / gridWidth);
			if (path.Count == 0 || path[path.Count - 1] != c)
				path.Add(c);
		}
		path.Reverse();
		return path;
	}

	float StepCost(Vector2Int c, Room from, Room to)
	{
		switch (grid[c.x, c.y])
		{
			case CellType.Room:
				return IsEndpointRoom(c, from, to) ? 0.2f : -1f;
			case CellType.Hallway:
				return hallwayReuseCost;
			default:
				float cost = 1f;
				bool nearRoom = false, nearHall = false;
				foreach (var d in Compass)
				{
					var n = c + d;
					if (!InBounds(n))
						continue;
					if (grid[n.x, n.y] == CellType.Room) nearRoom = true;
					else if (grid[n.x, n.y] == CellType.Hallway) nearHall = true;
				}
				if (nearRoom) cost += roomAdjacentPenalty;
				if (nearHall) cost += parallelHallwayPenalty;
				return cost;
		}
	}

	bool IsEndpointRoom(Vector2Int c, Room from, Room to)
	{
		int id = roomIds[c.x, c.y];
		return id >= 0 && (id == from.id || id == to.id);
	}

	int StateIndex(Vector2Int c, int dir) => (c.y * gridWidth + c.x) * 4 + dir;

	static float Heuristic(Vector2Int a, Vector2Int b, float minStep) =>
		(Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y)) * minStep;

	static (Vector2Int, Vector2Int) EdgeKey(Vector2Int a, Vector2Int b) =>
		(a.x < b.x || (a.x == b.x && a.y < b.y)) ? (a, b) : (b, a);

	// ================= GEOMETRY =================
	// Room cells: floor + roof, and a wall on every edge that isn't connected. A room owns the wall
	// between itself and an unconnected hallway, so the hallway side doesn't add a second one.
	// Hallway cells: one Hall_* piece chosen from the cell's open sides (or tiles if pieces are missing).
	// Columns: one per grid vertex where any walls (room or hallway) turn or end - corners, junctions
	// and doorway sides. All columns come from this single pass so they never overlap.
	void BuildGeometry()
	{
		var vertices = new Dictionary<Vector2Int, VertexWalls>();
		bool pieces = UseHallPieces;

		for (int x = 0; x < gridWidth; x++)
		{
			for (int y = 0; y < gridHeight; y++)
			{
				var type = grid[x, y];
				if (type == CellType.Empty)
					continue;

				var cell = new Vector2Int(x, y);
				Transform parent = GetCellParent(cell);
				Vector3 center = CellToWorld(cell);

				if (type == CellType.Hallway && pieces)
				{
					BuildHallPiece(cell, center, parent, vertices);
					continue;
				}

				Spawn(floorPrefab, center, Quaternion.identity, parent);
				Spawn(roofPrefab, center, Quaternion.identity, parent);

				for (int d = 0; d < 4; d++)
				{
					var neighbour = cell + Compass[d];
					if (IsConnected(cell, neighbour))
						continue;
					// Hallway tiles leave the shared edge to the room's wall.
					if (type == CellType.Hallway && InBounds(neighbour) && grid[neighbour.x, neighbour.y] == CellType.Room)
						continue;

					Spawn(wallPrefab, center, Quaternion.Euler(0f, WallYaw[d], 0f), parent);
					RegisterWall(vertices, cell, Compass[d]);
				}
			}
		}

		var columnParent = new GameObject("Columns").transform;
		columnParent.SetParent(generatedRoot, false);

		foreach (var kv in vertices)
		{
			var v = kv.Value;
			bool straightRun = (v.horizontal == 2 && v.vertical == 0 && v.normalZ != 0)
				|| (v.vertical == 2 && v.horizontal == 0 && v.normalX != 0);
			if (straightRun)
				continue;

			Vector3 pos = VertexToWorld(kv.Key)
				+ new Vector3(System.Math.Sign(v.normalX) * wallOutset, 0f, System.Math.Sign(v.normalZ) * wallOutset);
			Spawn(columnPrefab, pos, Quaternion.identity, columnParent);
		}
	}

	// Places a Hall_* piece, then hands its corners over to the shared column pass so every grid vertex
	// gets at most one column: the piece's own columns are removed, and so is any wall facing a room
	// (the room already has a wall on that edge). The remaining walls are registered like room walls.
	void BuildHallPiece(Vector2Int cell, Vector3 center, Transform parent, Dictionary<Vector2Int, VertexWalls> vertices)
	{
		int mask = HallwayMask(cell);
		if (!PickHallPiece(mask, out var prefab, out float yaw))
			return;

		var piece = Spawn(prefab, center, Quaternion.Euler(0f, yaw, 0f), parent);
		if (piece == null)
			return;

		for (int i = piece.transform.childCount - 1; i >= 0; i--)
		{
			var child = piece.transform.GetChild(i);
			if (child.name.StartsWith("Column"))
				DestroyImmediate(child.gameObject);
		}

		int rotationSteps = Mathf.RoundToInt(yaw / 90f) & 3;
		for (int d = 0; d < 4; d++)
		{
			if ((mask & (1 << d)) != 0)
				continue; // open side, no wall

			var neighbour = cell + Compass[d];
			if (InBounds(neighbour) && grid[neighbour.x, neighbour.y] == CellType.Room)
			{
				// The piece was rotated, so world side d is the piece's local side (d - rotation).
				var wall = piece.transform.Find("Wall_" + SideNames[(d - rotationSteps + 4) & 3]);
				if (wall != null)
					DestroyImmediate(wall.gameObject);
				continue;
			}

			RegisterWall(vertices, cell, Compass[d]);
		}
	}

	static readonly string[] SideNames = { "N", "E", "S", "W" };

	int HallwayMask(Vector2Int cell)
	{
		int mask = 0;
		for (int i = 0; i < 4; i++)
			if (IsConnected(cell, cell + Compass[i]))
				mask |= 1 << i;
		return mask;
	}

	// Each piece is authored with fixed openings (bit 0 = N, 1 = E, 2 = S, 3 = W).
	// Rotating the piece by +90 degrees shifts every opening one step clockwise.
	bool PickHallPiece(int mask, out GameObject prefab, out float yaw)
	{
		var pieces = new (GameObject prefab, int openings)[]
		{
			(hallDeadEnd, 0b0001),  // N
			(hallStraight, 0b0101), // N, S
			(hallCorner, 0b0011),   // N, E
			(hallT, 0b1011),        // N, E, W
			(hallCross, 0b1111),
		};

		foreach (var p in pieces)
		{
			for (int k = 0; k < 4; k++)
			{
				int rotated = ((p.openings << k) | (p.openings >> (4 - k))) & 0b1111;
				if (rotated == mask)
				{
					prefab = p.prefab;
					yaw = k * 90f;
					return true;
				}
			}
		}

		prefab = null;
		yaw = 0f;
		return false;
	}

	struct VertexWalls
	{
		public int horizontal; // walls running along X (south/north edges)
		public int vertical;   // walls running along Z (west/east edges)
		public int normalX;    // summed outward normals; 0 when walls face opposite ways
		public int normalZ;
	}

	void RegisterWall(Dictionary<Vector2Int, VertexWalls> vertices, Vector2Int cell, Vector2Int dir)
	{
		// Cell (x,y) spans vertices (x..x+1, y..y+1).
		Vector2Int a, b;
		if (dir == Vector2Int.down) { a = new Vector2Int(cell.x, cell.y); b = new Vector2Int(cell.x + 1, cell.y); }
		else if (dir == Vector2Int.up) { a = new Vector2Int(cell.x, cell.y + 1); b = new Vector2Int(cell.x + 1, cell.y + 1); }
		else if (dir == Vector2Int.left) { a = new Vector2Int(cell.x, cell.y); b = new Vector2Int(cell.x, cell.y + 1); }
		else { a = new Vector2Int(cell.x + 1, cell.y); b = new Vector2Int(cell.x + 1, cell.y + 1); }

		AddToVertex(vertices, a, dir);
		AddToVertex(vertices, b, dir);
	}

	static void AddToVertex(Dictionary<Vector2Int, VertexWalls> vertices, Vector2Int vertex, Vector2Int dir)
	{
		vertices.TryGetValue(vertex, out var v);
		if (dir.y != 0) { v.horizontal++; v.normalZ += dir.y; }
		else { v.vertical++; v.normalX += dir.x; }
		vertices[vertex] = v;
	}

	public bool IsConnected(Vector2Int a, Vector2Int b)
	{
		if (!InBounds(a) || !InBounds(b))
			return false;

		var ta = grid[a.x, a.y];
		var tb = grid[b.x, b.y];
		if (ta == CellType.Empty || tb == CellType.Empty)
			return false;

		if (ta == CellType.Room && tb == CellType.Room)
			return roomIds[a.x, a.y] == roomIds[b.x, b.y];

		if (ta == CellType.Hallway && tb == CellType.Hallway)
			return true;

		return doorways.Contains(EdgeKey(a, b));
	}

	Transform GetCellParent(Vector2Int cell)
	{
		string name = grid[cell.x, cell.y] == CellType.Room ? $"Room_{roomIds[cell.x, cell.y]}" : "Hallways";
		var parent = generatedRoot.Find(name);
		if (parent == null)
		{
			parent = new GameObject(name).transform;
			parent.SetParent(generatedRoot, false);
		}
		return parent;
	}

	// ================= LIGHTS =================
	void PlaceLights()
	{
		if (ceilingLightPrefab == null)
			return;

		foreach (var room in rooms)
		{
			int nx = Mathf.Max(1, Mathf.RoundToInt(room.rect.width / (float)lightSpacing));
			int nz = Mathf.Max(1, Mathf.RoundToInt(room.rect.height / (float)lightSpacing));
			Transform parent = generatedRoot.Find($"Room_{room.id}");

			for (int i = 0; i < nx; i++)
			{
				for (int j = 0; j < nz; j++)
				{
					float gx = room.rect.x + room.rect.width * (i + 0.5f) / nx;
					float gz = room.rect.y + room.rect.height * (j + 0.5f) / nz;
					Vector3 pos = transform.position + new Vector3(gx * cellSize, ceilingHeight, gz * cellSize);
					Spawn(ceilingLightPrefab, pos, Quaternion.identity, parent);
				}
			}
		}

		var hallParent = generatedRoot.Find("Hallways");
		var placed = new List<Vector2Int>();
		for (int x = 0; x < gridWidth; x++)
		{
			for (int y = 0; y < gridHeight; y++)
			{
				if (grid[x, y] != CellType.Hallway)
					continue;

				var c = new Vector2Int(x, y);
				bool tooClose = false;
				foreach (var p in placed)
				{
					if (Mathf.Max(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) < hallwayLightSpacing)
					{
						tooClose = true;
						break;
					}
				}
				if (tooClose)
					continue;

				placed.Add(c);
				Spawn(ceilingLightPrefab, CellToWorld(c) + Vector3.up * ceilingHeight, Quaternion.identity, hallParent);
			}
		}
	}

	// ================= SPAWN POINTS =================
	void PlaceSpawnPoints()
	{
		if (rooms.Count == 0)
			return;

		StartRoom = rooms[rng.Next(rooms.Count)];
		var fromStart = WalkDistances(StartRoom.Center);

		// Exit goes in the reachable room farthest (by walking distance) from the start.
		ExitRoom = StartRoom;
		int best = -1;
		UnreachableRooms = 0;
		foreach (var r in rooms)
		{
			int d = fromStart[r.Center.x, r.Center.y];
			if (d < 0) { UnreachableRooms++; continue; }
			if (d > best) { best = d; ExitRoom = r; }
		}

		// Big cheese goes in the room farthest from both start and exit.
		var fromExit = WalkDistances(ExitRoom.Center);
		CheeseRoom = ExitRoom;
		best = -1;
		foreach (var r in rooms)
		{
			if (r == StartRoom || r == ExitRoom || fromStart[r.Center.x, r.Center.y] < 0)
				continue;
			int d = Mathf.Min(fromStart[r.Center.x, r.Center.y], fromExit[r.Center.x, r.Center.y]);
			if (d > best) { best = d; CheeseRoom = r; }
		}

		AddPoint(playerSpawnContainer, "PlayerSpawnPoint", RoomCenterWorld(StartRoom, playerSpawnHeight));
		AddPoint(exitPointContainer, "ExitPoint", RoomCenterWorld(ExitRoom, exitHeight));
		AddPoint(bigCheeseContainer, "BigCheesePoint", RoomCenterWorld(CheeseRoom, cheeseHeight));

		// Small cheese: random distinct reachable cells in rooms other than the start room.
		var cells = new List<Vector2Int>();
		foreach (var r in rooms)
		{
			if (r == StartRoom && rooms.Count > 1)
				continue;
			foreach (var c in r.rect.allPositionsWithin)
				if (fromStart[c.x, c.y] >= 0)
					cells.Add(c);
		}
		cells.Remove(CheeseRoom.Center);

		int count = Mathf.Min(smallCheesePoints, cells.Count);
		for (int i = 0; i < count; i++)
		{
			int index = rng.Next(cells.Count);
			AddPoint(smallCheeseContainer, "SmallCheesePoint", CellToWorld(cells[index]) + Vector3.up * cheeseHeight);
			cells.RemoveAt(index);
		}
	}

	// BFS step distance over connected cells; -1 = unreachable.
	int[,] WalkDistances(Vector2Int start)
	{
		var dist = new int[gridWidth, gridHeight];
		for (int x = 0; x < gridWidth; x++)
			for (int y = 0; y < gridHeight; y++)
				dist[x, y] = -1;

		var queue = new Queue<Vector2Int>();
		dist[start.x, start.y] = 0;
		queue.Enqueue(start);
		while (queue.Count > 0)
		{
			var c = queue.Dequeue();
			foreach (var d in Compass)
			{
				var n = c + d;
				if (!IsConnected(c, n) || dist[n.x, n.y] >= 0)
					continue;
				dist[n.x, n.y] = dist[c.x, c.y] + 1;
				queue.Enqueue(n);
			}
		}
		return dist;
	}

	static float RoomDistance(Room a, Room b) => Vector2Int.Distance(a.Center, b.Center);

	Vector3 RoomCenterWorld(Room room, float height)
	{
		Vector2 c = room.rect.center;
		return transform.position + new Vector3(c.x * cellSize, height, c.y * cellSize);
	}

	void AddPoint(Transform container, string pointName, Vector3 position)
	{
		if (container == null)
			return;

		var point = new GameObject(pointName).transform;
		point.SetParent(container, false);
		point.SetPositionAndRotation(position, Quaternion.Euler(0f, rng.Next(4) * 90f, 0f));
	}

	// ================= HELPERS =================
	public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < gridWidth && c.y < gridHeight;

	public Vector3 CellToWorld(Vector2Int c) =>
		transform.position + new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);

	Vector3 VertexToWorld(Vector2Int v) =>
		transform.position + new Vector3(v.x * cellSize, 0f, v.y * cellSize);

	GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
	{
		if (prefab == null)
			return null;
		return Instantiate(prefab, position, rotation, parent);
	}

	void ClearChildren(Transform container)
	{
		if (container == null)
			return;
		for (int i = container.childCount - 1; i >= 0; i--)
			DestroyObject(container.GetChild(i).gameObject);
	}

	static void DestroyObject(GameObject go)
	{
		if (Application.isPlaying)
		{
			// Detach first so childCount updates immediately (spawners read it in their Awake).
			go.transform.SetParent(null);
			Destroy(go);
		}
		else
		{
			DestroyImmediate(go);
		}
	}

	// Small binary min-heap for A* (Unity's .NET profile has no PriorityQueue).
	class MinHeap
	{
		private readonly List<(float priority, int value)> items = new List<(float, int)>();

		public int Count => items.Count;

		public void Push(float priority, int value)
		{
			items.Add((priority, value));
			int i = items.Count - 1;
			while (i > 0)
			{
				int parent = (i - 1) / 2;
				if (items[parent].priority <= items[i].priority)
					break;
				(items[parent], items[i]) = (items[i], items[parent]);
				i = parent;
			}
		}

		public void Pop(out float priority, out int value)
		{
			(priority, value) = items[0];
			int last = items.Count - 1;
			items[0] = items[last];
			items.RemoveAt(last);

			int i = 0;
			while (true)
			{
				int left = i * 2 + 1, right = left + 1, smallest = i;
				if (left < items.Count && items[left].priority < items[smallest].priority) smallest = left;
				if (right < items.Count && items[right].priority < items[smallest].priority) smallest = right;
				if (smallest == i)
					break;
				(items[smallest], items[i]) = (items[i], items[smallest]);
				i = smallest;
			}
		}
	}

	// ================= DEBUG =================
	void OnDrawGizmosSelected()
	{
		Vector3 origin = transform.position;
		Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
		Gizmos.DrawWireCube(origin + new Vector3(gridWidth * cellSize, 0f, gridHeight * cellSize) * 0.5f,
			new Vector3(gridWidth * cellSize, 0f, gridHeight * cellSize));

		foreach (var r in rooms)
		{
			Gizmos.color = r == StartRoom ? Color.blue : r == ExitRoom ? Color.green : r == CheeseRoom ? Color.yellow : Color.cyan;
			Vector2 c = r.rect.center;
			Gizmos.DrawWireCube(origin + new Vector3(c.x * cellSize, 0.1f, c.y * cellSize),
				new Vector3(r.rect.width * cellSize, 0.2f, r.rect.height * cellSize));
		}

		Gizmos.color = Color.magenta;
		foreach (var c in connections)
		{
			if (c.x >= rooms.Count || c.y >= rooms.Count)
				continue;
			Gizmos.DrawLine(CellToWorld(rooms[c.x].Center) + Vector3.up * 0.2f, CellToWorld(rooms[c.y].Center) + Vector3.up * 0.2f);
		}
	}
}

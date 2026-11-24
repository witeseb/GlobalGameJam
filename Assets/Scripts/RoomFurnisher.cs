using System.Collections.Generic;
using UnityEngine;

// Furnishes one generated room at a time, following the layout patterns of the hand-built Level scene:
// - Every furniture model faces +Z at yaw 0. Wall pieces (fridge, cabinets) stand with their back to a
//   wall facing into the room, and often sit in a corner.
// - TableB (corner counter) sits in a corner: yaw 0 fills the (+X,+Z) corner, each +90 moves it clockwise.
// - TableA sits in the middle of medium/large rooms with chairs on both long sides facing it.
// - Cupboard is a kitchen island in large rooms.
// - Ingredients: scattered on top of TableA / TableB, and stocked (stacked) on the island's lower shelf.
// Doorways and spawn points are kept clear, and a placement is undone if it would cut any doorway or
// spawn point off from the others.
public class RoomFurnisher : MonoBehaviour
{
	[System.Serializable]
	public class WallItem
	{
		public GameObject prefab;
		[Min(0f)] public float weight = 1f;
		[Range(0f, 1f), Tooltip("Chance to push it into a corner instead of a random spot along the wall.")]
		public float cornerChance = 0.5f;
	}

	public struct Door
	{
		public Vector2 point;  // world XZ, middle of the doorway on the wall line
		public Vector2 inward; // unit normal pointing into the room
	}

	public class Request
	{
		public Rect floor;   // world XZ rect of the room floor (cell edges)
		public float floorY;
		public float doorWidth;
		public List<Door> doors = new List<Door>();
		public List<Vector2> keepClear = new List<Vector2>(); // spawn points (world XZ)
		public Transform parent;
		public System.Random rng;
	}

	[Header("Wall furniture (back against the wall)")]
	[SerializeField] private List<WallItem> wallItems = new List<WallItem>();
	[Tooltip("Wall pieces attempted per room-perimeter cell.")]
	[SerializeField, Min(0f)] private float wallItemsPerPerimeterCell = 0.35f;

	[Header("Corner counter")]
	[SerializeField] private GameObject cornerCounter;
	[SerializeField, Range(0f, 1f)] private float cornerCounterChance = 0.5f;

	[Header("Dining table set")]
	[SerializeField] private GameObject tablePrefab;
	[SerializeField] private GameObject chairPrefab;
	[Tooltip("Chair distance from the table's long centre line.")]
	[SerializeField] private float chairSideOffset = 1.62f;
	[SerializeField] private float chairSpacing = 1.75f;
	[SerializeField, Min(0)] private int chairsPerSide = 3;
	[SerializeField] private float headChairOffset = 3.22f;
	[SerializeField, Range(0f, 1f)] private float headChairChance = 0.3f;
	[Tooltip("Smallest room side (in cells) that gets a table set.")]
	[SerializeField, Min(1)] private int minCellsForTable = 3;

	[Header("Kitchen island")]
	[SerializeField] private GameObject islandPrefab;
	[Tooltip("Smallest room side (in cells) that can get an island instead of a table.")]
	[SerializeField, Min(1)] private int minCellsForIsland = 4;
	[SerializeField, Range(0f, 1f)] private float islandChance = 0.4f;

	[System.Serializable]
	public class Ingredient
	{
		public GameObject prefab;
		[Min(0f), Tooltip("How often it is picked for a table top. 0 = never.")]
		public float onTableWeight = 1f;
		[Min(0f), Tooltip("How often it is picked for the shelf under the island. 0 = never.")]
		public float underIslandWeight = 0f;
		[Min(1), Tooltip("Under the island it can be stacked up to this many high.")]
		public int maxStack = 1;
		[Tooltip("Height of each stacked copy as a fraction of the item's height (soft bags squash, < 1).")]
		public float stackStep = 1f;
		[Min(1), Tooltip("On tables it can come in a small group of this many (wine bottles).")]
		public int maxCluster = 1;
		[Tooltip("A crate: may get a tomato or cabbage inside it.")]
		public bool isCrate;
	}

	// Ingredient layout from the Level scene: tables (TableA, TableB) carry a scatter of boxes, crates,
	// veg and wine at random 15-degree yaws; the island (Cupboard) is stocked on its lower shelf with
	// stacked rice bags and boxes, and only has a bottle or two on top.
	[Header("Ingredients")]
	[SerializeField] private List<Ingredient> ingredients = new List<Ingredient>();
	[Tooltip("Placed inside crates.")]
	[SerializeField] private List<GameObject> crateFillers = new List<GameObject>();
	[SerializeField, Range(0f, 1f)] private float crateFillChance = 0.6f;
	[SerializeField] private Vector2Int ingredientsOnTable = new Vector2Int(5, 10);
	[SerializeField] private Vector2Int ingredientsOnCornerCounter = new Vector2Int(6, 12);
	[SerializeField] private Vector2Int ingredientsOnIsland = new Vector2Int(0, 2);
	[SerializeField] private Vector2Int ingredientsUnderIsland = new Vector2Int(10, 16);
	[Tooltip("Ingredients stay this far from table edges.")]
	[SerializeField] private float tableEdgeMargin = 0.3f;
	[SerializeField] private float ingredientGap = 0.04f;

	[Header("Spacing")]
	[Tooltip("Gap kept from the wall line (clears the corner and doorway columns).")]
	[SerializeField] private float wallInset = 0.16f;
	[Tooltip("Depth kept free inside each doorway.")]
	[SerializeField] private float doorClearDepth = 1.5f;
	[Tooltip("Radius kept free around spawn points.")]
	[SerializeField] private float spawnClearRadius = 1f;
	[Tooltip("Half-width a cat needs to walk between doorways.")]
	[SerializeField] private float walkClearance = 0.4f;
	[SerializeField] private float gridResolution = 0.25f;
	[Tooltip("Spacing of the free floor spots returned for pickups.")]
	[SerializeField] private float freeSpotSpacing = 1f;

	private readonly Dictionary<(GameObject, int), (Vector3 offset, Vector3 size)> footprintCache =
		new Dictionary<(GameObject, int), (Vector3, Vector3)>();

	enum Decor { None, Table, CornerCounter, Island }

	struct Piece
	{
		public GameObject prefab;
		public float yaw;
		public Vector3 pivot;
		public Decor decor;
	}

	Request req;
	Occupancy occ;
	List<Vector2> mustConnect;
	int clearanceCells;

	// Places furniture in one room. Returns free floor spots (world) that are reachable from the doorways.
	public List<Vector3> Furnish(Request request)
	{
		req = request;
		var area = new Rect(req.floor.xMin + wallInset, req.floor.yMin + wallInset,
			req.floor.width - wallInset * 2f, req.floor.height - wallInset * 2f);
		occ = new Occupancy(area, gridResolution);
		clearanceCells = Mathf.CeilToInt(walkClearance / gridResolution);

		mustConnect = new List<Vector2>();
		foreach (var door in req.doors)
		{
			Vector2 along = new Vector2(door.inward.y, -door.inward.x);
			Vector2 center = door.point + door.inward * (doorClearDepth * 0.5f);
			Vector2 size = new Vector2(Mathf.Abs(along.x) * req.doorWidth + Mathf.Abs(door.inward.x) * doorClearDepth,
				Mathf.Abs(along.y) * req.doorWidth + Mathf.Abs(door.inward.y) * doorClearDepth);
			occ.Reserve(new Rect(center - size * 0.5f, size));
			mustConnect.Add(door.point + door.inward * (wallInset + gridResolution));
		}
		foreach (var p in req.keepClear)
		{
			occ.Reserve(new Rect(p - Vector2.one * spawnClearRadius, Vector2.one * spawnClearRadius * 2f));
			mustConnect.Add(p);
		}

		int cellsX = Mathf.RoundToInt(req.floor.width / req.doorWidth);
		int cellsZ = Mathf.RoundToInt(req.floor.height / req.doorWidth);

		PlaceCenterpiece(Mathf.Min(cellsX, cellsZ), req.floor.width >= req.floor.height);

		if (cornerCounter != null && req.rng.NextDouble() < cornerCounterChance)
			PlaceCornerCounter();

		int target = Mathf.RoundToInt((cellsX + cellsZ) * 2 * wallItemsPerPerimeterCell);
		int placed = 0;
		for (int attempt = 0; attempt < target * 6 && placed < target; attempt++)
			if (TryPlaceWallItem())
				placed++;

		var spots = new List<Vector3>();
		foreach (var p in occ.ReachableSpots(mustConnect, 1, Mathf.Max(1, Mathf.RoundToInt(freeSpotSpacing / gridResolution))))
			spots.Add(new Vector3(p.x, req.floorY, p.y));
		return spots;
	}

	// ================= CENTERPIECE =================
	void PlaceCenterpiece(int minCells, bool longAlongX)
	{
		bool canTable = tablePrefab != null && minCells >= minCellsForTable;
		bool canIsland = islandPrefab != null && minCells >= minCellsForIsland;
		if (!canTable && !canIsland)
			return;

		bool island = canIsland && (!canTable || req.rng.NextDouble() < islandChance);
		float baseYaw = longAlongX ? 0f : 90f;
		Vector2 roomCenter = req.floor.center;

		for (int attempt = 0; attempt < 10; attempt++)
		{
			float yaw = baseYaw + (req.rng.Next(2) == 0 ? 0f : 180f);
			Vector2 jitter = attempt == 0 ? Vector2.zero
				: new Vector2(RandomRange(-1.5f, 1.5f), RandomRange(-1.5f, 1.5f));
			Vector2 center = Snap(roomCenter + jitter);

			var pieces = island ? IslandPieces(center, yaw) : TablePieces(center, yaw);
			if (TryPlace(pieces))
				return;
		}
	}

	List<Piece> TablePieces(Vector2 center, float yaw)
	{
		var table = PieceAtBoundsCenter(tablePrefab, yaw, center);
		table.decor = Decor.Table;
		var pieces = new List<Piece> { table };
		if (chairPrefab == null)
			return pieces;

		var rot = Quaternion.Euler(0f, yaw, 0f);
		float first = -(chairsPerSide - 1) * 0.5f * chairSpacing;
		for (int i = 0; i < chairsPerSide; i++)
		{
			float x = first + i * chairSpacing;
			pieces.Add(ChairPiece(center, rot, new Vector3(x, 0f, chairSideOffset), yaw + 180f)); // faces -Z
			pieces.Add(ChairPiece(center, rot, new Vector3(x, 0f, -chairSideOffset), yaw));      // faces +Z
		}

		if (req.rng.NextDouble() < headChairChance)
		{
			bool west = req.rng.Next(2) == 0;
			pieces.Add(ChairPiece(center, rot, new Vector3(west ? -headChairOffset : headChairOffset, 0f, 0f), yaw + (west ? 90f : 270f)));
		}
		return pieces;
	}

	Piece ChairPiece(Vector2 tableCenter, Quaternion tableRot, Vector3 localOffset, float yaw)
	{
		Vector3 o = tableRot * localOffset;
		return new Piece { prefab = chairPrefab, yaw = Mathf.Repeat(yaw, 360f), pivot = new Vector3(tableCenter.x + o.x, req.floorY, tableCenter.y + o.z) };
	}

	List<Piece> IslandPieces(Vector2 center, float yaw)
	{
		var island = PieceAtBoundsCenter(islandPrefab, yaw, center);
		island.decor = Decor.Island;
		return new List<Piece> { island };
	}

	// ================= CORNER COUNTER =================
	void PlaceCornerCounter()
	{
		// Corners in random order; yaw that fills each corner (measured from the Level scene).
		var corners = new List<(int sx, int sz, float yaw)> { (1, 1, 0f), (1, -1, 90f), (-1, -1, 180f), (-1, 1, 270f) };
		Shuffle(corners);
		foreach (var c in corners)
		{
			var fp = Footprint(cornerCounter, c.yaw);
			var area = occ.area;
			float cx = c.sx > 0 ? area.xMax - fp.size.x * 0.5f : area.xMin + fp.size.x * 0.5f;
			float cz = c.sz > 0 ? area.yMax - fp.size.z * 0.5f : area.yMin + fp.size.z * 0.5f;
			var counter = PieceAtBoundsCenter(cornerCounter, c.yaw, new Vector2(cx, cz));
			counter.decor = Decor.CornerCounter;
			if (TryPlace(new List<Piece> { counter }))
				return;
		}
	}

	// ================= WALL ITEMS =================
	bool TryPlaceWallItem()
	{
		var item = PickWallItem();
		if (item == null)
			return false;

		var area = occ.area;
		// Pick a wall weighted by its length. Inward normals: S(+Z), N(-Z), W(+X), E(-X).
		float total = (area.width + area.height) * 2f;
		float r = (float)req.rng.NextDouble() * total;
		Vector2 inward;
		if ((r -= area.width) < 0f) inward = Vector2.up;
		else if ((r -= area.width) < 0f) inward = Vector2.down;
		else if ((r -= area.height) < 0f) inward = Vector2.right;
		else inward = Vector2.left;

		float yaw = YawFacing(inward);
		var fp = Footprint(item.prefab, yaw);
		bool wallAlongX = inward.x == 0f;
		float along = wallAlongX ? fp.size.x : fp.size.z;
		float depth = wallAlongX ? fp.size.z : fp.size.x;
		float wallMin = wallAlongX ? area.xMin : area.yMin;
		float wallMax = wallAlongX ? area.xMax : area.yMax;
		if (along > wallMax - wallMin)
			return false;

		float start;
		if (req.rng.NextDouble() < item.cornerChance)
			start = req.rng.Next(2) == 0 ? wallMin : wallMax - along;
		else
			start = wallMin + Mathf.Floor(RandomRange(0f, wallMax - wallMin - along) / gridResolution) * gridResolution;

		Vector2 center;
		float alongCenter = start + along * 0.5f;
		if (wallAlongX)
			center = new Vector2(alongCenter, inward.y > 0f ? area.yMin + depth * 0.5f : area.yMax - depth * 0.5f);
		else
			center = new Vector2(inward.x > 0f ? area.xMin + depth * 0.5f : area.xMax - depth * 0.5f, alongCenter);

		return TryPlace(new List<Piece> { PieceAtBoundsCenter(item.prefab, yaw, center) });
	}

	WallItem PickWallItem()
	{
		float total = 0f;
		foreach (var w in wallItems)
			if (w.prefab != null) total += w.weight;
		if (total <= 0f)
			return null;

		float r = (float)req.rng.NextDouble() * total;
		foreach (var w in wallItems)
		{
			if (w.prefab == null) continue;
			if ((r -= w.weight) <= 0f) return w;
		}
		return null;
	}

	// Furniture faces +Z at yaw 0; this yaw makes it face the given direction (back to the opposite wall).
	static float YawFacing(Vector2 dir)
	{
		if (dir.y > 0.5f) return 0f;
		if (dir.x > 0.5f) return 90f;
		if (dir.y < -0.5f) return 180f;
		return 270f;
	}

	// ================= PLACEMENT =================
	bool TryPlace(List<Piece> pieces)
	{
		var rects = new List<Rect>();
		foreach (var p in pieces)
		{
			if (p.prefab == null)
				continue;
			var fp = Footprint(p.prefab, p.yaw);
			Vector3 c = p.pivot + fp.offset;
			var rect = new Rect(c.x - fp.size.x * 0.5f, c.z - fp.size.z * 0.5f, fp.size.x, fp.size.z);
			if (fp.size.y > 0.05f && !occ.CanPlace(rect))
				return false;
			rects.Add(rect);
		}

		var snapshot = occ.Snapshot();
		foreach (var rect in rects)
			occ.Mark(rect);

		if (!occ.AllConnected(mustConnect, clearanceCells))
		{
			occ.Restore(snapshot);
			return false;
		}

		foreach (var p in pieces)
		{
			if (p.prefab == null)
				continue;
			var go = Instantiate(p.prefab, p.pivot, PrefabRotation(p.prefab, p.yaw), req.parent);
			if (p.decor != Decor.None)
				Decorate(go, p.decor, p.yaw);
		}
		return true;
	}

	// ================= INGREDIENTS =================
	void Decorate(GameObject host, Decor decor, float hostYaw)
	{
		if (ingredients.Count == 0)
			return;

		// Surfaces are found by raycasting the host's own colliders, so L-shaped tops (TableB) and
		// the island's lower shelf are handled without hard-coded shapes.
		Physics.SyncTransforms();
		var colliders = host.GetComponentsInChildren<Collider>();
		var renderers = host.GetComponentsInChildren<Renderer>();
		if (colliders.Length == 0 || renderers.Length == 0)
			return;
		var bounds = renderers[0].bounds;
		foreach (var r in renderers)
			bounds.Encapsulate(r.bounds);

		switch (decor)
		{
			case Decor.Table:
				Scatter(colliders, bounds, hostYaw, false, ingredientsOnTable);
				break;
			case Decor.CornerCounter:
				Scatter(colliders, bounds, hostYaw, false, ingredientsOnCornerCounter);
				break;
			case Decor.Island:
				Scatter(colliders, bounds, hostYaw, false, ingredientsOnIsland);
				Scatter(colliders, bounds, hostYaw, true, ingredientsUnderIsland);
				break;
		}
	}

	void Scatter(Collider[] colliders, Bounds host, float hostYaw, bool under, Vector2Int countRange)
	{
		int target = req.rng.Next(Mathf.Min(countRange.x, countRange.y), Mathf.Max(countRange.x, countRange.y) + 1);
		if (target <= 0)
			return;

		// Rays for a top start above the host; rays for the shelf start just under the top slab.
		float rayStart = under ? host.max.y - 0.25f : host.max.y + 0.5f;
		float margin = under ? 0.05f : tableEdgeMargin;
		var region = new Rect(host.min.x + margin, host.min.z + margin, host.size.x - margin * 2f, host.size.z - margin * 2f);
		var placed = new List<Rect>();

		int count = 0;
		for (int attempt = 0; attempt < target * 10 && count < target; attempt++)
		{
			var item = PickIngredient(under);
			if (item == null)
				return;

			// Tables: any 15-degree yaw. Shelf: lined up with the island, nudged a little (as in Level).
			float yaw = under
				? hostYaw + req.rng.Next(2) * 180f + (req.rng.Next(5) - 2) * 5f
				: req.rng.Next(24) * 15f;

			if (!TryPlaceIngredient(item.prefab, yaw, RandomPointIn(region, item.prefab, yaw), colliders, rayStart, under, placed, out float surfaceY, out var rect))
				continue;
			count++;

			var fp = Footprint(item.prefab, yaw);
			if (under && item.maxStack > 1)
			{
				// Stack straight up while there is headroom below the top slab.
				float ceiling = HitY(colliders, new Vector3(rect.center.x, surfaceY + 0.02f, rect.center.y), Vector3.up, 2f, out float up) ? up - 0.03f : surfaceY + 1f;
				int stack = req.rng.Next(1, item.maxStack + 1);
				for (int i = 1; i < stack; i++)
				{
					float y = surfaceY + i * fp.size.y * item.stackStep;
					if (y + fp.size.y > ceiling)
						break;
					SpawnIngredient(item.prefab, new Vector3(rect.center.x - fp.offset.x, y, rect.center.y - fp.offset.z), yaw + (req.rng.Next(5) - 2) * 3f);
				}
			}

			if (item.isCrate && crateFillers.Count > 0 && req.rng.NextDouble() < crateFillChance)
			{
				var filler = crateFillers[req.rng.Next(crateFillers.Count)];
				if (filler != null)
					SpawnIngredient(filler, new Vector3(rect.center.x, surfaceY + 0.03f, rect.center.y), req.rng.Next(24) * 15f);
			}

			if (!under && item.maxCluster > 1)
			{
				// Small groups next to the first one (wine bottles stand 0.2-0.3 m apart in Level).
				int extra = req.rng.Next(item.maxCluster);
				for (int i = 0; i < extra; i++)
				{
					float angle = (float)req.rng.NextDouble() * Mathf.PI * 2f;
					float dist = Mathf.Max(fp.size.x, fp.size.z) + ingredientGap;
					var pos = rect.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
					float clusterYaw = req.rng.Next(24) * 15f;
					TryPlaceIngredient(item.prefab, clusterYaw, pos, colliders, rayStart, false, placed, out _, out _);
				}
			}
		}
	}

	bool TryPlaceIngredient(GameObject prefab, float yaw, Vector2 center, Collider[] colliders, float rayStart, bool under,
		List<Rect> placed, out float surfaceY, out Rect rect)
	{
		var fp = Footprint(prefab, yaw);
		rect = new Rect(center.x - fp.size.x * 0.5f, center.y - fp.size.z * 0.5f, fp.size.x, fp.size.z);
		surfaceY = 0f;

		var padded = new Rect(rect.xMin - ingredientGap, rect.yMin - ingredientGap, rect.width + ingredientGap * 2f, rect.height + ingredientGap * 2f);
		foreach (var other in placed)
			if (padded.Overlaps(other))
				return false;

		// Fully supported: the centre and all four corners must land on the same surface.
		float hx = rect.width * 0.45f, hz = rect.height * 0.45f;
		Vector2[] samples = { center, center + new Vector2(hx, hz), center + new Vector2(-hx, hz), center + new Vector2(hx, -hz), center + new Vector2(-hx, -hz) };
		for (int i = 0; i < samples.Length; i++)
		{
			if (!HitY(colliders, new Vector3(samples[i].x, rayStart, samples[i].y), Vector3.down, 3f, out float y))
				return false;
			if (i == 0) surfaceY = y;
			else if (Mathf.Abs(y - surfaceY) > 0.03f) return false;
		}
		if (under && surfaceY >= rayStart - 0.05f)
			return false;

		placed.Add(rect);
		SpawnIngredient(prefab, new Vector3(center.x - fp.offset.x, surfaceY, center.y - fp.offset.z), yaw);
		return true;
	}

	Vector2 RandomPointIn(Rect region, GameObject prefab, float yaw)
	{
		var fp = Footprint(prefab, yaw);
		float hx = fp.size.x * 0.5f, hz = fp.size.z * 0.5f;
		return new Vector2(RandomRange(region.xMin + hx, Mathf.Max(region.xMin + hx, region.xMax - hx)),
			RandomRange(region.yMin + hz, Mathf.Max(region.yMin + hz, region.yMax - hz)));
	}

	void SpawnIngredient(GameObject prefab, Vector3 pivot, float yaw) =>
		Instantiate(prefab, pivot, PrefabRotation(prefab, yaw), req.parent);

	// Nearest hit on the host's colliders along the ray.
	static bool HitY(Collider[] colliders, Vector3 origin, Vector3 dir, float distance, out float y)
	{
		y = 0f;
		float best = float.MaxValue;
		var ray = new Ray(origin, dir);
		foreach (var c in colliders)
		{
			if (c.Raycast(ray, out var hit, distance) && hit.distance < best)
			{
				best = hit.distance;
				y = hit.point.y;
			}
		}
		return best < float.MaxValue;
	}

	Ingredient PickIngredient(bool under)
	{
		float total = 0f;
		foreach (var i in ingredients)
			if (i.prefab != null) total += under ? i.underIslandWeight : i.onTableWeight;
		if (total <= 0f)
			return null;

		float r = (float)req.rng.NextDouble() * total;
		foreach (var i in ingredients)
		{
			if (i.prefab == null) continue;
			float w = under ? i.underIslandWeight : i.onTableWeight;
			if (w > 0f && (r -= w) <= 0f) return i;
		}
		return null;
	}

	Piece PieceAtBoundsCenter(GameObject prefab, float yaw, Vector2 boundsCenter)
	{
		var fp = Footprint(prefab, yaw);
		return new Piece
		{
			prefab = prefab,
			yaw = yaw,
			pivot = new Vector3(boundsCenter.x - fp.offset.x, req.floorY, boundsCenter.y - fp.offset.z),
		};
	}

	// Keeps the prefab's own X/Z tilt (these FBX models are authored with X = 270) and sets the yaw.
	static Quaternion PrefabRotation(GameObject prefab, float yaw)
	{
		var e = prefab.transform.rotation.eulerAngles;
		return Quaternion.Euler(e.x, yaw, e.z);
	}

	// World-space bounds of the prefab at this yaw: offset from pivot to bounds centre, and size.
	(Vector3 offset, Vector3 size) Footprint(GameObject prefab, float yaw)
	{
		int key = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f));
		if (footprintCache.TryGetValue((prefab, key), out var cached))
			return cached;

		var probe = Instantiate(prefab, new Vector3(0f, -1000f, 0f), PrefabRotation(prefab, key));
		var renderers = probe.GetComponentsInChildren<Renderer>();
		(Vector3, Vector3) result = (Vector3.zero, Vector3.zero);
		if (renderers.Length > 0)
		{
			var b = renderers[0].bounds;
			foreach (var r in renderers)
				b.Encapsulate(r.bounds);
			result = (b.center - probe.transform.position, b.size);
		}
		DestroyImmediate(probe);

		footprintCache[(prefab, key)] = result;
		return result;
	}

	public void ClearCache() => footprintCache.Clear();

	// ================= HELPERS =================
	float RandomRange(float min, float max) => min + (float)req.rng.NextDouble() * (max - min);

	Vector2 Snap(Vector2 p) =>
		new Vector2(Mathf.Round(p.x / gridResolution) * gridResolution, Mathf.Round(p.y / gridResolution) * gridResolution);

	void Shuffle<T>(List<T> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = req.rng.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}

	// Floor occupancy of one room on a fine grid, used to stop overlaps and keep doorways connected.
	class Occupancy
	{
		public readonly Rect area;
		readonly float res;
		readonly int w, h;
		bool[] occupied;
		readonly bool[] reserved;

		public Occupancy(Rect area, float res)
		{
			this.area = area;
			this.res = res;
			// Round up so the last row/column reaches the far walls (room sizes aren't multiples of res).
			w = Mathf.Max(1, Mathf.CeilToInt(area.width / res - 0.001f));
			h = Mathf.Max(1, Mathf.CeilToInt(area.height / res - 0.001f));
			occupied = new bool[w * h];
			reserved = new bool[w * h];
		}

		bool Range(Rect r, bool clamp, out int x0, out int y0, out int x1, out int y1)
		{
			x0 = Mathf.FloorToInt((r.xMin - area.xMin) / res + 0.001f);
			y0 = Mathf.FloorToInt((r.yMin - area.yMin) / res + 0.001f);
			x1 = Mathf.CeilToInt((r.xMax - area.xMin) / res - 0.001f) - 1;
			y1 = Mathf.CeilToInt((r.yMax - area.yMin) / res - 0.001f) - 1;
			if (clamp)
			{
				x0 = Mathf.Max(x0, 0); y0 = Mathf.Max(y0, 0);
				x1 = Mathf.Min(x1, w - 1); y1 = Mathf.Min(y1, h - 1);
				return x0 <= x1 && y0 <= y1;
			}
			// Items must stay inside the area (a hair of float error allowed).
			if (r.xMin < area.xMin - 0.01f || r.yMin < area.yMin - 0.01f || r.xMax > area.xMax + 0.01f || r.yMax > area.yMax + 0.01f)
				return false;
			x0 = Mathf.Max(x0, 0); y0 = Mathf.Max(y0, 0);
			x1 = Mathf.Min(x1, w - 1); y1 = Mathf.Min(y1, h - 1);
			return x0 <= x1 && y0 <= y1;
		}

		public bool CanPlace(Rect r)
		{
			if (!Range(r, false, out int x0, out int y0, out int x1, out int y1))
				return false;
			for (int y = y0; y <= y1; y++)
				for (int x = x0; x <= x1; x++)
					if (occupied[y * w + x] || reserved[y * w + x])
						return false;
			return true;
		}

		public void Mark(Rect r) => Fill(occupied, r);
		public void Reserve(Rect r) => Fill(reserved, r);

		void Fill(bool[] cells, Rect r)
		{
			if (!Range(r, true, out int x0, out int y0, out int x1, out int y1))
				return;
			for (int y = y0; y <= y1; y++)
				for (int x = x0; x <= x1; x++)
					cells[y * w + x] = true;
		}

		public bool[] Snapshot() => (bool[])occupied.Clone();
		public void Restore(bool[] snapshot) => occupied = snapshot;

		// Cells at least k cells away (square distance) from any occupied cell.
		bool[] Walkable(int k)
		{
			var rows = new bool[w * h];
			for (int y = 0; y < h; y++)
				for (int x = 0; x < w; x++)
				{
					bool any = false;
					for (int dx = Mathf.Max(0, x - k); dx <= Mathf.Min(w - 1, x + k) && !any; dx++)
						any = occupied[y * w + dx];
					rows[y * w + x] = any;
				}

			var walk = new bool[w * h];
			for (int y = 0; y < h; y++)
				for (int x = 0; x < w; x++)
				{
					bool any = false;
					for (int dy = Mathf.Max(0, y - k); dy <= Mathf.Min(h - 1, y + k) && !any; dy++)
						any = rows[dy * w + x];
					walk[y * w + x] = !any;
				}
			return walk;
		}

		int CellOf(Vector2 p)
		{
			int x = Mathf.Clamp(Mathf.FloorToInt((p.x - area.xMin) / res), 0, w - 1);
			int y = Mathf.Clamp(Mathf.FloorToInt((p.y - area.yMin) / res), 0, h - 1);
			return y * w + x;
		}

		bool[] Flood(bool[] walk, int start)
		{
			var seen = new bool[w * h];
			if (!walk[start])
				return seen;
			var queue = new Queue<int>();
			queue.Enqueue(start);
			seen[start] = true;
			while (queue.Count > 0)
			{
				int c = queue.Dequeue();
				int cx = c % w, cy = c / w;
				TryVisit(cx + 1, cy); TryVisit(cx - 1, cy); TryVisit(cx, cy + 1); TryVisit(cx, cy - 1);

				void TryVisit(int nx, int ny)
				{
					if (nx < 0 || ny < 0 || nx >= w || ny >= h) return;
					int n = ny * w + nx;
					if (seen[n] || !walk[n]) return;
					seen[n] = true;
					queue.Enqueue(n);
				}
			}
			return seen;
		}

		public bool AllConnected(List<Vector2> points, int k)
		{
			if (points.Count == 0)
				return true;
			var seen = Flood(Walkable(k), CellOf(points[0]));
			foreach (var p in points)
				if (!seen[CellOf(p)])
					return false;
			return true;
		}

		public List<Vector2> ReachableSpots(List<Vector2> from, int k, int step)
		{
			var spots = new List<Vector2>();
			var walk = Walkable(k);
			var seen = new bool[w * h];
			foreach (var p in from)
			{
				var s = Flood(walk, CellOf(p));
				for (int i = 0; i < s.Length; i++)
					seen[i] |= s[i];
			}
			for (int y = step / 2; y < h; y += step)
				for (int x = step / 2; x < w; x += step)
					if (seen[y * w + x])
						spots.Add(new Vector2(area.xMin + (x + 0.5f) * res, area.yMin + (y + 0.5f) * res));
			return spots;
		}
	}
}

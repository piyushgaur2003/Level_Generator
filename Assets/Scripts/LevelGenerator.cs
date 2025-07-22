using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
   [Header("Level Settings")]
    public int lvlWidth = 50;
    public int lvlHeight = 50;
    public int minRooms = 8;
    public int maxRooms = 15;
    public Vector2Int minRoomSize = new Vector2Int(4, 4);
    public Vector2Int maxRoomSize = new Vector2Int(10, 10);
    public int maxAttempts = 100;
    public int maxGenerationAttemps = 10;
    
    [Header("Organic Generation Settings")]
    public bool useOrganicGeneration = true;
    [Range(0f, 1f)] public float noiseThreshold = 0.5f;
    public float noiseScale = 0.1f;
    public int randomWalkSteps = 1000;
    [Range(0f, 1f)] public float randomWalkTurnChance = 0.3f;
    public int organicBlendIterations = 3;
    
    [Header("Generation Settings")]
    public bool generateOnStart = true;
    public int seed = 0;
    public bool useRandomSeed = true;
    
    [Header("Visual Settings")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject corridorPrefab;
    public Transform dungeonParent;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showRoomNumbers = false;
    
    // internal data
    private int[,] lvlGrid;
    private List<Room> rooms;
    private List<Corridor> corridors;
    private Room startRoom;

    private List<Room> allRooms = new List<Room>();
    private List<Corridor> allCorridors = new List<Corridor>();
    private Dictionary<Vector2Int, GameObject> floorPool = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> wallPool = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> corridorPool = new Dictionary<Vector2Int, GameObject>();
    
    // grid cell types
    const int EMPTY = 0;
    const int FLOOR = 1;
    const int WALL = 2;
    const int CORRIDOR = 3;
    
    void Start()
    {
        if (generateOnStart)
        {
            GenerateLevel();
        }
    }
    
    [ContextMenu("Generate New Dungeon")]
    public void GenerateLevel()
    {
        ClearDungeon();
        InitializeGenerator();
        
        bool success = false;
        int attempts = 0;
        
        while (!success && attempts < maxGenerationAttemps)
        {
            attempts++;
            success = TryGenerateLevel();

            if (!success)
            {
                // tried with a diff seed here
                if (useRandomSeed)
                    seed = Random.Range(0, 10000);
                else
                    seed++;

                Random.InitState(seed);
                ClearGenerationData();

                ReleaseRoomsAndCorridors();
            }
        }
        
        if (success)
        {
            allRooms.AddRange(rooms);
            allCorridors.AddRange(corridors);

            BuildDungeonMesh();
            if (showDebugInfo)
            {
                Debug.Log($"Dungeon generated successfully with {rooms.Count} rooms. Seed: {seed}, Attempts: {attempts}");
                LogMemoryUsage();
            }
        }
        else
        {
            Debug.LogWarning("Failed to generate dungeon after multiple attempts!");
        }
    }

    private void ReleaseRoomsAndCorridors()
    {
        if (rooms != null)
        {
            foreach (var room in rooms)
            {
                if (room != null)
                {
                    Room.Release(room);
                }
            }
            rooms.Clear();
        }

        if (corridors != null)
        {
            foreach (var corridor in corridors)
            {
                if (corridor != null)
                {
                    Corridor.Release(corridor);
                }
            }
            corridors.Clear();
        }
    }
    
    private void LogMemoryUsage()
    {
        Debug.Log($"Object Pool Status - Rooms: {Room.PoolCount}, Corridors: {Corridor.PoolCount}");
        Debug.Log($"Tile Pools - Floors: {floorPool.Count}, Walls: {wallPool.Count}, Corridors: {corridorPool.Count}");
    }

    private void InitializeGenerator()
    {
        if (useRandomSeed)
            seed = Random.Range(0, 10000);

        Random.InitState(seed);

        lvlGrid = new int[lvlWidth, lvlHeight];
        rooms = new List<Room>();
        corridors = new List<Corridor>();
        startRoom = null;
        
        floorPool ??= new Dictionary<Vector2Int, GameObject>();
        wallPool ??= new Dictionary<Vector2Int, GameObject>();
        corridorPool ??= new Dictionary<Vector2Int, GameObject>();
        allRooms ??= new List<Room>();
        allCorridors ??= new List<Corridor>();
    }
    
    private bool TryGenerateLevel()
    {
        if (!GenerateRooms()) return false;
        
        ConnectRooms();
        
        if (useOrganicGeneration)
        {
            ApplyPerlinNoise();
            RandomWalk();
            BlendOrganicWithStructured();
        }
        
        FillGrid();
        GenerateWalls();
        
        return true;
    }
    
    private void ApplyPerlinNoise()
    {
        if (lvlGrid == null || lvlGrid.GetLength(0) != lvlWidth || lvlGrid.GetLength(1) != lvlHeight)
        {
            lvlGrid = new int[lvlWidth, lvlHeight];
        }

        float offsetX = Random.Range(0f, 1000f);
        float offsetY = Random.Range(0f, 1000f);
        
        for (int x = 0; x < lvlWidth; x++)
        {
            for (int y = 0; y < lvlHeight; y++)
            {
                //calculating perlin noise value here
                float noiseValue = Mathf.PerlinNoise(
                    offsetX + x * noiseScale, 
                    offsetY + y * noiseScale
                );
                
                if (noiseValue > noiseThreshold)
                {
                    lvlGrid[x, y] = FLOOR;
                }
            }
        }
    }
    
    private void RandomWalk()
    {
        // walk in rand pos
        Vector2Int walkerPos = new Vector2Int(
            Random.Range(1, lvlWidth - 1),
            Random.Range(1, lvlHeight - 1)
        );
        
        for (int i = 0; i < randomWalkSteps; i++)
        {
            lvlGrid[walkerPos.x, walkerPos.y] = FLOOR;
            
            if (Random.value < randomWalkTurnChance)
            {
                walkerPos = GetNextRandomPosition(walkerPos);
            }
            else
            {
                walkerPos += GetDirectionVector(walkerPos);
            }
            
            walkerPos.x = Mathf.Clamp(walkerPos.x, 1, lvlWidth - 2);
            walkerPos.y = Mathf.Clamp(walkerPos.y, 1, lvlHeight - 2);
        }
    }
    
    private Vector2Int GetNextRandomPosition(Vector2Int currentPos)
    {
        List<Vector2Int> possibleDirections = new List<Vector2Int>();
        
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue; // skip curr pos
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > 1) continue; // skip diags
                
                int newX = currentPos.x + dx;
                int newY = currentPos.y + dy;
                
                if (newX > 0 && newX < lvlWidth - 1 && newY > 0 && newY < lvlHeight - 1)
                {
                    possibleDirections.Add(new Vector2Int(dx, dy));
                }
            }
        }
        
        if (possibleDirections.Count == 0)
            return currentPos;
            
        return currentPos + possibleDirections[Random.Range(0, possibleDirections.Count)];
    }
    
    private Vector2Int GetDirectionVector(Vector2Int pos)
    {
        return GetNextRandomPosition(pos) - pos;
    }
    
    private void BlendOrganicWithStructured()
    {
        for (int i = 0; i < organicBlendIterations; i++)
        {
            foreach (Room room in rooms)
            {
                RectInt bounds = room.GetExpandedBounds(1);
                List<Vector2Int> newRoomTiles = new List<Vector2Int>();
                
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    for (int y = bounds.yMin; y < bounds.yMax; y++)
                    {
                        if (IsValidPosition(x, y) && lvlGrid[x, y] == FLOOR)
                        {
                            newRoomTiles.Add(new Vector2Int(x, y));
                        }
                    }
                }
                
                foreach (Vector2Int tile in newRoomTiles)
                {
                    room.size = new Vector2Int(
                        Mathf.Max(room.size.x, tile.x - room.position.x + 1),
                        Mathf.Max(room.size.y, tile.y - room.position.y + 1)
                    );
                }
            }
            
            foreach (Corridor corridor in corridors)
            {
                List<Vector2Int> originalPath = new List<Vector2Int>(corridor.path);
                List<Vector2Int> newPathTiles = new List<Vector2Int>();
                
                foreach (Vector2Int point in originalPath)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int x = point.x + dx;
                            int y = point.y + dy;
                            
                            if (IsValidPosition(x, y) && lvlGrid[x, y] == FLOOR)
                            {
                                Vector2Int newTile = new Vector2Int(x, y);
                                if (!corridor.path.Contains(newTile) && !newPathTiles.Contains(newTile))
                                {
                                    newPathTiles.Add(newTile);
                                }
                            }
                        }
                    }
                }
                
                corridor.path.AddRange(newPathTiles);
            }
        }
    }
    
    private bool GenerateRooms()
    {
        int roomCount = Random.Range(minRooms, maxRooms + 1);
        int attempts = 0;
        
        while (rooms.Count < roomCount && attempts < maxAttempts)
        {
            attempts++;
            
            Vector2Int roomSize = new Vector2Int(
                Random.Range(minRoomSize.x, maxRoomSize.x + 1),
                Random.Range(minRoomSize.y, maxRoomSize.y + 1)
            );
            
            Vector2Int roomPosition = new Vector2Int(
                Random.Range(2, lvlWidth - roomSize.x - 2),
                Random.Range(2, lvlHeight - roomSize.y - 2)
            );

            Room newRoom = Room.Get(roomPosition, roomSize);
            
            bool overlaps = false;
            foreach (Room existingRoom in rooms)
            {
                if (newRoom.Overlaps(existingRoom, 2))
                {
                    overlaps = true;
                    Room.Release(newRoom);
                    break;
                }
            }
            
            if (!overlaps)
            {
                rooms.Add(newRoom);
            }
        }
        
        if (rooms.Count < minRooms)
        {
            return false;
        }
        
        if (rooms.Count > 0)
        {
            startRoom = rooms[0];
            startRoom.roomType = RoomType.Start;
        }
        
        return true;
    }
    
    private void ConnectRooms()
    {
        if (rooms.Count < 2) return;
        
        List<Room> connectedRooms = new List<Room> { rooms[0] };
        List<Room> unconnectedRooms = new List<Room>(rooms.Skip(1));
        
        while (unconnectedRooms.Count > 0)
        {
            Room closestConnected = null;
            Room closestUnconnected = null;
            float closestDistance = float.MaxValue;
            
            foreach (Room connected in connectedRooms)
            {
                foreach (Room unconnected in unconnectedRooms)
                {
                    float distance = Vector2Int.Distance(connected.GetCenter(), unconnected.GetCenter());
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestConnected = connected;
                        closestUnconnected = unconnected;
                    }
                }
            }
            
            if (closestConnected != null && closestUnconnected != null)
            {
                corridors.Add(Corridor.Get(closestConnected, closestUnconnected));
                connectedRooms.Add(closestUnconnected);
                unconnectedRooms.Remove(closestUnconnected);
            }
        }
        
        for (int i = 0; i < rooms.Count; i++)
        {
            for (int j = i + 1; j < rooms.Count; j++)
            {
                if (Random.Range(0f, 1f) < 0.2f) // 20% chances for extra connec
                {
                    if (!AreRoomsConnected(rooms[i], rooms[j]))
                    {
                        corridors.Add(Corridor.Get(rooms[i], rooms[j]));
                    }
                }
            }
        }
    }
    
    private bool AreRoomsConnected(Room a, Room b)
    {
        foreach (Corridor corridor in corridors)
        {
            if ((corridor.roomA == a && corridor.roomB == b) ||
                (corridor.roomA == b && corridor.roomB == a))
            {
                return true;
            }
        }
        return false;
    }
    
    private void FillGrid()
    {
        if (lvlGrid == null)
        {
            lvlGrid = new int[lvlWidth, lvlHeight];
        }

        if (!useOrganicGeneration)
        {
            for (int x = 0; x < lvlWidth; x++)
            {
                for (int y = 0; y < lvlHeight; y++)
                {
                    lvlGrid[x, y] = EMPTY;
                }
            }
        }

        foreach (Room room in rooms)
        {
            RectInt bounds = room.GetBounds();
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    if (IsValidPosition(x, y))
                    {
                        lvlGrid[x, y] = FLOOR;
                    }
                }
            }
        }

        foreach (Corridor corridor in corridors)
        {
            foreach (Vector2Int point in corridor.path)
            {
                if (IsValidPosition(point.x, point.y))
                {
                    if (lvlGrid[point.x, point.y] == EMPTY)
                    {
                        lvlGrid[point.x, point.y] = CORRIDOR;
                    }
                }
            }
        }
    }
    
    private void GenerateWalls()
    {
        for (int x = 0; x < lvlWidth; x++)
        {
            for (int y = 0; y < lvlHeight; y++)
            {
                if (lvlGrid[x, y] == FLOOR || lvlGrid[x, y] == CORRIDOR)
                {
                    CheckAndMarkWall(x-1, y);
                    CheckAndMarkWall(x+1, y);
                    CheckAndMarkWall(x, y-1);
                    CheckAndMarkWall(x, y+1);
                    
                    CheckAndMarkWall(x-1, y-1);
                    CheckAndMarkWall(x-1, y+1);
                    CheckAndMarkWall(x+1, y-1);
                    CheckAndMarkWall(x+1, y+1);
                }
            }
        }
    }
    
    private void CheckAndMarkWall(int x, int y)
    {
        if (IsValidPosition(x, y) && lvlGrid[x, y] == EMPTY)
        {
            lvlGrid[x, y] = WALL;
        }
    }
    
    private void BuildDungeonMesh()
    {
        if (dungeonParent == null)
        {
            GameObject dungeonGO = new GameObject("Generated Dungeon");
            dungeonParent = dungeonGO.transform;
        }

        Dictionary<Vector2Int, GameObject> newFloorPool = new Dictionary<Vector2Int, GameObject>();
        Dictionary<Vector2Int, GameObject> newWallPool = new Dictionary<Vector2Int, GameObject>();
        Dictionary<Vector2Int, GameObject> newCorridorPool = new Dictionary<Vector2Int, GameObject>();

        foreach (Transform child in dungeonParent)
        {
            DestroyImmediate(child.gameObject);
        }

        for (int x = 0; x < lvlWidth; x++)
        {
            for (int y = 0; y < lvlHeight; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                Vector3 worldPos = new Vector3(x, 0, y);
                
                switch (lvlGrid[x, y])
                {
                    case FLOOR:
                        HandleTile(pos, worldPos, floorPrefab, floorPool, newFloorPool, "Floor");
                        break;
                        
                    case WALL:
                        HandleTile(pos, worldPos, wallPrefab, wallPool, newWallPool, "Wall");
                        break;
                        
                    case CORRIDOR:
                        HandleTile(pos, worldPos, corridorPrefab ?? floorPrefab, corridorPool, newCorridorPool, "Corridor");
                        break;
                }
            }
        }
        
        // Destroy unused tiles
        CleanupUnusedTiles(floorPool, newFloorPool);
        CleanupUnusedTiles(wallPool, newWallPool);
        CleanupUnusedTiles(corridorPool, newCorridorPool);
        
        // Update pools
        floorPool = newFloorPool;
        wallPool = newWallPool;
        corridorPool = newCorridorPool;
    }

    private void HandleTile(Vector2Int pos, Vector3 worldPos, GameObject prefab, 
                          Dictionary<Vector2Int, GameObject> oldPool,
                          Dictionary<Vector2Int, GameObject> newPool, string namePrefix)
    {
        if (prefab == null) return;
        
        if (oldPool.TryGetValue(pos, out GameObject existingTile))
        {
            existingTile.transform.position = worldPos;
            existingTile.SetActive(true);
            newPool.Add(pos, existingTile);
            oldPool.Remove(pos);
        }
        else
        {
            GameObject tile = Instantiate(prefab, worldPos, Quaternion.identity, dungeonParent);
            tile.name = $"{namePrefix}_{pos.x}_{pos.y}";
            newPool.Add(pos, tile);
        }
    }

    private void CleanupUnusedTiles(Dictionary<Vector2Int, GameObject> oldPool, 
                                  Dictionary<Vector2Int, GameObject> newPool)
    {
        foreach (var kvp in oldPool)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        oldPool.Clear();
    }

    private void ClearDungeon()
    {
        if (dungeonParent != null)
        {
            foreach (Transform child in dungeonParent)
            {
                if (child != null)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        ReleaseRoomsAndCorridors();
        
        if (allRooms != null)
        {
            allRooms.Clear();
        }
        
        if (allCorridors != null)
        {
            allCorridors.Clear();
        }
        
        ClearTilePool(floorPool);
        ClearTilePool(wallPool);
        ClearTilePool(corridorPool);
    }
    
    private void ClearTilePool(Dictionary<Vector2Int, GameObject> pool)
    {
        if (pool != null)
        {
            foreach (var tile in pool.Values)
            {
                if (tile != null)
                {
                    DestroyImmediate(tile);
                }
            }
            pool.Clear();
        }
    }
    
    private void ClearGenerationData()
    {
        lvlGrid = null;
        rooms?.Clear();
        corridors?.Clear();
        startRoom = null;
    }

    private bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < lvlWidth && y >= 0 && y < lvlHeight;
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugInfo || rooms == null) return;
        
        // rooms
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            
            // colors based on the room type
            switch (room.roomType)
            {
                case RoomType.Start:
                    Gizmos.color = Color.green;
                    break;
                default:
                    Gizmos.color = Color.blue;
                    break;
            }
            
            Vector3 center = new Vector3(room.position.x + room.size.x / 2f, 0, room.position.y + room.size.y / 2f);
            Vector3 size = new Vector3(room.size.x, 0.1f, room.size.y);
            Gizmos.DrawCube(center, size);
            
            // show room numbs
            if (showRoomNumbers)
            {
                UnityEditor.Handles.Label(center + Vector3.up, i.ToString());
            }
        }
        
        // corridors
        if (corridors != null)
        {
            Gizmos.color = Color.yellow;
            foreach (Corridor corridor in corridors)
            {
                for (int i = 0; i < corridor.path.Count - 1; i++)
                {
                    Vector3 start = new Vector3(corridor.path[i].x, 0, corridor.path[i].y);
                    Vector3 end = new Vector3(corridor.path[i + 1].x, 0, corridor.path[i + 1].y);
                    Gizmos.DrawLine(start, end);
                }
            }
        }
    }
}

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
    
    // Internal data
    private int[,] lvlGrid;
    private List<Room> rooms;
    private List<Corridor> corridors;
    private Room startRoom;
    
    // Grid cell types
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
        
        while (!success && attempts < 10)
        {
            attempts++;
            success = TryGenerateLevel();
            
            if (!success)
            {
                // Try with a different seed
                if (useRandomSeed)
                    seed = Random.Range(0, 10000);
                else
                    seed++;
                    
                Random.InitState(seed);
                ClearGenerationData();
            }
        }
        
        if (success)
        {
            BuildDungeonMesh();
            if (showDebugInfo)
                Debug.Log($"Dungeon generated successfully with {rooms.Count} rooms. Seed: {seed}");
        }
        else
        {
            Debug.LogWarning("Failed to generate dungeon after multiple attempts!");
        }
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
    }
    
    private bool TryGenerateLevel()
    {
        // Ensure collections are initialized
        if (rooms == null) rooms = new List<Room>();
        if (corridors == null) corridors = new List<Corridor>();
        
        // Step 1: Generate rooms
        if (!GenerateRooms()) return false;
        
        // Step 2: Connect rooms with corridors
        ConnectRooms();
        
        // Step 3: Apply organic generation if enabled
        if (useOrganicGeneration)
        {
            ApplyPerlinNoise();
            RandomWalk();
            BlendOrganicWithStructured();
        }
        
        // Step 4: Fill the grid
        FillGrid();
        
        // Step 5: Generate walls
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
                // Calculate Perlin noise value
                float noiseValue = Mathf.PerlinNoise(
                    offsetX + x * noiseScale, 
                    offsetY + y * noiseScale
                );
                
                // If noise value is above threshold, mark as potential floor
                if (noiseValue > noiseThreshold)
                {
                    lvlGrid[x, y] = FLOOR;
                }
            }
        }
    }
    
    private void RandomWalk()
    {
        // Start walker in a random position
        Vector2Int walkerPos = new Vector2Int(
            Random.Range(1, lvlWidth - 1),
            Random.Range(1, lvlHeight - 1)
        );
        
        for (int i = 0; i < randomWalkSteps; i++)
        {
            // Mark current position as floor
            lvlGrid[walkerPos.x, walkerPos.y] = FLOOR;
            
            // Randomly decide to change direction
            if (Random.value < randomWalkTurnChance)
            {
                walkerPos = GetNextRandomPosition(walkerPos);
            }
            else
            {
                // Continue in same direction
                walkerPos += GetDirectionVector(walkerPos);
            }
            
            // Clamp position to stay within bounds
            walkerPos.x = Mathf.Clamp(walkerPos.x, 1, lvlWidth - 2);
            walkerPos.y = Mathf.Clamp(walkerPos.y, 1, lvlHeight - 2);
        }
    }
    
    private Vector2Int GetNextRandomPosition(Vector2Int currentPos)
    {
        // Get all valid neighboring positions
        List<Vector2Int> possibleDirections = new List<Vector2Int>();
        
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue; // Skip current position
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > 1) continue; // Skip diagonals
                
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
            
        // Return current position plus a random direction
        return currentPos + possibleDirections[Random.Range(0, possibleDirections.Count)];
    }
    
    private Vector2Int GetDirectionVector(Vector2Int pos)
    {
        // This could be enhanced to remember the last direction for smoother paths
        return GetNextRandomPosition(pos) - pos;
    }
    
    private void BlendOrganicWithStructured()
{
    for (int i = 0; i < organicBlendIterations; i++)
    {
        // Expand rooms into nearby organic areas
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
                        // Collect tiles to add to the room
                        newRoomTiles.Add(new Vector2Int(x, y));
                    }
                }
            }
            
            // Expand the room to include new tiles
            foreach (Vector2Int tile in newRoomTiles)
            {
                room.size = new Vector2Int(
                    Mathf.Max(room.size.x, tile.x - room.position.x + 1),
                    Mathf.Max(room.size.y, tile.y - room.position.y + 1)
                );
            }
        }
        
        // Connect organic areas to corridors
        foreach (Corridor corridor in corridors)
        {
            // Create a copy of the original path to iterate over
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
            
            // Add all new tiles to the corridor path
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
            
            // Generate random room size and position
            Vector2Int roomSize = new Vector2Int(
                Random.Range(minRoomSize.x, maxRoomSize.x + 1),
                Random.Range(minRoomSize.y, maxRoomSize.y + 1)
            );
            
            Vector2Int roomPosition = new Vector2Int(
                Random.Range(2, lvlWidth - roomSize.x - 2),
                Random.Range(2, lvlHeight - roomSize.y - 2)
            );
            
            Room newRoom = new Room(roomPosition, roomSize);
            
            // Check if room overlaps with existing rooms
            bool overlaps = false;
            foreach (Room existingRoom in rooms)
            {
                if (newRoom.Overlaps(existingRoom, 2))
                {
                    overlaps = true;
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
        
        // Designate the first room as start room
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
        
        // Create minimum spanning tree to connect all rooms
        List<Room> connectedRooms = new List<Room> { rooms[0] };
        List<Room> unconnectedRooms = new List<Room>(rooms.Skip(1));
        
        while (unconnectedRooms.Count > 0)
        {
            Room closestConnected = null;
            Room closestUnconnected = null;
            float closestDistance = float.MaxValue;
            
            // Find the closest pair between connected and unconnected rooms
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
            
            // Create corridor between closest rooms
            if (closestConnected != null && closestUnconnected != null)
            {
                corridors.Add(new Corridor(closestConnected, closestUnconnected));
                connectedRooms.Add(closestUnconnected);
                unconnectedRooms.Remove(closestUnconnected);
            }
        }
        
        // Add some extra connections for variety (20% chance per room pair)
        for (int i = 0; i < rooms.Count; i++)
        {
            for (int j = i + 1; j < rooms.Count; j++)
            {
                if (Random.Range(0f, 1f) < 0.2f)
                {
                    // Check if these rooms are already connected
                    bool alreadyConnected = false;
                    foreach (Corridor corridor in corridors)
                    {
                        if ((corridor.roomA == rooms[i] && corridor.roomB == rooms[j]) ||
                            (corridor.roomA == rooms[j] && corridor.roomB == rooms[i]))
                        {
                            alreadyConnected = true;
                            break;
                        }
                    }
                    
                    if (!alreadyConnected)
                    {
                        corridors.Add(new Corridor(rooms[i], rooms[j]));
                    }
                }
            }
        }
    }
    
    private void FillGrid()
    {
        // Clear grid
        if (lvlGrid == null)
        {
            lvlGrid = new int[lvlWidth, lvlHeight];
        }
        
        // Clear grid (but preserve organic generation if enabled)
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
        
        // Fill rooms
        if (rooms != null)
        {
            foreach (Room room in rooms)
            {
                if (room != null)
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
            }
        }
        
        // Fill corridors
        if (corridors != null)
        {
            foreach (Corridor corridor in corridors)
            {
                if (corridor != null && corridor.path != null)
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
                    // Check 8 neighbors (including diagonals)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;

                            if (IsValidPosition(nx, ny) && lvlGrid[nx, ny] == EMPTY)
                            {
                                lvlGrid[nx, ny] = WALL;
                            }
                        }
                    }
                }
            }
        }
    }


    // private bool IsFloorOrCorridor(int x, int y)
    // {
    //     if (!IsValidPosition(x, y)) return false;
    //     return lvlGrid[x, y] == FLOOR || lvlGrid[x, y] == CORRIDOR;
    // }

    
    private void BuildDungeonMesh()
    {
        if (dungeonParent == null)
        {
            GameObject dungeonGO = new GameObject("Generated Dungeon");
            dungeonParent = dungeonGO.transform;
        }
        
        // Clear existing dungeon
        foreach (Transform child in dungeonParent)
        {
            DestroyImmediate(child.gameObject);
        }
        
        // Build the dungeon
        for (int x = 0; x < lvlWidth; x++)
        {
            for (int y = 0; y < lvlHeight; y++)
            {
                Vector3 position = new Vector3(x, 0, y);
                
                switch (lvlGrid[x, y])
                {
                    case FLOOR:
                        if (floorPrefab != null)
                        {
                            GameObject floor = Instantiate(floorPrefab, position, Quaternion.identity, dungeonParent);
                            floor.name = $"Floor_{x}_{y}";
                        }
                        break;
                        
                    case WALL:
                        if (wallPrefab != null)
                        {
                            GameObject wall = Instantiate(wallPrefab, position, Quaternion.identity, dungeonParent);
                            wall.name = $"Wall_{x}_{y}";
                        }
                        break;
                        
                    case CORRIDOR:
                        if (corridorPrefab != null)
                        {
                            GameObject corridor = Instantiate(corridorPrefab, position, Quaternion.identity, dungeonParent);
                            corridor.name = $"Corridor_{x}_{y}";
                        }
                        else if (floorPrefab != null)
                        {
                            // Use floor prefab if no corridor prefab is specified
                            GameObject corridor = Instantiate(floorPrefab, position, Quaternion.identity, dungeonParent);
                            corridor.name = $"Corridor_{x}_{y}";
                        }
                        break;
                }
            }
        }
    }
    
    private bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < lvlWidth && y >= 0 && y < lvlHeight;
    }
    
    private void ClearDungeon()
    {
        if (dungeonParent != null)
        {
            foreach (Transform child in dungeonParent)
            {
                DestroyImmediate(child.gameObject);
            }
        }
        
        ClearGenerationData();
    }
    
    private void ClearGenerationData()
    {
        lvlGrid = null;
    
        // Initialize empty lists instead of setting to null or clearing
        if (rooms == null)
            rooms = new List<Room>();
        else
            rooms.Clear();
            
        if (corridors == null)
            corridors = new List<Corridor>();
        else
            corridors.Clear();
            
        startRoom = null;
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (!showDebugInfo || rooms == null) return;
        
        // Draw rooms
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            
            // Color based on room type
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
            
            // Draw room numbers
            if (showRoomNumbers)
            {
                UnityEditor.Handles.Label(center + Vector3.up, i.ToString());
            }
        }
        
        // Draw corridors
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

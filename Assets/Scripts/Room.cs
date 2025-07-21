using UnityEngine;

public static class RoomExtensions
{
    public static RectInt GetExpandedBounds(this Room room, int expansion)
    {
        return new RectInt(
            room.position.x - expansion,
            room.position.y - expansion,
            room.size.x + 2 * expansion,
            room.size.y + 2 * expansion
        );
    }
}

[System.Serializable]
public class Room
{
    public Vector2Int position;
    public Vector2Int size;
    public RoomType roomType;
    public bool isMainPath;
    
    public Room(Vector2Int pos, Vector2Int roomSize, RoomType type = RoomType.Normal)
    {
        position = pos;
        size = roomSize;
        roomType = type;
        isMainPath = false;
    }
    
    // Get the bounds of the room
    public RectInt GetBounds()
    {
        return new RectInt(position.x, position.y, size.x, size.y);
    }
    
    // Get the center point of the room
    public Vector2Int GetCenter()
    {
        return new Vector2Int(position.x + size.x / 2, position.y + size.y / 2);
    }
    
    // Check if this room overlaps with another room (with padding)
    public bool Overlaps(Room other, int padding = 1)
    {
        RectInt thisRect = new RectInt(position.x - padding, position.y - padding, 
                                     size.x + padding * 2, size.y + padding * 2);
        RectInt otherRect = new RectInt(other.position.x - padding, other.position.y - padding, 
                                      other.size.x + padding * 2, other.size.y + padding * 2);
        
        return thisRect.Overlaps(otherRect);
    }
}
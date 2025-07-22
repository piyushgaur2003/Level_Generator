using System.Collections.Generic;
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
    private static Stack<Room> pool = new Stack<Room>();

    public Vector2Int position;
    public Vector2Int size;
    public RoomType roomType;
    public bool isMainPath;
    
    public static int PoolCount => pool.Count;

    private Room() { }

    public static Room Get(Vector2Int pos, Vector2Int roomSize, RoomType type = RoomType.Normal)
    {
        Room room;
        if (pool.Count > 0)
        {
            room = pool.Pop();
        }
        else
        {
            room = new Room();
        }

        room.position = pos;
        room.size = roomSize;
        room.roomType = type;
        room.isMainPath = false;

        return room;
    }

    public static void Release(Room room)
    {
        if (room == null) return;
    
        room.position = Vector2Int.zero;
        room.size = Vector2Int.zero;
        room.roomType = RoomType.Normal;
        room.isMainPath = false;
        
        pool.Push(room);
    }

    public RectInt GetBounds()
    {
        return new RectInt(position.x, position.y, size.x, size.y);
    }
    
    public Vector2Int GetCenter()
    {
        return new Vector2Int(position.x + size.x / 2, position.y + size.y / 2);
    }
    
    public bool Overlaps(Room other, int padding = 1)
    {
        RectInt thisRect = new RectInt(position.x - padding, position.y - padding, 
                                     size.x + padding * 2, size.y + padding * 2);
        RectInt otherRect = new RectInt(other.position.x - padding, other.position.y - padding, 
                                      other.size.x + padding * 2, other.size.y + padding * 2);
        
        return thisRect.Overlaps(otherRect);
    }
}
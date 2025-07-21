using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Corridor
{
    public List<Vector2Int> path;
    public Room roomA;
    public Room roomB;
    
    public Corridor(Room a, Room b)
    {
        roomA = a;
        roomB = b;
        path = new List<Vector2Int>();
        GeneratePath();
    }
    
    private void GeneratePath()
    {
        Vector2Int startPoint = GetClosestPointOnRoom(roomA, roomB.GetCenter());
        Vector2Int endPoint = GetClosestPointOnRoom(roomB, roomA.GetCenter());
        
        path.Clear();
        
        // L-shaped corridor (horizontal then vertical, or vertical then horizontal)
        Vector2Int current = startPoint;
        path.Add(current);
        
        // Randomly choose whether to go horizontal first or vertical first
        bool horizontalFirst = Random.Range(0, 2) == 0;
        
        if (horizontalFirst)
        {
            // Move horizontally first
            while (current.x != endPoint.x)
            {
                current.x += current.x < endPoint.x ? 1 : -1;
                path.Add(current);
            }
            
            // Then move vertically
            while (current.y != endPoint.y)
            {
                current.y += current.y < endPoint.y ? 1 : -1;
                path.Add(current);
            }
        }
        else
        {
            // Move vertically first
            while (current.y != endPoint.y)
            {
                current.y += current.y < endPoint.y ? 1 : -1;
                path.Add(current);
            }
            
            // Then move horizontally
            while (current.x != endPoint.x)
            {
                current.x += current.x < endPoint.x ? 1 : -1;
                path.Add(current);
            }
        }
    }
    
    private Vector2Int GetClosestPointOnRoom(Room room, Vector2Int targetPoint)
    {
        RectInt bounds = room.GetBounds();
        
        // Find the closest point on the room's perimeter to the target
        int closestX = Mathf.Clamp(targetPoint.x, bounds.xMin, bounds.xMax - 1);
        int closestY = Mathf.Clamp(targetPoint.y, bounds.yMin, bounds.yMax - 1);
        
        // Ensure we're on the edge of the room
        Vector2Int roomCenter = room.GetCenter();
        
        if (Mathf.Abs(targetPoint.x - roomCenter.x) > Mathf.Abs(targetPoint.y - roomCenter.y))
        {
            // Closer to vertical edges
            closestX = targetPoint.x < roomCenter.x ? bounds.xMin : bounds.xMax - 1;
        }
        else
        {
            // Closer to horizontal edges
            closestY = targetPoint.y < roomCenter.y ? bounds.yMin : bounds.yMax - 1;
        }
        
        return new Vector2Int(closestX, closestY);
    }
}
using UnityEngine;

[CreateAssetMenu(fileName = "NewPath", menuName = "Tower Defense/Path")]
public class PathConfig : ScriptableObject
{
  public string pathName = "New Path";
  public float pathWidth = 2f;

  [System.Serializable]
  public class PathSegment
  {
    public Vector3 startPoint;
    public Vector3 endPoint;

    public float GetLength()
    {
      return Vector3.Distance(startPoint, endPoint);
    }
  }

  public PathSegment[] segments;

  [TextArea(3, 5)]
  public string description;

  public Vector3[] GetPathPoints()
  {
    if (segments == null || segments.Length == 0)
      return new Vector3[0];

    // Create array with all points (start points + final end point)
    Vector3[] points = new Vector3[segments.Length + 1];

    // Add all start points
    for (int i = 0; i < segments.Length; i++)
    {
      points[i] = segments[i].startPoint;
    }

    // Add final end point
    points[segments.Length] = segments[segments.Length - 1].endPoint;

    return points;
  }
}
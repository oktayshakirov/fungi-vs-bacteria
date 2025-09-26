using UnityEngine;

[DefaultExecutionOrder(0)] // This makes it run after PathVisualizer
public class PathManager : MonoBehaviour
{
  public static PathManager Instance { get; private set; }

  [SerializeField] private PathConfig pathConfig;
  [SerializeField] private PathVisualizer pathVisualizer;

  private Vector3[] pathPoints;

  private void Awake()
  {
    if (Instance == null)
    {
      Instance = this;
    }
    else
    {
      Destroy(gameObject);
      return;
    }

    GeneratePath();
  }

  private void GeneratePath()
  {
    if (pathConfig == null)
    {
      Debug.LogError("PathConfig is not assigned in PathManager!");
      return;
    }

    if (pathVisualizer == null)
    {
      Debug.LogError("PathVisualizer is not assigned in PathManager!");
      pathVisualizer = GetComponent<PathVisualizer>();
      if (pathVisualizer == null)
      {
        Debug.LogError("Failed to get PathVisualizer component!");
        return;
      }
    }

    pathPoints = pathConfig.GetPathPoints();

    if (pathPoints == null || pathPoints.Length == 0)
    {
      Debug.LogError("No path points generated from PathConfig!");
      return;
    }

    Debug.Log($"Generated {pathPoints.Length} path points");
    pathVisualizer.UpdatePath(pathPoints, pathConfig.pathWidth);
  }

  private void OnValidate()
  {
    if (pathVisualizer == null)
    {
      pathVisualizer = GetComponent<PathVisualizer>();
    }
  }

  public Vector3[] GetPathPoints()
  {
    return pathPoints;
  }
}
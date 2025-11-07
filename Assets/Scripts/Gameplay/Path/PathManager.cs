using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(0)]
public class PathManager : MonoBehaviour
{
  public static PathManager Instance { get; private set; }

  [Header("Dependencies")]
  [SerializeField] private PathVisualizer pathVisualizer;
  [SerializeField] private PathConfig currentPathConfig;

  private Vector3[] pathPoints;

  private void Awake()
  {
    if (Instance == null)
    {
      Instance = this;
    }
    else
    {
      Debug.LogWarning("Duplicate PathManager instance found. Destroying this one.");
      Destroy(gameObject);
      return;
    }
  }

  private void Start()
  {
    GeneratePath();
  }

  private void GeneratePath()
  {
    if (currentPathConfig == null)
    {
      Debug.LogError("PathConfig is not assigned to PathManager! Cannot generate path.", this);
      return;
    }
    if (GridManager.Instance == null)
    {
      Debug.LogError("GridManager instance not found! Cannot generate path.", this);
      return;
    }
    if (GroundManager.Instance == null)
    {
      Debug.LogWarning("GroundManager instance not found. Path height calculation might be inaccurate (using 0).", this);
    }
    if (pathVisualizer == null)
    {
      pathVisualizer = GetComponent<PathVisualizer>();
      if (pathVisualizer == null)
      {
        Debug.LogError("PathVisualizer component not found on PathManager GameObject! Cannot visualize path.", this);
      }
    }

    List<Vector2Int> gridPath = currentPathConfig.pathGridCoordinates;

    if (gridPath == null || gridPath.Count < 2)
    {
      Debug.LogError($"PathConfig '{currentPathConfig.name}' has no path coordinates defined or path is too short! Path cannot be generated.", currentPathConfig);
      pathPoints = new Vector3[0]; // Ensure pathPoints is empty
      if (pathVisualizer != null) pathVisualizer.UpdatePath(pathPoints, 0f);
      return;
    }

    foreach (Vector2Int pathCoord in gridPath)
    {
      if (pathCoord.x >= 0 && pathCoord.x < GridManager.Instance.gridSize.x &&
         pathCoord.y >= 0 && pathCoord.y < GridManager.Instance.gridSize.y)
      {
        GridManager.Instance.SetCellBuildable(pathCoord, false);
      }
      else
      {
        Debug.LogWarning($"Path coordinate {pathCoord} in PathConfig '{currentPathConfig.name}' is outside the grid bounds ({GridManager.Instance.gridSize}). Ignoring.", currentPathConfig);
      }
    }
    Debug.Log($"Path cells marked as non-buildable by PathManager using PathConfig: {currentPathConfig.name}.");

    pathPoints = new Vector3[gridPath.Count];
    for (int i = 0; i < gridPath.Count; i++)
    {
      Vector3 worldPos = GridManager.Instance.GridToWorld(gridPath[i]);
      float groundHeight = 0f;
      if (GroundManager.Instance != null)
      {
        groundHeight = GroundManager.Instance.GetGroundHeight(worldPos);
      }

      pathPoints[i] = new Vector3(worldPos.x, groundHeight, worldPos.z);
    }

    if (pathVisualizer != null)
    {
      float pathWidth = 1f;
      pathVisualizer.UpdatePath(pathPoints, pathWidth);
      Debug.Log($"Path visualized with {pathPoints.Length} points.");
    }
  }

  public Vector3[] GetPathPoints()
  {
    if (pathPoints == null)
    {
      Debug.LogWarning("Attempted to GetPathPoints before PathManager has generated them. Returning empty array.");
      return new Vector3[0];
    }
    return pathPoints;
  }
}
using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    public Vector2Int gridSize = new Vector2Int(20, 20);
    public float cellSize = 1.0f;
    public Vector3 originPosition = Vector3.zero;

    private Dictionary<Vector2Int, bool> buildableGrid;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeGrid();
        }
        else
        {
            Debug.LogWarning("Duplicate GridManager instance found. Destroying this one.");
            Destroy(gameObject);
        }
    }

    void InitializeGrid()
    {
        buildableGrid = new Dictionary<Vector2Int, bool>();
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                buildableGrid[new Vector2Int(x, y)] = true;
            }
        }
        Debug.Log($"Grid Initialized ({gridSize.x}x{gridSize.y}). All cells initially buildable.");
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return originPosition + new Vector3(
            gridPos.x * cellSize + cellSize * 0.5f,
            0,
            gridPos.y * cellSize + cellSize * 0.5f
        );
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 relativePos = worldPos - originPosition;
        int x = Mathf.FloorToInt(relativePos.x / cellSize);
        int y = Mathf.FloorToInt(relativePos.z / cellSize);
        return new Vector2Int(x, y);
    }

    public bool IsCellBuildable(Vector2Int gridPos)
    {
        if (gridPos.x < 0 || gridPos.x >= gridSize.x || gridPos.y < 0 || gridPos.y >= gridSize.y)
        {
            return false;
        }
        return buildableGrid.TryGetValue(gridPos, out bool isBuildable) && isBuildable;
    }

    public void SetCellBuildable(Vector2Int gridPos, bool isBuildable)
    {
        if (buildableGrid.ContainsKey(gridPos))
        {
            buildableGrid[gridPos] = isBuildable;
        }
        else
        {
            Debug.LogWarning($"Attempted to set buildable status for cell {gridPos} which is outside grid bounds or not initialized.");
        }
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying && buildableGrid == null) return;

        Gizmos.color = Color.grey;
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Vector2Int currentGridPos = new Vector2Int(x, y);
                Vector3 cellCenter = GridToWorld(currentGridPos);
                Vector3 size = new Vector3(cellSize, 0.01f, cellSize);

                Gizmos.DrawWireCube(cellCenter, size);

                bool isBuildable = true;
                if (Application.isPlaying && buildableGrid != null)
                {
                    buildableGrid.TryGetValue(currentGridPos, out isBuildable);
                }

                if (!isBuildable)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(cellCenter, size * 0.8f);
                    Gizmos.color = Color.grey;
                }
            }
        }

        Gizmos.color = Color.white;
        Vector3 gridCenter = originPosition + new Vector3(gridSize.x * cellSize * 0.5f, 0, gridSize.y * cellSize * 0.5f);
        Vector3 totalGridSize = new Vector3(gridSize.x * cellSize, 0.1f, gridSize.y * cellSize);
        Gizmos.DrawWireCube(gridCenter, totalGridSize);
    }
}
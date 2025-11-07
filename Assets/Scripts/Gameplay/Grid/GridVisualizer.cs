// Assets/Scripts/Gameplay/Grid/GridVisualizer.cs
using UnityEngine;
using System.Collections.Generic;

public class GridVisualizer : MonoBehaviour
{
    [SerializeField] private GameObject gridLinePrefab;
    [SerializeField] private Transform lineParent;

    [SerializeField] private int initialPoolSize = 100;

    private GridManager gridManager;
    private bool isShowing = false;
    private List<GameObject> pooledLines = new List<GameObject>();
    private Queue<GameObject> availableLines = new Queue<GameObject>();
    private List<GameObject> activeLines = new List<GameObject>();

    void Start()
    {
        gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            Debug.LogError("GridManager not found!");
            enabled = false; return;
        }
        if (gridLinePrefab == null)
        {
            Debug.LogError("Grid Line Prefab is not assigned!");
            enabled = false; return;
        }
        InitializePool();
    }

    void InitializePool()
    {
        if (lineParent == null)
        {
            lineParent = new GameObject("GridLinesPool").transform;
            lineParent.SetParent(this.transform);
            lineParent.localPosition = Vector3.zero;
            lineParent.localRotation = Quaternion.identity;
        }
        lineParent.gameObject.SetActive(true);

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject line = Instantiate(gridLinePrefab, lineParent);
            line.SetActive(false);
            pooledLines.Add(line);
            availableLines.Enqueue(line);
        }
    }

    GameObject GetPooledLine()
    {
        if (availableLines.Count > 0)
        {
            GameObject line = availableLines.Dequeue();
            line.SetActive(true);
            return line;
        }
        else
        {
            GameObject line = Instantiate(gridLinePrefab, lineParent);
            pooledLines.Add(line);
            return line;
        }
    }

    void ReturnLineToPool(GameObject line)
    {
        if (line != null)
        {
            line.SetActive(false);
            availableLines.Enqueue(line);
        }
    }

    public void ShowGrid()
    {
        if (isShowing || gridManager == null || gridLinePrefab == null) return;
        isShowing = true;

        HideGridInternal();

        float gridWorldWidth = gridManager.gridSize.x * gridManager.cellSize;
        float gridWorldHeight = gridManager.gridSize.y * gridManager.cellSize;
        Vector3 origin = gridManager.originPosition;

        float lineThickness = gridLinePrefab.transform.localScale.z;
        float lineHeight = gridLinePrefab.transform.localScale.y;

        for (int x = 0; x <= gridManager.gridSize.x; x++)
        {
            float xPos = origin.x + x * gridManager.cellSize;
            Vector3 startPoint = new Vector3(xPos, origin.y, origin.z);
            Vector3 endPoint = new Vector3(xPos, origin.y, origin.z + gridWorldHeight);
            DrawLineSegment(startPoint, endPoint, gridWorldHeight, lineThickness, lineHeight, true);
        }

        for (int y = 0; y <= gridManager.gridSize.y; y++)
        {
            float zPos = origin.z + y * gridManager.cellSize;
            Vector3 startPoint = new Vector3(origin.x, origin.y, zPos);
            Vector3 endPoint = new Vector3(origin.x + gridWorldWidth, origin.y, zPos);
            DrawLineSegment(startPoint, endPoint, gridWorldWidth, lineThickness, lineHeight, false);
        }
    }

    void DrawLineSegment(Vector3 start, Vector3 end, float length, float thickness, float height, bool isVertical)
    {
        GameObject lineGO = GetPooledLine();
        if (lineGO == null) return;
        activeLines.Add(lineGO);

        Transform lineTransform = lineGO.transform;
        lineTransform.position = (start + end) / 2f;

        if (isVertical)
        {
            lineTransform.rotation = Quaternion.Euler(0, 90, 0);
        }
        else
        {
            lineTransform.rotation = Quaternion.identity;
        }

        length = Mathf.Max(0.001f, length);
        height = Mathf.Max(0.001f, height);
        thickness = Mathf.Max(0.001f, thickness);
        lineTransform.localScale = new Vector3(length, height, thickness);
    }

    private void HideGridInternal()
    {
        foreach (GameObject line in activeLines)
        {
            ReturnLineToPool(line);
        }
        activeLines.Clear();
    }

    public void HideGrid()
    {
        if (!isShowing) return;
        HideGridInternal();
        isShowing = false;
    }

    void OnDisable()
    {
        HideGrid();
    }

    void OnDestroy()
    {
        foreach (var line in pooledLines)
        {
            if (line != null) Destroy(line);
        }
        pooledLines.Clear();
        availableLines.Clear();
        activeLines.Clear();

        if (lineParent != null && lineParent.gameObject.name == "GridLinesPool")
        {
            if (lineParent.gameObject != null)
            {
                Destroy(lineParent.gameObject);
            }
        }
    }
}

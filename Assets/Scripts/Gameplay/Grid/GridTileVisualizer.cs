using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class GridTileVisualizer : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private GameObject tileIndicatorPrefab;
    [SerializeField] private Material availableMaterial;
    [SerializeField] private Material blockedMaterial;
    [SerializeField] private Transform indicatorParent;
    [SerializeField] private int initialPoolSize = 100;

    private GridManager gridManager;
    private bool isShowing = false;

    private Vector2Int hiddenIndicatorPos = new Vector2Int(-1, -1);
    private bool isIndicatorTemporarilyHidden = false;

    // --- Object Pool ---
    private List<GameObject> pooledIndicators = new List<GameObject>();
    private Dictionary<Vector2Int, GameObject> activeIndicators = new Dictionary<Vector2Int, GameObject>();
    private Queue<GameObject> availableIndicators = new Queue<GameObject>();
    // ---

    void Awake()
    {
        gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            Debug.LogError("GridManager instance not found! Disabling GridTileVisualizer.", this);
            enabled = false;
            return;
        }
        if (tileIndicatorPrefab == null)
        {
            Debug.LogError("Tile Indicator Prefab is not assigned! Disabling GridTileVisualizer.", this);
            enabled = false;
            return;
        }
        if (availableMaterial == null || blockedMaterial == null)
        {
            Debug.LogError("Available or Blocked Material is not assigned! Disabling GridTileVisualizer.", this);
            enabled = false;
            return;
        }

        InitializePool();
    }

    void InitializePool()
    {
        if (indicatorParent == null)
        {
            indicatorParent = new GameObject("TileIndicators_Pool").transform;
            indicatorParent.SetParent(this.transform);
            indicatorParent.localPosition = Vector3.zero;
        }
        indicatorParent.gameObject.SetActive(true);

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject indicator = Instantiate(tileIndicatorPrefab, indicatorParent);
            indicator.SetActive(false);
            pooledIndicators.Add(indicator);
            availableIndicators.Enqueue(indicator);
        }
    }

    GameObject GetPooledIndicator()
    {
        if (availableIndicators.Count > 0)
        {
            GameObject indicator = availableIndicators.Dequeue();
            indicator.SetActive(true);
            return indicator;
        }
        else
        {
            Debug.LogWarning("Tile Indicator pool exhausted. Instantiating new indicator.");
            GameObject indicator = Instantiate(tileIndicatorPrefab, indicatorParent);
            pooledIndicators.Add(indicator);
            indicator.SetActive(true);
            return indicator;
        }
    }

    void ReturnIndicatorToPool(GameObject indicator)
    {
        if (indicator != null)
        {
            indicator.SetActive(false);
            availableIndicators.Enqueue(indicator);
        }
    }

    public void ShowVisualization()
    {
        if (isShowing || gridManager == null || !enabled) return;
        isShowing = true;

        ClearIndicators();

        float cellSize = gridManager.cellSize;
        float yOffset = 0.02f;

        for (int x = 0; x < gridManager.gridSize.x; x++)
        {
            for (int y = 0; y < gridManager.gridSize.y; y++)
            {
                Vector2Int gridPos = new Vector2Int(x, y);
                bool isBuildable = gridManager.IsCellBuildable(gridPos);

                GameObject indicator = GetPooledIndicator();
                Vector3 worldPos = gridManager.GridToWorld(gridPos);
                indicator.transform.position = new Vector3(worldPos.x, worldPos.y + yOffset, worldPos.z);
                indicator.transform.rotation = Quaternion.identity;
                indicator.transform.localScale = new Vector3(cellSize, 0.01f, cellSize);

                Renderer rend = indicator.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.sharedMaterial = isBuildable ? availableMaterial : blockedMaterial;
                }
                activeIndicators[gridPos] = indicator;
            }
        }
        UpdateHiddenIndicatorVisibility();
    }

    public void SetPreviewPosition(Vector2Int newPreviewPos)
    {
        if (!isShowing || !enabled) return;

        ShowTemporarilyHiddenIndicator();

        HideIndicatorAt(newPreviewPos);
        hiddenIndicatorPos = newPreviewPos;
        isIndicatorTemporarilyHidden = true;
    }

    private void HideIndicatorAt(Vector2Int gridPos)
    {
        if (activeIndicators.TryGetValue(gridPos, out GameObject indicator))
        {
            if (indicator != null) indicator.SetActive(false);
        }
    }

    public void ShowTemporarilyHiddenIndicator()
    {
        if (isIndicatorTemporarilyHidden && activeIndicators.TryGetValue(hiddenIndicatorPos, out GameObject indicator))
        {
            if (indicator != null) indicator.SetActive(true);
        }
        isIndicatorTemporarilyHidden = false;
        hiddenIndicatorPos = new Vector2Int(-1, -1);
    }

    private void UpdateHiddenIndicatorVisibility()
    {
        if (isIndicatorTemporarilyHidden)
        {
            HideIndicatorAt(hiddenIndicatorPos);
        }
    }

    public void HideVisualization()
    {
        if (!isShowing || !enabled) return;
        isShowing = false;
        ClearIndicators();
        isIndicatorTemporarilyHidden = false;
        hiddenIndicatorPos = new Vector2Int(-1, -1);
    }

    private void ClearIndicators()
    {
        foreach (var kvp in activeIndicators)
        {
            ReturnIndicatorToPool(kvp.Value);
        }
        activeIndicators.Clear();
    }

    void OnDisable()
    {
        HideVisualization();
    }

    void OnDestroy()
    {
        foreach (var indicator in pooledIndicators)
        {
            if (indicator != null)
            {
                Destroy(indicator);
            }
        }
        pooledIndicators.Clear();
        availableIndicators.Clear();
        activeIndicators.Clear();

        if (indicatorParent != null && indicatorParent.name == "TileIndicators_Pool" && indicatorParent.parent == this.transform)
        {
            Destroy(indicatorParent.gameObject);
        }
    }
}
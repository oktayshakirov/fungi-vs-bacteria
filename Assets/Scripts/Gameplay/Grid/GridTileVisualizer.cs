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

        BuildMarkerMaterials();
        InitializePool();
    }

    // --- tile markers ---
    //
    // The scene's two materials are a flat green and a flat red wash at ~29%
    // alpha across the whole cell. Edge to edge like that the board reads as
    // two coloured regions rather than a grid of tiles you can aim at, and on
    // the snow and ash grounds the wash barely registers at all.
    //
    // These are copies of those same materials - copies specifically, so the
    // transparent-shader setup already configured on the assets carries over
    // without runtime keyword juggling, and the assets themselves are never
    // touched. Only the texture and colour change.
    private Material availableInstance;
    private Material blockedInstance;

    private void BuildMarkerMaterials()
    {
        Texture2D ring = MarkerTexture();

        availableInstance = new Material(availableMaterial);
        Tint(availableInstance, ring, new Color(0.45f, 1f, 0.42f, 0.85f));

        // Deliberately far weaker than the available marker. What the player is
        // actually scanning for is where they CAN build; painting every blocked
        // tile just as loudly buries that under the path and the towers already
        // placed, which are most of the board by the mid game.
        blockedInstance = new Material(blockedMaterial);
        Tint(blockedInstance, ring, new Color(1f, 0.35f, 0.35f, 0.34f));
    }

    private static void Tint(Material material, Texture texture, Color color)
    {
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        material.mainTexture = texture;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        material.color = color;
    }

    private static Texture2D markerTexture;

    // A rounded square: a bright ring with a much fainter fill inside it, and
    // nothing at all in the gutter between cells, so adjacent tiles stay
    // visually separate instead of merging into one slab.
    private static Texture2D MarkerTexture()
    {
        if (markerTexture != null) return markerTexture;

        const int size = 64;
        const float inset = 4f;      // gutter, keeps neighbouring tiles apart
        const float radius = 12f;
        const float ringWidth = 5f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded square: negative inside, positive
                // outside, and the magnitude is how far - which is what makes a
                // ring of a given width easy to express.
                float dx = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - inset - radius);
                float dy = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - inset - radius);
                float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
                float distance = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;

                float alpha;
                if (distance > 0.5f) alpha = 0f;                       // gutter
                else if (distance > -ringWidth) alpha = 1f;            // ring
                else alpha = 0.30f;                                    // fill

                // One pixel of feathering on the outer edge, or the rounded
                // corners alias badly at this size.
                if (distance > -0.5f && distance <= 0.5f) alpha *= 1f - (distance + 0.5f);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels32(pixels);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        markerTexture = tex;
        return tex;
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
                    rend.sharedMaterial = isBuildable ? availableInstance : blockedInstance;
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
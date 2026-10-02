using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// A single on-demand render, not a second running game. Uses live LevelConfig,
// the real road interpolation, palette, meshes and biome-specific base/nest.
// No gameplay singletons, GameSession writes, saved screenshots or per-frame
// rendering. Native objects owned by this preview are released on every change.
public class SelectionIslandPreview : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    RawImage image;
    RenderTexture target;
    LevelConfig shown;
    bool baseOnly, sceneryOnly;
    readonly Dictionary<LevelConfig, RenderTexture> cache = new Dictionary<LevelConfig, RenderTexture>();
    public int RenderCount { get; private set; }
    public double LastRenderMilliseconds { get; private set; }
    public void ShowScenery(LevelConfig level) { sceneryOnly=true; baseOnly=true; Show(level); }
    public void ShowBase(LevelConfig level) { baseOnly=true; Show(level); }
    System.Action<int> browse;
    public void EnableSwipe(System.Action<int> action)
    {
        browse = action;
        image.raycastTarget = true;
    }
    public void OnBeginDrag(PointerEventData data) { }
    public void OnEndDrag(PointerEventData data)
    {
        Vector2 delta = data.position - data.pressPosition;
        if (browse == null || Mathf.Abs(delta.x) < Screen.width * .06f || Mathf.Abs(delta.x) < Mathf.Abs(delta.y)) return;
        data.eligibleForClick = false;
        browse(delta.x < 0 ? 1 : -1);
    }
    public static SelectionIslandPreview Create(RectTransform parent)
    {
        var preview = parent.gameObject.AddComponent<SelectionIslandPreview>();
        preview.image = parent.gameObject.AddComponent<RawImage>();
        preview.image.raycastTarget = false;
        preview.image.enabled = false;
        return preview;
    }

    public void Show(LevelConfig level)
    {
        if (level == shown && target != null) return;
        shown = level;
        if (level == null || level.pathConfig == null || level.pathConfig.pathGridCoordinates == null || level.pathConfig.pathGridCoordinates.Count < 2) { image.enabled = false; return; }
        var board = Resources.Load<SelectionBoardSettings>("SelectionBoardSettings");
        if (board == null)
        {
            Debug.LogError("SelectionBoardSettings missing: run Tools/Selection/Sync Board Settings.");
            image.enabled = false;
            return;
        }
        image.enabled = true;
        if(cache.TryGetValue(level,out var cached)) { target=cached; image.texture=target; return; }
        if(cache.Count>=2)
        {
            LevelConfig key=null;
            foreach(var entry in cache) { key=entry.Key; break; }
            var expired=cache[key]; cache.Remove(key); expired.Release(); Dispose(expired);
        }
        target=null;
        var watch=System.Diagnostics.Stopwatch.StartNew();
        if (target == null)
        {
            target = new RenderTexture(baseOnly ? 192 : 768, baseOnly ? 192 : 480, 16, RenderTextureFormat.ARGB32);
            target.name = "Selection Island";
            target.Create();
        }
        cache[level]=target;
        image.texture = target;
        var root = new GameObject("SelectionPreviewStage");
        var owned = new List<Material>();
        var p = EnvironmentTheme.PaletteFor(level.environmentName);
        LevelDecorator decor = null;
        Camera camera = null;
        var oldAmbient = RenderSettings.ambientMode;
        Color oldSky = RenderSettings.ambientSkyColor, oldEquator = RenderSettings.ambientEquatorColor,
          oldGround = RenderSettings.ambientGroundColor;
        bool oldFog = RenderSettings.fog;
        Light oldSun = RenderSettings.sun;
        try
        {
            var coords = level.pathConfig.pathGridCoordinates;
            var points = new Vector3[coords.Count];
            for (int i = 0; i < points.Length; i++)
                points[i] = board.origin + new Vector3((coords[i].x + .5f) * board.cellSize,
                  board.groundHeight - board.origin.y, (coords[i].y + .5f) * board.cellSize);
            decor = root.AddComponent<LevelDecorator>();
            if(sceneryOnly) decor.BuildSceneryPreview(level);
            else if(baseOnly) decor.BuildBasePreview(level);
            else
            {
            decor.BuildPreview(level, board, points);
            Material ground = Material(p.groundTint, false, owned);
            ground.SetTexture("_BaseMap", EnvironmentTheme.ResolveGround(p.ground));
            float tiling = p.groundTiling > 0 ? p.groundTiling : 3;
            ground.mainTextureScale = new Vector2(tiling, tiling);
            Surface(root.transform, "Ground", board.groundMesh, board.groundPosition, board.groundRotation, board.groundScale, ground);
            Surface(root.transform, "Soil", board.soilMesh, board.soilPosition, board.soilRotation, board.soilScale,
              Material(p.soilColor, false, owned));
            Vector3[] smooth = PathVisualizer.GenerateSmoothPath(points);
            if (smooth != null)
            {
                Road(root.transform, smooth, board.cellSize * .56f, board.groundHeight + .03f, Material(p.pathColor, true, owned));
            }
            }
            // Move BEFORE rendering; preview decorations deliberately skip static
            // batching, so relocating this hierarchy cannot leave meshes behind.
            Vector3 offset = new Vector3(10000, 10000, 10000);
            root.transform.position = offset;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            var light = new GameObject("PreviewLight").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.type = LightType.Directional;
            light.color = p.lightColor;
            light.intensity = p.lightIntensity;
            light.cullingMask = 1 << 2;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(p.lightAngles);
            RenderSettings.sun = light;
            camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 2;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 300f;
            camera.targetTexture = target;
            camera.aspect = baseOnly ? 1 : 1.6f;
            camera.transform.position = offset + new Vector3(0, 60, -85);
            camera.transform.LookAt(offset + new Vector3(0, -2, 0));
            // Fit actual renderer bounds, so replacing a base with a taller
            // model cannot crop it. No biome-specific camera guesses.
            Frame(camera, root);
            EnvironmentTheme.ApplyAmbient(p);
            RenderSettings.fog = false;
            camera.Render();
            RenderCount++;
            LastRenderMilliseconds=watch.Elapsed.TotalMilliseconds;
        }
        finally
        {
            RenderSettings.ambientMode = oldAmbient;
            RenderSettings.ambientSkyColor = oldSky;
            RenderSettings.ambientEquatorColor = oldEquator;
            RenderSettings.ambientGroundColor = oldGround;
            RenderSettings.fog = oldFog;
            RenderSettings.sun = oldSun;
            if (camera != null) camera.targetTexture = null;
            root.SetActive(false);
            if (decor != null) decor.ReleasePreviewResources();
            foreach (Material material in owned) Dispose(material);
            Dispose(root);
        }
    }
    static Material Material(Color color, bool unlit, List<Material> owned)
    {
        var m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        m.color = color;
        owned.Add(m);
        return m;
    }
    static void Frame(Camera camera, GameObject root)
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            Bounds bounds = renderer.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = camera.transform.InverseTransformPoint(corner);
                min = Vector2.Min(min, new Vector2(local.x, local.y));
                max = Vector2.Max(max, new Vector2(local.x, local.y));
            }
        }
        Vector2 center = (min + max) * .5f;
        camera.transform.position += camera.transform.right * center.x + camera.transform.up * center.y;
        camera.orthographicSize = Mathf.Max((max.x - min.x) / camera.aspect, max.y - min.y) * .54f;
    }
    static void Surface(Transform parent, string name, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }
    static void Road(Transform parent, Vector3[] points, float width, float height, Material material)
    {
        var go = new GameObject("Road");
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.Euler(90, 0, 0);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.alignment = LineAlignment.TransformZ;
        PathSurface.ApplyRoad(material);
        line.textureMode=LineTextureMode.Tile;
        line.sharedMaterial = material;
        line.sortingOrder = -10;
        line.numCornerVertices = 10;
        line.numCapVertices = 5;
        PathSurface.ApplyWidth(line,points,width);
        line.positionCount = points.Length;
        for (int i = 0; i < points.Length; i++)
            line.SetPosition(i, go.transform.InverseTransformPoint(new Vector3(points[i].x, height, points[i].z)));
        line.shadowCastingMode = ShadowCastingMode.Off;
    }
    public void Invalidate() { OnDisable(); }
    void OnDisable()
    {
        if(image!=null) image.texture=null;
        foreach(var rt in cache.Values) { rt.Release(); Dispose(rt); }
        cache.Clear(); target=null; shown=null;
    }
    static void Dispose(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}

using UnityEngine;

public class GroundManager : MonoBehaviour
{
  private static GroundManager instance;
  public static GroundManager Instance
  {
    get
    {
      if (instance == null)
      {
        instance = FindFirstObjectByType<GroundManager>();
      }
      return instance;
    }
  }

  private void Awake()
  {
    if (instance != null && instance != this)
    {
      Destroy(gameObject);
      return;
    }
    instance = this;
  }

  // Only the scenery border is reshaped; the full rectangular grid remains
  // covered by the same horizontal ground collider, including corner cells.
  public void ApplyIslandOutline()
  {
    GridManager grid = GridManager.Instance;
#if UNITY_EDITOR
    if(grid == null) grid = FindFirstObjectByType<GridManager>();
#endif
    if(grid == null) return;
    float w=grid.gridSize.x*grid.cellSize*.5f+BoardDecor.Margin;
    float d=grid.gridSize.y*grid.cellSize*.5f+BoardDecor.Margin;
    Mesh turf=MeshFactory.IslandSurface(w,d,0,0);
    var filter=GetComponent<MeshFilter>();
    if(filter != null) filter.sharedMesh=turf;
    transform.localScale=Vector3.one;
    var collider=GetComponent<MeshCollider>();
    if(collider != null) collider.sharedMesh=turf;
    GameObject soil=GameObject.Find("BoardBase");
    if(soil != null)
    {
      var soilFilter=soil.GetComponent<MeshFilter>();
      if(soilFilter != null) soilFilter.sharedMesh=MeshFactory.IslandSurface(w,d,BoardDecor.SoilTop,BoardDecor.SoilTop-BoardDecor.SoilThickness);
      soil.transform.localScale=Vector3.one;soil.transform.position=Vector3.zero;
    }
  }

  public float GetGroundHeight(Vector3 position, float raycastHeight = 10f)
  {
    Ray ray = new Ray(position + Vector3.up * raycastHeight, Vector3.down);
    if (Physics.Raycast(ray, out RaycastHit hit, raycastHeight * 2f, LayerMask.GetMask("Ground")))
    {
      return hit.point.y;
    }
    Debug.LogWarning($"No ground found below position {position}");
    return 0f;
  }

  public bool GetGroundInfo(Vector3 position, float raycastHeight, out Vector3 groundPoint, out Vector3 groundNormal)
  {
    Ray ray = new Ray(position + Vector3.up * raycastHeight, Vector3.down);
    if (Physics.Raycast(ray, out RaycastHit hit, raycastHeight * 2f, LayerMask.GetMask("Ground")))
    {
      groundPoint = hit.point;
      groundNormal = hit.normal;
      return true;
    }
    groundPoint = position;
    groundNormal = Vector3.up;
    return false;
  }
}
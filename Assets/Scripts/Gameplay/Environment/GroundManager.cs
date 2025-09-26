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
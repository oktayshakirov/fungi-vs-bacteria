// How large towers and enemies are drawn relative to their tile.
//
// The prefabs were authored when the camera framed 75 world units across; the
// board is now 10x5 framed at ~54, and the models still filled only about a
// third of a 5-unit cell. Scaling them here keeps all eight tower prefabs and
// the enemy prefabs untouched, and keeps the two figures side by side.
using UnityEngine;

public static class UnitScale
{
  public const float Tower = 1.5f;
  public const float Enemy = 1.35f;

  // Renderer.bounds on an unloaded prefab can be stale/default. Transform the
  // mesh's authored AABB explicitly; this works for both imported and baked rigs.
  public static Bounds AuthoredBodyBounds(GameObject prefab)
  {
    MeshRenderer renderer=global::Enemy.FindBodyRenderer(prefab);
    MeshFilter filter=renderer != null ? renderer.GetComponent<MeshFilter>() : null;
    if(filter == null || filter.sharedMesh == null) return new Bounds(prefab.transform.position,Vector3.one);
    Bounds local=filter.sharedMesh.bounds;Bounds world=new Bounds(filter.transform.TransformPoint(local.center),Vector3.zero);
    for(int corner=0;corner<8;corner++)
    {
      Vector3 point=local.center+new Vector3((corner&1)==0?-local.extents.x:local.extents.x,
        (corner&2)==0?-local.extents.y:local.extents.y,(corner&4)==0?-local.extents.z:local.extents.z);
      world.Encapsulate(filter.transform.TransformPoint(point));
    }
    return world;
  }

  public static float EnemyGroundOffset(GameObject prefab, float typeScale = 1f, float childScale = 1f)
  {
    Bounds body = AuthoredBodyBounds(prefab);
    float authoredLift = Mathf.Max(0f, prefab.transform.position.y - body.min.y);
    return authoredLift * Enemy * Mathf.Max(0.01f, typeScale * childScale);
  }
}

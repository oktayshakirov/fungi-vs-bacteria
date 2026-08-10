using System.Collections.Generic;
using UnityEngine;

// Simple prefab-keyed pool so enemies are reused instead of
// instantiated/destroyed every spawn (see GUIDELINES 3.2).
public static class EnemyPool
{
  private static readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();

  public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
  {
    if (!pools.TryGetValue(prefab, out Stack<GameObject> pool))
    {
      pool = new Stack<GameObject>();
      pools[prefab] = pool;
    }

    // Pooled instances may have been destroyed by a scene unload
    GameObject instance = null;
    while (pool.Count > 0 && instance == null)
    {
      instance = pool.Pop();
    }

    if (instance == null)
    {
      instance = Object.Instantiate(prefab, position, rotation);
      instance.transform.localScale *= UnitScale.Enemy;
      instance.AddComponent<PooledEnemy>().sourcePrefab = prefab;
    }
    else
    {
      instance.transform.SetPositionAndRotation(position, rotation);
      instance.SetActive(true);
    }

    return instance;
  }

  public static void Release(GameObject instance)
  {
    PooledEnemy pooled = instance.GetComponent<PooledEnemy>();
    if (pooled == null || pooled.sourcePrefab == null)
    {
      Object.Destroy(instance);
      return;
    }

    instance.SetActive(false);
    pools[pooled.sourcePrefab].Push(instance);
  }
}

public class PooledEnemy : MonoBehaviour
{
  [HideInInspector] public GameObject sourcePrefab;
}

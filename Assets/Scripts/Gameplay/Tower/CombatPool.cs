using System.Collections.Generic;
using UnityEngine;

// Scene-owned pool: unloading gameplay releases both active and idle objects.
// Retained capacity is bounded; active projectiles are never dropped.
public sealed class CombatPool : MonoBehaviour
{
    static CombatPool instance;
    readonly Dictionary<GameObject,Stack<CombatPoolItem>> idle = new Dictionary<GameObject,Stack<CombatPoolItem>>();
    int retained;
    const int PerPrefabLimit=64, TotalLimit=256;

    public static GameObject Spawn(GameObject prefab,Vector3 position,Quaternion rotation,bool impact=false)
    {
        if(instance==null) instance=new GameObject("CombatPool").AddComponent<CombatPool>();
        if(!instance.idle.TryGetValue(prefab,out var stack)) instance.idle[prefab]=stack=new Stack<CombatPoolItem>();
        CombatPoolItem item=null;
        while(stack.Count>0 && item==null) {item=stack.Pop();instance.retained--;}
        if(item==null)
        {
            var go=Instantiate(prefab,position,rotation,instance.transform);
            item=go.AddComponent<CombatPoolItem>();item.Prepare(instance,prefab,impact);
        }
        item.Restart(position,rotation);
        return item.gameObject;
    }

    public static void Release(GameObject go)
    {
        if(go.TryGetComponent<CombatPoolItem>(out var item)) item.Return();
        else Destroy(go); // Supports authored/editor projectiles outside the pool.
    }

    internal void Store(CombatPoolItem item)
    {
        if(!idle.TryGetValue(item.Source,out var stack) || stack.Count>=PerPrefabLimit || retained>=TotalLimit)
        {Destroy(item.gameObject);return;}
        stack.Push(item);retained++;
    }
}

public sealed class CombatPoolItem : MonoBehaviour
{
    public GameObject Source {get;private set;}
    CombatPool owner;
    ParticleSystem[] particles;
    TrailRenderer[] trails;
    Vector3 scale;
    bool impact, returned;
    float expires;

    public void Prepare(CombatPool pool,GameObject source,bool isImpact)
    {
        owner=pool;Source=source;impact=isImpact;scale=source.transform.localScale;
        particles=GetComponentsInChildren<ParticleSystem>(true);
        trails=GetComponentsInChildren<TrailRenderer>(true);
        foreach(var ps in particles)
        {
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;
            if(impact){main.loop=false;main.duration=Mathf.Min(main.duration,.45f);main.maxParticles=Mathf.Min(main.maxParticles,64);}
            main.stopAction=ParticleSystemStopAction.None;
        }
    }
    public void Restart(Vector3 position,Quaternion rotation)
    {
        returned=false;expires=impact?Time.time+2f:float.PositiveInfinity;
        transform.SetPositionAndRotation(position,rotation);transform.localScale=scale;
        gameObject.SetActive(true);
        foreach(var trail in trails) trail.Clear();
        foreach(var ps in particles){ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);ps.Play(false);}
        enabled=impact;
    }
    void Update(){if(Time.time>=expires)Return();}
    public void Return()
    {
        if(returned)return;returned=true;
        foreach(var ps in particles)ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach(var trail in trails)trail.Clear();
        gameObject.SetActive(false);
        if(owner!=null)owner.Store(this);else Destroy(gameObject);
    }
}

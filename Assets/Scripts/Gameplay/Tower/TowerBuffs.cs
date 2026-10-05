using System.Collections.Generic;
using UnityEngine;

// Event-driven support graph. Adjacent attackers get a small additional
// bonus; ordinary aura coverage remains useful for a spread-out defense.
public static class TowerBuffs
{
  private static readonly List<Tower> Towers = new List<Tower>();
  private static Tower selected;
  public static void Register(Tower tower)
  {
    if(tower==null || Towers.Contains(tower)) return;
    Towers.Add(tower); Recalculate();
  }
  public static void Unregister(Tower tower)
  {
    if(!Towers.Remove(tower)) return;
    if(selected==tower) selected=null;
    Recalculate();
  }
  public static void Clear()
  {
    foreach(var tower in Towers) if(tower!=null) tower.GetComponent<MyceliumRoots>()?.ShowFor(null);
    Towers.Clear(); selected=null;
  }
  public static void ShowLinksFor(Tower tower)
  {
    selected=tower;
    foreach(var source in Towers) if(source!=null) source.GetComponent<MyceliumRoots>()?.ShowFor(tower);
  }
  public static bool Adjacent(Tower a,Tower b)
  {
    float cell=GridManager.Instance!=null?GridManager.Instance.cellSize:5f;
    Vector3 delta=a.transform.position-b.transform.position;
    return (Mathf.Abs(Mathf.Abs(delta.x)-cell)<.05f && Mathf.Abs(delta.z)<.05f)
      || (Mathf.Abs(Mathf.Abs(delta.z)-cell)<.05f && Mathf.Abs(delta.x)<.05f);
  }
  public static void Recalculate()
  {
    Towers.RemoveAll(t=>t==null);
    foreach(var tower in Towers)
    {
      tower.MyceliumConnections=0;
      if(tower.IsSupport)
      {
        var roots=tower.GetComponent<MyceliumRoots>();
        if(roots==null) roots=tower.gameObject.AddComponent<MyceliumRoots>();
        roots.Begin(tower);
      }
    }
    foreach(var tower in Towers)
    {
      float damage=1, fireRate=1;
      if(!tower.IsSupport) foreach(var source in Towers)
      {
        if(source==tower || !source.IsSupport) continue;
        Vector3 delta=source.transform.position-tower.transform.position;delta.y=0;
        if(delta.sqrMagnitude>source.Range*source.Range) continue;
        damage+=source.EffectiveDamageBoost;
        fireRate+=source.EffectiveFireRateBoost;
        float link=source.GetTowerConfig().myceliumLinkBoost;
        if(link<=0 || !Adjacent(source,tower)) continue;
        if(source.EffectiveDamageBoost>0) damage+=link;
        if(source.EffectiveFireRateBoost>0) fireRate+=link;
        tower.MyceliumConnections++;
        source.MyceliumConnections++;
        source.GetComponent<MyceliumRoots>().Connect(tower);
      }
      tower.SetBuffs(damage,fireRate);
    }
    ShowLinksFor(selected);
  }
}

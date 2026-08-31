using System.Collections.Generic;
using UnityEngine;

// Support towers (Aura, Defense) do not shoot. They raise the damage and fire
// rate of every attacking tower standing inside their radius, which is what
// makes placing them centrally among your damage dealers worth 250-275 gold.
//
// Buffs are recomputed only when the set of towers CHANGES - a tower is built
// or sold - never per frame. The board tops out somewhere around thirty towers,
// so the O(n^2) sweep is trivial at that cadence, and no tower has to poll its
// neighbours in Update.
public static class TowerBuffs
{
  private static readonly List<Tower> Towers = new List<Tower>();

  public static void Register(Tower tower)
  {
    if (tower == null || Towers.Contains(tower)) return;
    Towers.Add(tower);
    Recalculate();
  }

  public static void Unregister(Tower tower)
  {
    if (!Towers.Remove(tower)) return;
    Recalculate();
  }

  // This list is static, so it survives a scene load with every entry pointing
  // at a destroyed tower. GameManager clears it when a level starts.
  public static void Clear()
  {
    Towers.Clear();
  }

  public static void Recalculate()
  {
    Towers.RemoveAll(t => t == null);

    foreach (Tower tower in Towers)
    {
      // A support tower gets no benefit from another support tower - there is
      // nothing to multiply, and letting them chain would make stacking two of
      // them strictly better than covering more of the board.
      if (tower.IsSupport)
      {
        tower.SetBuffs(1f, 1f);
        continue;
      }

      float damage = 1f;
      float fireRate = 1f;

      foreach (Tower source in Towers)
      {
        if (source == tower || !source.IsSupport) continue;

        TowerConfig cfg = source.GetTowerConfig();
        if (cfg == null) continue;
        if (Vector3.Distance(source.transform.position, tower.transform.position) > cfg.range) continue;

        damage += cfg.damageBoost;
        fireRate += cfg.fireRateBoost;
      }

      tower.SetBuffs(damage, fireRate);
    }
  }
}

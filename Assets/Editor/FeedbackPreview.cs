using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Uses the real runtime effect and boss code; the cast placement is a visual
// fixture so shield/heal/split cues can be compared in the same crowded frame.
public static class FeedbackPreview
{
  private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  private static void Invoke(object owner,string name,params object[] args) => owner.GetType().GetMethod(name,Private).Invoke(owner,args);
  public static Tower Prepare(int mode,Vector3[] path)
  {
    foreach(var tower in Object.FindObjectsByType<Tower>(FindObjectsSortMode.None)) Object.DestroyImmediate(tower.gameObject);
    TowerBuffs.Clear();
    var grid=Object.FindFirstObjectByType<GridManager>();
    Tower Place(string name,int x,int y)
    {
      var cfg=AssetDatabase.LoadAssetAtPath<TowerConfig>($"Assets/Settings/Towers/{name}.asset");
      var go=(GameObject)PrefabUtility.InstantiatePrefab(cfg.towerPrefab);
      go.transform.localScale*=UnitScale.Tower;
      go.transform.position=grid.GridToWorld(new Vector2Int(x,y));
      var unit=go.GetComponent<Tower>();Invoke(unit,"Awake");unit.Initialize(cfg);
      return unit;
    }
    var source=Place("AuraTower",4,2);
    Place("ArcherTower",5,2);Place("ArcherTower",4,3);
    Place("DefenseTower",5,3);Place("PoisonTower",5,4);Place("IceTower",6,4);
    TowerBuffs.ShowLinksFor(mode==1?source:null);
    if(mode==2)
    {
      Vector3 At(int index) { var p=path[Mathf.Min(index,path.Length-1)];p.y=.17f;return p; }
      CombatPulse.Emit(At(3),3.2f,new Color(.42f,.76f,.43f),.48f,CombatPulse.Shape.Ring)?.Pose(.35f);
      CombatPulse.Emit(At(7),1.25f,new Color(.55f,.8f,1),.45f,CombatPulse.Shape.BrokenShield,true)?.Pose(.25f);
      CombatPulse.Emit(At(10),1.1f,new Color(.91f,.63f,.25f),.4f,CombatPulse.Shape.Ring,true)?.Pose(.4f);
      foreach(var trait in Object.FindObjectsByType<EnemyTrait>(FindObjectsSortMode.None)) trait.Animate(.4f);
    }
    if(mode>=3)
    {
      var cfg=AssetDatabase.LoadAssetAtPath<EnemyConfig>(Phase4Encounter.BossPath);
      foreach(var existing in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        if(existing.gameObject.name==cfg.prefab.name) Object.DestroyImmediate(existing.gameObject);
      var go=(GameObject)PrefabUtility.InstantiatePrefab(cfg.prefab);
      var enemy=go.GetComponent<Enemy>();Invoke(enemy,"Awake");
      foreach(var trait in go.GetComponentsInChildren<EnemyTrait>()) Invoke(trait,"Awake");
      var bar=go.AddComponent<EnemyHealthBar>();Invoke(bar,"Awake");
      Vector3 at=path[path.Length-4];at.y=UnitScale.EnemyGroundOffset(cfg.prefab,cfg.scaleMultiplier);
      enemy.Initialize(new[]{at,new Vector3(path[path.Length-1].x,at.y,path[path.Length-1].z)},cfg);
      enemy.TakeDamage(204,true);
      if(mode>=4) enemy.StepBoss(1.3f);
      if(mode==5 || mode==6) { enemy.TakeDamage(245,true);if(mode==5) enemy.StepBoss(1.3f); }
      Invoke(enemy,"ApplyMotion",.2f);
      // World texts need their runtime billboard pose in an edit-mode capture.
      foreach(var text in Object.FindObjectsByType<FloatingText>(FindObjectsSortMode.None)) Invoke(text,"LateUpdate");
    }
    return mode==1?source:null;
  }
  public static void Pose(Camera cam)
  {
    foreach(var bar in Object.FindObjectsByType<EnemyHealthBar>(FindObjectsSortMode.None)) Invoke(bar,"LateUpdate");
    foreach(var text in Object.FindObjectsByType<FloatingText>(FindObjectsSortMode.None)) Invoke(text,"LateUpdate");
  }
}

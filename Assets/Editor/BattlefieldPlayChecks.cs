using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Exercises actual Instantiate/Awake ordering and pool reuse in Play Mode.
// Edit-mode previews cannot reproduce the scale bug these checks protect.
[InitializeOnLoad]
public static class BattlefieldPlayChecks
{
  private const string Pending = "BattlefieldPlayChecks.Pending";
  static BattlefieldPlayChecks() { EditorApplication.playModeStateChanged += Changed; }

  public static void RunBatch()
  {
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    SessionState.SetBool(Pending, true);
    EditorApplication.EnterPlaymode();
  }

  private static void Changed(PlayModeStateChange state)
  {
    if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false)) return;
    SessionState.SetBool(Pending,false);
    try
    {
      var camera=new GameObject("ValidationCamera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,10,-20);
      foreach (string name in new[] { "BasicEnemy", "FastEnemy", "HealerEnemy", "SplitterEnemy", "SwarmEnemy", "ArmoredEnemy", "ShieldedEnemy", "BossEnemy" })
      {
        EnemyConfig config = AssetDatabase.LoadAssetAtPath<EnemyConfig>($"Assets/Settings/Enemies/{name}.asset");
        float lift = UnitScale.EnemyGroundOffset(config.prefab, config.scaleMultiplier);
        Vector3[] path = {new Vector3(0,lift,0), new Vector3(0,lift,10)};
        GameObject instance = EnemyPool.Get(config.prefab,path[0],Quaternion.identity);
        Enemy enemy = instance.GetComponent<Enemy>();
        enemy.Initialize(path,config);
        if(name=="HealerEnemy")CheckHealerSymbols(enemy,camera);
        Vector3 expected = config.prefab.transform.localScale * UnitScale.Enemy * config.scaleMultiplier;
        CheckScale(name+" full-size", expected, instance.transform.localScale);
        CheckHealthBar(enemy);
        MeshRenderer body = Enemy.FindBodyRenderer(instance);
        // Some authored meshes (the boss) deliberately float above their pivot.
        Bounds authoredBody = UnitScale.AuthoredBodyBounds(config.prefab);
        float clearance = Mathf.Max(0, authoredBody.min.y - config.prefab.transform.position.y)
          * UnitScale.Enemy * config.scaleMultiplier;
        if (Mathf.Abs(body.bounds.min.y - clearance) > .04f)
          throw new InvalidOperationException($"{name} ground clearance: expected {clearance}, got {body.bounds.min.y}.");

        Enemy.SpawnOverride child = Enemy.SpawnOverride.Default;
        child.sizeScale = config.splitScaleShare;
        enemy.Initialize(path, config, 1, 1, child);
        CheckScale(name+" child",expected*child.sizeScale,instance.transform.localScale);
        CheckHealthBar(enemy);
        EnemyPool.Release(instance);
        GameObject reused = EnemyPool.Get(config.prefab,path[0],Quaternion.identity);
        if(reused != instance) throw new InvalidOperationException("Pool did not reuse instance.");
        reused.GetComponent<Enemy>().Initialize(path,config);
        CheckScale(name+" reused adult",expected,reused.transform.localScale);
        EnemyPool.Release(reused);
      }
      CheckGroundRange();
      CheckGroundCoverage();
      Debug.Log("BATTLEFIELD PLAY CHECKS: actual Awake, full-size spawn, clearance, child scale, pool reuse and ground range passed.");
      StartAimingChecks();
    }
    catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
  }

  private static readonly System.Collections.Generic.List<Tower> aimingTowers = new System.Collections.Generic.List<Tower>();
  private static float aimingStarted;
  private static double aimingDeadline;

  private static void StartAimingChecks()
  {
    var enemies = AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/BasicEnemy.asset");
    var stationary = Object.Instantiate(enemies);
    stationary.moveSpeed = 0f;
    stationary.maxHealth = 1000000;
    var factory = new GameObject("AimingFactory").AddComponent<TowerFactory>();
    foreach (string name in new[] {"ArcherTower","IceTower","InfernoTower","SniperTower","PoisonTower","ShockTower"})
    {
      float x = aimingTowers.Count * 20f;
      float y = UnitScale.EnemyGroundOffset(stationary.prefab, stationary.scaleMultiplier);
      GameObject unit = EnemyPool.Get(stationary.prefab, new Vector3(x+2f,y,2f), Quaternion.identity);
      unit.GetComponent<Enemy>().Initialize(new[] {new Vector3(x+2f,y,2f),new Vector3(x+2f,y,12f)}, stationary);
      TowerConfig config = AssetDatabase.LoadAssetAtPath<TowerConfig>($"Assets/Settings/Towers/{name}.asset");
      Tower tower = factory.CreateTower(config,new Vector3(x,0,0));
      typeof(Tower).GetField("fireCountdown",PrivateFields).SetValue(tower,float.MaxValue);
      aimingTowers.Add(tower);
    }
    aimingStarted = Time.time;
    aimingDeadline = EditorApplication.timeSinceStartup + 15;
    EditorApplication.update += FinishAimingChecks;
  }

  private static void FinishAimingChecks()
  {
    if(Time.time-aimingStarted < 1f && EditorApplication.timeSinceStartup < aimingDeadline) return;
    EditorApplication.update -= FinishAimingChecks;
    try
    {
      Physics.SyncTransforms();
      const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
      foreach(Tower tower in aimingTowers)
      {
        Transform target = tower.GetComponent<TowerTargeting>().CurrentTarget;
        if(target == null) throw new InvalidOperationException($"{tower.name} did not acquire its nearby enemy.");
        Transform head = (Transform)typeof(Tower).GetField("tower",fields).GetValue(tower);
        Transform muzzle = (Transform)typeof(Tower).GetField("projectileSpawnPoint",fields).GetValue(tower);
        Vector3 direction = target.position - head.position; direction.y=0;
        if(Vector3.Dot(head.rotation*Vector3.left,direction.normalized) < .98f)
          throw new InvalidOperationException($"{tower.name} mouth is not aimed at its target.");
        Vector3 outlet = muzzle.position-head.position; outlet.y=0;
        // Ice and Poison mouths are offset from their mesh origin. Require
        // the authored outlet on the target-facing side, not on its centreline.
        if(Vector3.Dot(outlet.normalized,direction.normalized) < .8f)
          throw new InvalidOperationException($"{tower.name} projectile outlet is not on the aimed side.");
        foreach(Collider collider in tower.GetComponentsInChildren<Collider>())
          if(collider.gameObject.layer != LayerMask.NameToLayer("Tower"))
            throw new InvalidOperationException($"{tower.name} has an unselectable collider.");
      }
      Debug.Log("BATTLEFIELD AIM CHECKS: all six attacking tower mouths and projectile outlets face their targets in actual Play Mode; selection layers passed.");
      StartScaleChecks();
    }
    catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
  }

  private static int scaleStage;
  private static float scaleDeadline;
  private static readonly System.Reflection.BindingFlags PrivateFields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

  private static void StartScaleChecks()
  {
    foreach(Tower tower in aimingTowers)
    {
      typeof(Tower).GetField("fireCountdown",PrivateFields).SetValue(tower,float.MaxValue);
      TowerPopIn.Play(tower.gameObject);
    }
    scaleStage=0;
    scaleDeadline=Time.time+.4f;
    EditorApplication.update+=CheckFiringScale;
  }

  private static void CheckFiringScale()
  {
    if(Time.time < scaleDeadline) return;
    try
    {
      foreach(Tower tower in aimingTowers)
      {
        Vector3 expected=tower.GetTowerConfig().towerPrefab.transform.localScale*UnitScale.Tower*(1f+.08f*(tower.Level-1));
        Vector3 actual=tower.transform.localScale;
        if(scaleStage==1)
        {
          // Recoil may compress X by 5.5%, but may never fall back to prefab size.
          for(int axis=0;axis<3;axis++)
            if(actual[axis] < expected[axis]*.94f || actual[axis] > expected[axis]*1.04f)
              throw new InvalidOperationException($"{tower.name} firing shrank its placed scale: expected near {expected}, got {actual}.");
        }
        else CheckScale(tower.name+" placed/firing tier "+tower.Level,expected,actual);

        if(scaleStage<=1)
        {
          int before=Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
          tower.Attack();
          int after=Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
          if(after!=before+1)throw new InvalidOperationException(tower.name+" scale check did not fire an actual projectile.");
        }
        else if(scaleStage==2 || scaleStage==3)
        {
          // Upgrade during recoil, then fire at the next tier. Avoid touching
          // the persisted wallet; exercise the real tier visual method.
          tower.Attack();
          typeof(Tower).GetField("<Level>k__BackingField",PrivateFields).SetValue(tower,scaleStage);
          typeof(Tower).GetMethod("ApplyTierVisuals",PrivateFields).Invoke(tower,null);
          tower.Attack();
        }
      }
      if(scaleStage==4)
      {
        EditorApplication.update-=CheckFiringScale;
        Debug.Log("TOWER SCALE CHECKS: all six attackers preserve placement scale through pop-in, first shot, overlapping recoil, and tier 2/3 firing.");
        EditorApplication.Exit(0);return;
      }
      scaleDeadline=Time.time+(scaleStage==0?.065f:.25f);
      scaleStage++;
    }
    catch(Exception e)
    {
      EditorApplication.update-=CheckFiringScale;
      Debug.LogException(e);EditorApplication.Exit(1);
    }
  }

  private static void CheckHealthBar(Enemy enemy)
  {
    const System.Reflection.BindingFlags fields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var bar=enemy.GetComponent<EnemyHealthBar>();typeof(EnemyHealthBar).GetMethod("LateUpdate",fields).Invoke(bar,null);
    Transform root=(Transform)typeof(EnemyHealthBar).GetField("barRoot",fields).GetValue(bar);
    float top=Enemy.FindBodyRenderer(enemy.gameObject).bounds.max.y;
    foreach(var trait in enemy.GetComponentsInChildren<EnemyTrait>())
      if(trait.includeInHealthBarBounds) foreach(var renderer in trait.GetComponentsInChildren<Renderer>())top=Mathf.Max(top,renderer.bounds.max.y);
    if(root.position.y<top+.45f)throw new InvalidOperationException("Health bar overlaps solid role marker: "+enemy.name);
    if((root.lossyScale-Vector3.one).sqrMagnitude>.001f)throw new InvalidOperationException("Health bar inherited unit scale: "+enemy.name);
  }

  private static void CheckHealerSymbols(Enemy enemy,Camera camera)
  {
    int signs=0;
    foreach(var trait in enemy.GetComponentsInChildren<EnemyTrait>(true))
    {
      if(!trait.name.StartsWith("HealSign"))continue;
      signs++;
      if(!trait.faceCamera)throw new InvalidOperationException("Healer symbol lacks camera facing.");
      var mesh=trait.GetComponent<MeshFilter>().sharedMesh;
      if(Mathf.Abs(mesh.bounds.size.x-mesh.bounds.size.y)>.001f)
        throw new InvalidOperationException("Healer plus has unequal arm lengths.");
      foreach(float pitch in new[]{34f,42f,50f})foreach(float yaw in new[]{0f,90f,180f,270f})
      {
        enemy.transform.rotation=Quaternion.Euler(0,yaw,0);
        camera.transform.rotation=Quaternion.Euler(pitch,0,0);
        trait.Animate(.37f,camera);
        if(Quaternion.Angle(trait.transform.rotation,camera.transform.rotation)>.1f)
          throw new InvalidOperationException("Healer symbol turns edge-on to the camera.");
      }
    }
    if(signs!=3)throw new InvalidOperationException("Expected three single-mesh healer pluses.");
    enemy.transform.rotation=Quaternion.identity;
    Debug.Log("HEALER SYMBOL CHECKS: three balanced single-mesh pluses face the camera across twelve view/heading combinations.");
  }

  private static void CheckGroundCoverage()
  {
    var gridGo=new GameObject("OutlineGrid");var grid=gridGo.AddComponent<GridManager>();
    grid.gridSize=new Vector2Int(10,5);grid.cellSize=5;grid.originPosition=new Vector3(-25,0,-12.5f);
    var groundGo=new GameObject("OutlineGround",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
    groundGo.layer=LayerMask.NameToLayer("Ground");var ground=groundGo.AddComponent<GroundManager>();ground.ApplyIslandOutline();Physics.SyncTransforms();
    for(int x=0;x<10;x++)for(int z=0;z<5;z++)
      for(int corner=0;corner<4;corner++)
      {
        Vector3 point=new Vector3(-25+x*5+((corner&1)==0?.05f:4.95f),0,-12.5f+z*5+((corner&2)==0?.05f:4.95f));
        if(!Physics.Raycast(point+Vector3.up*10,Vector3.down,out RaycastHit hit,20,1<<groundGo.layer) || Mathf.Abs(hit.point.y)>.01f)
          throw new InvalidOperationException($"Rounded ground lost grid cell {x},{z} corner {corner}.");
      }
    Object.DestroyImmediate(groundGo);Object.DestroyImmediate(gridGo);
    Debug.Log("GROUND COVERAGE: all 200 grid-cell corner raycasts passed on the rounded island.");
  }

  private static void CheckGroundRange()
  {
    EnemyConfig config = AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/BasicEnemy.asset");
    GameObject unit = EnemyPool.Get(config.prefab, new Vector3(4f,8f,0), Quaternion.identity);
    Enemy enemy = unit.GetComponent<Enemy>();
    enemy.Initialize(new[] {new Vector3(4f,8f,0),new Vector3(4f,8f,10)},config);
    var tower = new GameObject("GroundRangeCheck");
    TowerTargeting targeting = tower.AddComponent<TowerTargeting>();
    targeting.Initialize(5f);
    typeof(TowerTargeting).GetField("nextSearch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(targeting,-1f);
    targeting.UpdateTarget();
    if(targeting.CurrentTarget != unit.transform) throw new InvalidOperationException("Elevated enemy inside ground range was not targeted.");
    unit.transform.position = new Vector3(6f,0,0);
    targeting.UpdateTarget();
    if(targeting.CurrentTarget != null) throw new InvalidOperationException("Enemy beyond ground range retained as target.");
    EnemyPool.Release(unit); Object.DestroyImmediate(tower);
  }

  private static void CheckScale(string label, Vector3 expected, Vector3 actual)
  {
    if((expected-actual).sqrMagnitude > .00001f) throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
  }
}

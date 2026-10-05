using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class Phase5Checks
{
  private const string Pending="Phase5Checks.Pending";
  private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static Phase5Checks(){EditorApplication.playModeStateChanged+=Changed;}
  public static void RunBatch()
  {
    foreach(string key in new[]{"Wallet_Coins","Wallet_LevelLoan"})
    {
      SessionState.SetBool("Phase5Checks.Had."+key,PlayerPrefs.HasKey(key));SessionState.SetInt("Phase5Checks.Value."+key,PlayerPrefs.GetInt(key,0));
    }
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
  }
  private static void Changed(PlayModeStateChange state)
  {
    if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false))return;
    SessionState.SetBool(Pending,false);
    try
    {
      new GameObject("Camera").AddComponent<Camera>().tag="MainCamera";
      Branches();Assets();Lighting();
      Debug.Log("PHASE 5 CHECKS: specialization unlock/choice lock, purchase/refund, independent stats, extended targeting, projectile damage, buffs, upgrades/recoil/priority; six authored routes, hints and portraits; biome lighting passed.");EditorApplication.Exit(0);
    }
    catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    finally
    {
      foreach(string key in new[]{"Wallet_Coins","Wallet_LevelLoan"})
      {
        if(SessionState.GetBool("Phase5Checks.Had."+key,false))PlayerPrefs.SetInt(key,SessionState.GetInt("Phase5Checks.Value."+key,0));else PlayerPrefs.DeleteKey(key);
      }
      PlayerPrefs.Save();
    }
  }
  private static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
  private static void Equal(float expected,float actual,string why)=>Require(Mathf.Abs(expected-actual)<.003f,why+$" ({expected} != {actual})");
  private static TowerConfig TowerConfig(string name)=>AssetDatabase.LoadAssetAtPath<TowerConfig>($"Assets/Settings/Towers/{name}.asset");
  private static EnemyConfig EnemyConfig(string name)=>AssetDatabase.LoadAssetAtPath<EnemyConfig>($"Assets/Settings/Enemies/{name}.asset");
  private static void Branches()
  {
    var hud=new GameObject("HUD",typeof(RectTransform)).AddComponent<HUDManager>();hud.enabled=false;
    var manager=new GameObject("GameManager").AddComponent<GameManager>();manager.enabled=false;
    PlayerPrefs.SetInt("Wallet_Coins",3000);PlayerPrefs.SetInt("Wallet_LevelLoan",0);
    TowerBuffs.Clear();var factory=new GameObject("Factory").AddComponent<TowerFactory>();var cfg=TowerConfig("ArcherTower");
    var flurry=factory.CreateTower(cfg,Vector3.zero);var longshot=factory.CreateTower(cfg,new Vector3(5,0,0));
    Require(!flurry.CanSpecialize && !flurry.Specialize(ArcherSpecialization.Flurry),"Level-one Archer can specialize");
    int before=Wallet.Coins;Require(flurry.Upgrade(),"Upgrade failed");Equal(350,before-Wallet.Coins,"First upgrade price changed");
    Vector3 scale=flurry.transform.localScale;
    Require(flurry.Specialize(ArcherSpecialization.Flurry),"Flurry choice failed");
    Require(!flurry.Specialize(ArcherSpecialization.Longshot),"Choice could be changed");
    Equal(315,flurry.SellValue,"Free choice changed refund");Require(scale==flurry.transform.localScale,"Choice changed placed scale");
    Equal(19,flurry.EffectiveDamage,"Flurry direct damage");Equal(2.415f,flurry.EffectiveFireRate,"Flurry cadence");Equal(6.545f,flurry.Range,"Flurry reach");
    Equal(15,longshot.EffectiveDamage,"Choosing one Archer changed another");
    Require(longshot.Upgrade(),"Longshot upgrade failed");before=Wallet.Coins;
    Require(longshot.Specialize(ArcherSpecialization.Longshot),"Longshot choice failed");Equal(before,Wallet.Coins,"Choice charged extra coins");
    Equal(32,longshot.EffectiveDamage,"Longshot direct damage");Equal(1.29375f,longshot.EffectiveFireRate,"Longshot cadence");Equal(9.625f,longshot.Range,"Longshot reach");
    var basic=EnemyConfig("BasicEnemy");float lift=UnitScale.EnemyGroundOffset(basic.prefab,basic.scaleMultiplier);
    var enemy=EnemyPool.Get(basic.prefab,new Vector3(14.3f,lift,0),Quaternion.identity).GetComponent<Enemy>();
    enemy.Initialize(new[]{new Vector3(14.3f,lift,0),new Vector3(14.3f,lift,30)},basic);
    flurry.SetPriority(TargetPriority.First);longshot.SetPriority(TargetPriority.Strong);
    Require(flurry.GetComponent<TowerTargeting>().CurrentTarget==null,"Flurry targeted outside its reduced range");
    Require(longshot.GetComponent<TowerTargeting>().CurrentTarget==enemy.transform,"Longshot did not target its extended range");
    longshot.Attack();
    var projectile=Object.FindFirstObjectByType<Projectile>();
    var data=(ProjectileData)typeof(Projectile).GetField("data",Private).GetValue(projectile);
    Equal(32,data.Damage,"Projectile ignored branch damage");
    var aura=factory.CreateTower(TowerConfig("AuraTower"),Vector3.zero);
    Equal(44,longshot.EffectiveDamage,"Adjacent support did not stack with specialization");
    Require(!aura.Specialize(ArcherSpecialization.Flurry),"Support accepted Archer branch");
    Require(longshot.Upgrade(),"Tier-three upgrade failed");
    Require(longshot.Specialization==ArcherSpecialization.Longshot && longshot.Priority==TargetPriority.Strong,"Upgrade lost branch or priority");
    Equal(longshot.ProjectedDamageAt(3),longshot.EffectiveDamage,"Projection mismatch after upgrade");
    Require(Mathf.Abs(longshot.transform.localScale.x/(cfg.towerPrefab.transform.localScale.x*UnitScale.Tower)-1.16f)<.002f,"Upgrade during recoil lost placed scale");
    Equal(15,cfg.damage,"Branch mutated shared config damage");Equal(7,cfg.range,"Branch mutated shared config range");
    EnemyPool.Release(enemy.gameObject);
    foreach(var tower in new[]{flurry,longshot,aura})Object.DestroyImmediate(tower.gameObject);
    Object.DestroyImmediate(factory.gameObject);Object.DestroyImmediate(manager.gameObject);Object.DestroyImmediate(hud.gameObject);TowerBuffs.Clear();
  }
  private static void Assets()
  {
    var keys=new HashSet<string>();
    for(int biome=1;biome<=7;biome++)
    {
      var levels=LevelRepository.GetLevelsForEnvironment("Environment "+biome);Require(levels.Count==10,"Biome lost levels");
      foreach(var level in levels)Require(keys.Add(level.environmentName+"/"+level.levelNumber),"Duplicate progression identity");
      if(biome==1)continue;
      var first=levels[0];var cells=first.pathConfig.pathGridCoordinates;var seen=new HashSet<Vector2Int>();
      for(int i=0;i<cells.Count;i++)
      {
        Require(cells[i].x>=0 && cells[i].x<10 && cells[i].y>=0 && cells[i].y<5 && seen.Add(cells[i]),"Invalid authored path cell");
        if(i>0)Require(Mathf.Abs(cells[i].x-cells[i-1].x)+Mathf.Abs(cells[i].y-cells[i-1].y)==1,"Disconnected authored route");
        for(int j=0;j<i-1;j++)Require(Mathf.Abs(cells[i].x-cells[j].x)+Mathf.Abs(cells[i].y-cells[j].y)!=1,"Route has ambiguous adjacent branches");
      }
      Require(cells[0].x==0 && cells[cells.Count-1].x==9,"Route endpoints changed sides");
      foreach(var wave in first.waveConfig.waves)
      {
        Require(!string.IsNullOrWhiteSpace(wave.planningHint) && wave.timeToNextWave==12,"Challenge has no planning hint/preparation");
        foreach(var group in wave.enemyGroups)Require(group.enemyConfig!=null && group.count>0 && group.healthMultiplier>0 && WaveIntel.Portrait(group.enemyConfig)!=null,"Invalid challenge threat/portrait");
      }
    }
  }
  private static void Lighting()
  {
    var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;
    for(int biome=1;biome<=7;biome++)
    {
      EnvironmentTheme.Apply("Environment "+biome);var tint=EnvironmentTheme.EnemyTint;
      Require(tint.r>=.90f && tint.g>=.90f && tint.b>=.90f && tint.r<=1.1f && tint.g<=1.1f && tint.b<=1.1f,"Biome obscures enemy role colours");
      Require(light.color.r>=.7f && light.color.g>=.7f && light.color.b>=.7f,"Key light is too saturated for the cast");
    }
    Object.DestroyImmediate(light.gameObject);
  }
  public static void RunRegenerationBatch()
  {
    try
    {
      var guids=new Dictionary<string,string>();
      foreach(string guid in AssetDatabase.FindAssets("t:LevelConfig",new[]{"Assets/Resources/Levels"}))
      {
        var level=AssetDatabase.LoadAssetAtPath<LevelConfig>(AssetDatabase.GUIDToAssetPath(guid));
        foreach(var asset in new Object[]{level,level.pathConfig,level.waveConfig}){string path=AssetDatabase.GetAssetPath(asset);guids[path]=AssetDatabase.AssetPathToGUID(path);}
      }
      Require(guids.Count==210,"Campaign reference fixture incomplete");
      for(int pass=0;pass<2;pass++)
      {
        LevelGenerator.Generate();
        foreach(var entry in guids)Require(AssetDatabase.AssetPathToGUID(entry.Key)==entry.Value,"Regeneration replaced an existing GUID: "+entry.Key);
        Assets();
      }
      Debug.Log("REGENERATION CHECKS: two full 70-level generations retained 210 asset GUIDs and authored biome challenges.");EditorApplication.Exit(0);
    }
    catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
}

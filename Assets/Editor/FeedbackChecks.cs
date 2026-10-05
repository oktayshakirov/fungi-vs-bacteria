using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class FeedbackChecks
{
  private const string Pending="FeedbackChecks.Pending";
  private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static FeedbackChecks() { EditorApplication.playModeStateChanged+=Changed; }
  public static void RunBatch()
  {
    foreach(string key in new[]{"Wallet_Coins","Wallet_LevelLoan"})
    {
      SessionState.SetBool("FeedbackChecks.Had."+key,PlayerPrefs.HasKey(key));
      SessionState.SetInt("FeedbackChecks.Value."+key,PlayerPrefs.GetInt(key,0));
    }
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
  }
  private static void Changed(PlayModeStateChange state)
  {
    if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false)) return;
    SessionState.SetBool(Pending,false);
    try
    {
      new GameObject("Camera").AddComponent<Camera>().tag="MainCamera";
      Links();Boss();Healing();Budgets();
      Debug.Log("FEEDBACK CHECKS: adjacency/range/stacking, selection roots, support removal and upgrade projections; boss warnings, armor, rush, slow, poison, burst skip, pooling; actual heal aggregation and bounded/reserved effects passed.");
      EditorApplication.Exit(0);
    }
    catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
    finally
    {
      foreach(string key in new[]{"Wallet_Coins","Wallet_LevelLoan"})
      {
        if(SessionState.GetBool("FeedbackChecks.Had."+key,false)) PlayerPrefs.SetInt(key,SessionState.GetInt("FeedbackChecks.Value."+key,0));
        else PlayerPrefs.DeleteKey(key);
      }
      PlayerPrefs.Save();
    }
  }
  private static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
  private static void Equal(float expected,float actual,string message) { Require(Mathf.Abs(expected-actual)<.002f,message+$" ({expected} != {actual})"); }
  private static void Invoke(object owner,string method) { owner.GetType().GetMethod(method,Private).Invoke(owner,null); }
  private static void Set(object owner,string field,object value) { owner.GetType().GetField(field,Private).SetValue(owner,value); }
  private static void Links()
  {
    TowerBuffs.Clear();var factory=new GameObject("Factory").AddComponent<TowerFactory>();
    TowerConfig Load(string name)=>AssetDatabase.LoadAssetAtPath<TowerConfig>($"Assets/Settings/Towers/{name}.asset");
    var source=factory.CreateTower(Load("AuraTower"),Vector3.zero);
    var near=factory.CreateTower(Load("ArcherTower"),new Vector3(5,0,0));
    var diagonal=factory.CreateTower(Load("ArcherTower"),new Vector3(5,0,5));
    var far=factory.CreateTower(Load("ArcherTower"),new Vector3(15,0,0));
    Equal(20,near.EffectiveDamage,"Adjacent damage bonus missing");
    Equal(19,diagonal.EffectiveDamage,"Diagonal fungus incorrectly linked");
    Equal(15,far.EffectiveDamage,"Outside aura received bonus");
    Require(source.MyceliumConnections==1 && near.MyceliumConnections==1,"Link count incorrect");
    var second=factory.CreateTower(Load("DefenseTower"),new Vector3(5,0,-5));
    Equal(2.025f,near.EffectiveFireRate,"Adjacent fire-rate bonus missing");
    Require(source.EffectiveDamage==0 && second.EffectiveDamage==0,"Supports buff each other");
    TowerBuffs.ShowLinksFor(near);
    int visible=0;foreach(var line in source.GetComponentsInChildren<LineRenderer>()) if(line.name=="Mycelium strand" && line.enabled) visible++;
    Require(visible==3,"Selected attacker did not reveal its root connection");
    TowerBuffs.ShowLinksFor(null);
    foreach(var line in source.GetComponentsInChildren<LineRenderer>()) if(line.name=="Mycelium strand") Require(!line.enabled,"Roots remain bright when unselected");
    Set(near,"<Level>k__BackingField",2);Invoke(near,"ApplyTierVisuals");TowerBuffs.Recalculate();
    Equal(near.ProjectedDamageAt(2),near.EffectiveDamage,"Upgrade projection does not match effective damage");
    Equal(32,near.EffectiveDamage,"Upgraded damage lost link bonus");
    Set(source,"<Level>k__BackingField",2);Invoke(source,"ApplyTierVisuals");TowerBuffs.Recalculate();
    Equal(34,near.EffectiveDamage,"Support upgrade did not preserve/add link strength");
    Object.DestroyImmediate(source.gameObject);
    Equal(24,near.EffectiveDamage,"Sold support left a stale damage bonus");
    Require(near.MyceliumConnections==1,"Support removal left stale connections");
    foreach(var tower in new[]{near,diagonal,far,second}) Object.DestroyImmediate(tower.gameObject);
    Object.DestroyImmediate(factory.gameObject);TowerBuffs.Clear();
  }
  private static Enemy Spawn(EnemyConfig cfg)
  {
    float height=UnitScale.EnemyGroundOffset(cfg.prefab,cfg.scaleMultiplier);
    var enemy=EnemyPool.Get(cfg.prefab,new Vector3(0,height,0),Quaternion.identity).GetComponent<Enemy>();
    enemy.Initialize(new[]{new Vector3(0,height,0),new Vector3(0,height,30)},cfg);
    return enemy;
  }
  private static void Boss()
  {
    var cfg=AssetDatabase.LoadAssetAtPath<EnemyConfig>(Phase4Encounter.BossPath);
    var enemy=Spawn(cfg);
    enemy.ApplyFreeze(2);enemy.TakeDamage(204,true);
    foreach(var line in enemy.GetComponent<BossCue>().GetComponentsInChildren<LineRenderer>()) Require(line.enabled,"Frozen boss did not show its warning arcs");
    Require(enemy.IsBossWarning && enemy.Phase==Enemy.BossStage.Stable,"Boss fortified without warning");
    enemy.StepBoss(0);enemy.StepBoss(.5f);
    Require(enemy.Phase==Enemy.BossStage.Stable,"Boss ignored its warning duration");
    enemy.StepBoss(.76f);
    Require(enemy.Phase==Enemy.BossStage.Fortified && !enemy.IsBossWarning,"Fortify transition failed");
    Require(FloatingText.ActiveCount==0,"Completed boss warning text remains active");
    var arcBlock=new MaterialPropertyBlock();
    enemy.GetComponent<BossCue>().GetComponentInChildren<LineRenderer>().GetPropertyBlock(arcBlock);
    Equal(.76f,arcBlock.GetColor("_BaseColor").r,"Frozen boss retained the warning phase cue");
    int before=enemy.health;enemy.TakeDamage(100);
    Equal(55,before-enemy.health,"Fortified armor incorrect");
    enemy.ApplySlow(.5f);enemy.TakeDamage(245,true);
    Require(enemy.IsBossWarning,"Rush did not warn");
    enemy.StepBoss(1.26f);
    Require(enemy.Phase==Enemy.BossStage.Rushing,"Rush transition failed");
    Equal(.7f,enemy.speed,"Rush lost slow or speed multiplier");
    before=enemy.health;enemy.TakeDamage(100);Equal(85,before-enemy.health,"Rush armor incorrect");
    enemy.ApplyPoison(10,1);enemy.StepPoison(1);Equal(1,enemy.health,"Poison did not bypass phase armor");
    uint old=enemy.SpawnVersion;
    EnemyPool.Release(enemy.gameObject);
    var original=AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/BossEnemy.asset");
    var reused=Spawn(original);
    Require(reused==enemy && reused.SpawnVersion!=old,"Boss pooling fixture did not reuse");
    Require(reused.Phase==Enemy.BossStage.Stable && !reused.IsBossWarning,"Pooled boss retained a phase");
    Equal(1,reused.speed,"Pooled boss retained rush speed");
    reused.TakeDamage(420,true);reused.StepBoss(2);
    Require(reused.Phase==Enemy.BossStage.Stable,"Normal campaign boss acquired prototype phases");
    EnemyPool.Release(reused.gameObject);
    var burst=Spawn(cfg);burst.TakeDamage(204,true);
    int warnings=FloatingText.ActiveCount;burst.TakeDamage(216,true);
    Require(FloatingText.ActiveCount==warnings,"Superseded boss warning remains active");
    burst.StepBoss(1.26f);
    Require(burst.Phase==Enemy.BossStage.Rushing,"Burst incorrectly applied obsolete fortified phase");
    burst.TakeDamage(1000,true);burst.StepBoss(2);
    Require(!burst.IsTargetable,"Lethal damage left a boss active");
    Require(WaveIntel.Portrait(cfg)!=null,"Boss portrait alias missing");
  }
  private static void Healing()
  {
    var basic=AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/BasicEnemy.asset");
    var target=Spawn(basic);target.TakeDamage(23,true);
    target.ReceiveHeal(-10);Equal(77,target.health,"Negative healing damaged a unit");
    target.ReceiveHeal(1000);Equal(100,target.health,"Healing overflowed max health");
    Equal(23,(int)typeof(Enemy).GetField("pendingHeal",Private).GetValue(target),"Heal text reports requested rather than actual amount");
    Set(target,"pendingHeal",0);target.TakeDamage(30,true);
    var healer=Spawn(AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/HealerEnemy.asset"));
    Set(healer,"nextHealAt",-1f);Invoke(healer,"TickHealer");
    Equal(78,target.health,"Healer tick did not heal its neighbor");
    EnemyPool.Release(target.gameObject);EnemyPool.Release(healer.gameObject);
  }
  private static void ClearEffects()
  {
    foreach(var e in Object.FindObjectsByType<CombatPulse>(FindObjectsInactive.Include,FindObjectsSortMode.None)) Object.DestroyImmediate(e.gameObject);
    foreach(var e in Object.FindObjectsByType<FloatingText>(FindObjectsInactive.Include,FindObjectsSortMode.None)) Object.DestroyImmediate(e.gameObject);
    foreach(var e in Object.FindObjectsByType<DeathEffect>(FindObjectsInactive.Include,FindObjectsSortMode.None)) Object.DestroyImmediate(e.gameObject);
  }
  private static void Budgets()
  {
    ClearEffects();
    var pulse=CombatPulse.Emit(Vector3.zero,1,Color.green,1);uint pulseRevision=pulse.Revision;
    pulse.Cancel(pulseRevision);
    var reusedPulse=CombatPulse.Emit(Vector3.zero,1,Color.green,1);pulse.Cancel(pulseRevision);
    Require(pulse==reusedPulse && CombatPulse.ActiveCount==1,"Stale handle cancelled a reused pulse");
    reusedPulse.Cancel(reusedPulse.Revision);
    var text=FloatingText.Spawn(Vector3.zero,"OLD",Color.white);uint textRevision=text.Revision;
    text.Cancel(textRevision);
    var reusedText=FloatingText.Spawn(Vector3.zero,"NEW",Color.white);text.Cancel(textRevision);
    Require(text==reusedText && FloatingText.ActiveCount==1,"Stale handle cancelled a reused text");
    reusedText.Cancel(reusedText.Revision);
    for(int i=0;i<100;i++) CombatPulse.Emit(Vector3.zero,1,Color.green,1);
    Require(CombatPulse.ActiveCount==CombatPulse.MinorLimit,"Minor pulse budget not enforced");
    for(int i=0;i<30;i++) CombatPulse.Emit(Vector3.zero,1,Color.blue,1,CombatPulse.Shape.BrokenShield,true);
    Require(CombatPulse.ActiveCount==CombatPulse.TotalLimit,"Important pulse budget not bounded/reserved");
    for(int i=0;i<100;i++) FloatingText.Spawn(Vector3.zero,"1",Color.white);
    Require(FloatingText.ActiveCount==FloatingText.MinorLimit,"Floating text minor budget not enforced");
    for(int i=0;i<30;i++) FloatingText.Spawn(Vector3.zero,"WARN",Color.white,3,true);
    Require(FloatingText.ActiveCount==FloatingText.TotalLimit,"Floating text reserved budget not bounded");
    for(int i=0;i<100;i++) DeathEffect.Spawn(Vector3.zero,Color.red,2);
    Require(DeathEffect.ActiveCount==DeathEffect.LiveLimit,"Death fragment burst budget not enforced");
    ClearEffects();
    Require(CombatPulse.ActiveCount==0 && FloatingText.ActiveCount==0 && DeathEffect.ActiveCount==0,"Destroyed effects left stale live counters");
  }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Assertions for the four combat-logic fixes, run headless.
//
// Everything here is logic the game can get wrong SILENTLY: a projectile that
// quietly hits the wrong enemy, a splash that quietly damages one enemy twice,
// a chain that quietly stops after one jump, a poison that quietly respects
// armour it is supposed to ignore. None of that shows up in a render and none
// of it fails a compile, which is why it is checked here rather than left for
// the device playtest to maybe notice.
//
// Drives the REAL Projectile and Enemy, through Projectile.ResolveImpact and
// Enemy.StepPoison - not a reimplementation of either. A check that recomputes
// what it is checking proves only that it can do arithmetic.
//
//   Unity -batchmode -quit -nographics -executeMethod CombatCheck.RunBatch
public static class CombatCheck
{
  private static int failures;
  private static readonly List<GameObject> spawned = new List<GameObject>();

  [MenuItem("Tools/Balance/Check Combat Logic")]
  public static void Run()
  {
    failures = 0;

    ChainWalksDownTheLine();
    ChainNeverHitsTheSameEnemyTwice();
    ChainStopsWhenTheNextEnemyIsOutOfReach();
    SplashDamagesEachEnemyExactlyOnce();
    SplashCountsAnEnemyWithChildCollidersOnce();
    PoisonIgnoresArmour();
    PoisonExpires();
    PoisonDoesNotSurviveReuseOfAPooledEnemy();
    RespawnedEnemyIsNotHitByAnOldProjectile();

    Cleanup();
    Debug.Log(failures == 0
      ? "COMBAT CHECK: all checks passed."
      : $"COMBAT CHECK: {failures} FAILED.");
  }

  public static void RunBatch()
  {
    Run();
    EditorApplication.Exit(failures == 0 ? 0 : 1);
  }

  // ------------------------------------------------------------------ chain

  private static void ChainWalksDownTheLine()
  {
    // Four in a row, two units apart, with a three-unit reach: each enemy can
    // only see its neighbours, so a chain has to WALK rather than splash.
    Enemy[] line = Line(4, 2f);
    Fire(Chain(100, targets: 3, radius: 3f), line[0]);

    // 100, then 25% off each jump.
    Expect("chain hits the whole line", 100, Dealt(line[0]));
    Expect("chain hop 1", 75, Dealt(line[1]));
    Expect("chain hop 2", 56, Dealt(line[2]));
    Expect("chain hop 3", 42, Dealt(line[3]));
    Cleanup();
  }

  private static void ChainNeverHitsTheSameEnemyTwice()
  {
    // Three enemies all within reach of each other. A chain of four jumps has
    // nowhere new to go after the third, and must not come back around.
    Enemy[] cluster = Line(3, 0.5f);
    Fire(Chain(100, targets: 4, radius: 3f), cluster[0]);

    Expect("cluster head hit once", 100, Dealt(cluster[0]));
    Expect("cluster second hit once", 75, Dealt(cluster[1]));
    Expect("cluster third hit once", 56, Dealt(cluster[2]));
    Cleanup();
  }

  private static void ChainStopsWhenTheNextEnemyIsOutOfReach()
  {
    // Two close together, the third beyond the reach of the second.
    Enemy a = At(new Vector3(0f, 0f, 0f));
    Enemy b = At(new Vector3(1f, 0f, 0f));
    Enemy far = At(new Vector3(20f, 0f, 0f));
    Fire(Chain(100, targets: 3, radius: 3f), a);

    Expect("chain reached the near pair", 100, Dealt(a));
    Expect("chain reached the near pair", 75, Dealt(b));
    Expect("chain did not reach across the board", 0, Dealt(far));
    Cleanup();
  }

  // ----------------------------------------------------------------- splash

  private static void SplashDamagesEachEnemyExactlyOnce()
  {
    Enemy[] pack = Line(3, 0.5f);
    Fire(Splash(40, radius: 3f), pack[0]);

    foreach (Enemy e in pack) Expect("splash hits each enemy once", 40, Dealt(e));
    Cleanup();
  }

  // The one the old code got wrong: OverlapSphere returns every COLLIDER, so an
  // enemy carrying a body collider plus a trait's collider was damaged once per
  // collider. Shielded and Splitter both carry extra geometry.
  private static void SplashCountsAnEnemyWithChildCollidersOnce()
  {
    Enemy e = At(Vector3.zero);
    for (int i = 0; i < 3; i++)
    {
      var child = new GameObject("TraitCollider");
      child.transform.SetParent(e.transform, false);
      child.AddComponent<SphereCollider>().radius = 0.4f;
    }
    Physics.SyncTransforms();

    Fire(Splash(40, radius: 3f), e);
    Expect("four colliders, one enemy, one hit", 40, Dealt(e));
    Cleanup();
  }

  // ----------------------------------------------------------------- poison

  private static void PoisonIgnoresArmour()
  {
    Enemy soft = At(Vector3.zero);
    Enemy armoured = At(new Vector3(30f, 0f, 0f), armor: 0.5f);

    foreach (Enemy e in new[] { soft, armoured })
    {
      e.ApplyPoison(20f, 4f, Color.green);
      // Ten steps of a tenth of a second: one full second of a 20/s dose.
      for (int i = 0; i < 10; i++) e.StepPoison(0.1f);
    }

    Expect("poison dose lands", 20, Dealt(soft));
    Expect("poison ignores armour", 20, Dealt(armoured));

    // The control: ordinary damage on the same enemy IS halved, so the check
    // above is about poison and not about the armour field being ignored.
    armoured.TakeDamage(20);
    Expect("armour still applies to ordinary damage", 30, Dealt(armoured));
    Cleanup();
  }

  private static void PoisonExpires()
  {
    Enemy e = At(Vector3.zero);
    e.ApplyPoison(10f, 1f, Color.green);
    for (int i = 0; i < 40; i++) e.StepPoison(0.1f);   // four seconds of ticks

    Expect("poison stops at its duration", 10, Dealt(e));
    Expect("poison flag clears", false, e.IsPoisoned);
    Cleanup();
  }

  // Enemies are pooled, so state that outlives Initialize is handed to whoever
  // comes out of the pool next - the trap frozenUntil was already fixed for.
  private static void PoisonDoesNotSurviveReuseOfAPooledEnemy()
  {
    Enemy e = At(Vector3.zero);
    e.ApplyPoison(50f, 10f, Color.green);
    e.StepPoison(0.1f);

    Reinitialize(e);
    for (int i = 0; i < 10; i++) e.StepPoison(0.1f);

    Expect("a reused enemy is not still poisoned", 0, Dealt(e));
    Cleanup();
  }

  // ----------------------------------------------------------- spawn version

  private static void RespawnedEnemyIsNotHitByAnOldProjectile()
  {
    Enemy e = At(Vector3.zero);
    Projectile shot = Shot(Single(50), e);

    // The enemy dies and the pool hands the same instance back out as a new
    // one. The shot is still in the air and must drop its target.
    uint before = e.SpawnVersion;
    Reinitialize(e);
    Expect("reuse bumps the spawn version", true, e.SpawnVersion != before);

    shot.ResolveImpact();
    Expect("an old shot does not hit the new enemy", 0, Dealt(e));
    Cleanup();
  }

  // ------------------------------------------------------------------ setup

  private static ProjectileData Single(int damage) =>
    new ProjectileData(damage, false, 0f, false, 0f, 20f);

  private static ProjectileData Splash(int damage, float radius) =>
    new ProjectileData(damage, true, radius, false, 0f, 20f);

  private static ProjectileData Chain(int damage, int targets, float radius) =>
    new ProjectileData(damage, false, 0f, false, 0f, 20f, null,
      targets, radius, 0.25f);

  private static void Fire(ProjectileData data, Enemy at) => Shot(data, at).ResolveImpact();

  private static Projectile Shot(ProjectileData data, Enemy at)
  {
    var go = new GameObject("CheckProjectile");
    spawned.Add(go);
    Projectile p = go.AddComponent<Projectile>();
    p.Aim(data, at.transform, at.transform.position);
    return p;
  }

  private static Enemy[] Line(int count, float spacing)
  {
    var line = new Enemy[count];
    for (int i = 0; i < count; i++) line[i] = At(new Vector3(i * spacing, 0f, 0f));
    return line;
  }

  private static Enemy At(Vector3 position, float armor = 0f)
  {
    var go = new GameObject("CheckEnemy");
    spawned.Add(go);
    go.transform.position = position;
    go.AddComponent<SphereCollider>().radius = 0.4f;

    Enemy e = go.AddComponent<Enemy>();
    Initialize(e, armor);
    // Enemy.Initialize places the enemy on its path; put it back where the
    // check wants it, and tell physics, or every OverlapSphere here is stale.
    go.transform.position = position;
    Physics.SyncTransforms();
    return e;
  }

  private static readonly Dictionary<Enemy, int> startHealth = new Dictionary<Enemy, int>();

  private static void Initialize(Enemy e, float armor)
  {
    var cfg = ScriptableObject.CreateInstance<EnemyConfig>();
    cfg.maxHealth = 100000;   // high enough that nothing here dies mid-check
    cfg.moveSpeed = 0f;
    cfg.goldReward = 0;
    cfg.baseDamage = 0;
    cfg.isArmored = armor > 0f;
    cfg.armorDamageReduction = armor;
    cfg.scaleMultiplier = 1f;

    // A path of one point, so Update has somewhere to stand and the enemy never
    // walks out from under the check.
    e.Initialize(new[] { e.transform.position }, cfg);
    startHealth[e] = e.health;
  }

  private static void Reinitialize(Enemy e)
  {
    var cfg = ScriptableObject.CreateInstance<EnemyConfig>();
    cfg.maxHealth = 100000;
    cfg.moveSpeed = 0f;
    cfg.scaleMultiplier = 1f;
    e.Initialize(new[] { e.transform.position }, cfg);
    startHealth[e] = e.health;
  }

  private static int Dealt(Enemy e) => startHealth[e] - e.health;

  private static void Cleanup()
  {
    foreach (GameObject go in spawned)
    {
      if (go != null) Object.DestroyImmediate(go);
    }
    spawned.Clear();
    startHealth.Clear();
  }

  private static void Expect(string what, object expected, object actual)
  {
    if (Equals(expected, actual)) return;
    failures++;
    Debug.LogError($"COMBAT CHECK FAILED - {what}: expected {expected}, got {actual}");
  }
}

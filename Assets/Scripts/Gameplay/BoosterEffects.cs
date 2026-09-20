using System;
using System.Collections;
using UnityEngine;

// What pressing a booster actually does, and the rules about when it may be
// pressed. The HUD bar asks CanUse before enabling a button and calls Use when
// it is tapped; nothing else activates a booster.
//
// A MonoBehaviour because two of the four are timed, and it creates itself on
// first use so nothing has to be wired into a scene.
public class BoosterEffects : MonoBehaviour
{
  private static BoosterEffects instance;

  // Raised whenever a booster is used or a limit changes, so the bar can
  // redraw. The inventory raises its own event for the counts.
  public static event Action OnUsageChanged;

  // Multiplies every tower's fire rate while Overclock is running. Read by
  // Tower.EffectiveFireRate; 1 when nothing is active.
  public static float FireRateMultiplier { get; private set; } = 1f;

  // When the current Overclock ends, for the HUD countdown. Realtime-independent:
  // it uses scaled time, so the 2x/3x speed controls shorten it in game-seconds
  // exactly as they shorten everything else.
  private static float overclockEndsAt;
  public static bool OverclockActive => FireRateMultiplier > 1f;
  public static float OverclockRemaining => Mathf.Max(0f, overclockEndsAt - Time.time);

  // The wave number each booster was last used on, or -1. A level has at most a
  // handful of waves, so "once per wave" is just "not the wave I used it on".
  private static readonly int[] usedOnWave = new int[4];

  // Cleared by GameManager when a level starts - these are static and would
  // otherwise carry a previous level's usage into the next one, which is the
  // same trap TowerBuffs.Clear exists for.
  public static void ResetForLevel()
  {
    for (int i = 0; i < usedOnWave.Length; i++) usedOnWave[i] = -1;
    FireRateMultiplier = 1f;
    overclockEndsAt = 0f;
    OnUsageChanged?.Invoke();
  }

  private static int CurrentWave =>
    EnemySpawner.Instance != null ? EnemySpawner.Instance.WavesStarted : 0;

  // Why a booster cannot be used right now, or null when it can be. A string
  // rather than a bool because the HUD says it out loud - a disabled button
  // with no reason reads as broken.
  public static string BlockedReason(BoosterKind kind)
  {
    if (!BoosterInventory.Has(kind)) return "None left";

    int last = usedOnWave[(int)kind];
    if (last < 0) return null;

    return BoosterCatalog.Limit(kind) switch
    {
      BoosterLimit.OncePerLevel => "Used this level",
      BoosterLimit.OncePerWave => last == CurrentWave ? "Used this wave" : null,
      _ => null,
    };
  }

  public static bool CanUse(BoosterKind kind) => BlockedReason(kind) == null;

  public static bool Use(BoosterKind kind)
  {
    if (!CanUse(kind)) return false;
    if (!BoosterInventory.Consume(kind)) return false;

    usedOnWave[(int)kind] = CurrentWave;

    switch (kind)
    {
      case BoosterKind.SporeBomb: Detonate(); break;
      case BoosterKind.FrostWave: Freeze(); break;
      case BoosterKind.Overclock: Runtime().BeginOverclock(); break;
      case BoosterKind.Mend: Mend(); break;
    }

    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    OnUsageChanged?.Invoke();
    return true;
  }

  // --------------------------------------------------------------- effects

  // Kills everything on the board WITHOUT paying gold for it (Enemy.Vaporize).
  //
  // That is the balance rule the whole booster hangs on: a bomb that paid full
  // kill rewards would earn back more than its 600 coins on any dense late
  // wave, and buying bombs would become the cheapest way to farm coins rather
  // than a way out of trouble.
  private static void Detonate()
  {
    // Over a copy: Vaporize removes the enemy from the very list being walked.
    Enemy[] doomed = new Enemy[Enemy.Active.Count];
    for (int i = 0; i < doomed.Length; i++) doomed[i] = Enemy.Active[i];

    foreach (Enemy enemy in doomed)
    {
      if (enemy != null) enemy.Vaporize();
    }

    CameraRig.Instance?.Shake(0.6f);
    AudioManager.Instance?.Vibrate();
  }

  private static void Freeze()
  {
    foreach (Enemy enemy in Enemy.Active)
    {
      if (enemy != null) enemy.ApplyFreeze(BoosterCatalog.FreezeSeconds);
    }
  }

  private static void Mend()
  {
    GameManager.Instance?.Repair(BoosterCatalog.MendHealth);
  }

  private void BeginOverclock()
  {
    overclockEndsAt = Time.time + BoosterCatalog.OverclockSeconds;
    FireRateMultiplier = BoosterCatalog.OverclockMultiplier;

    StopAllCoroutines();
    StartCoroutine(EndOverclockWhenDue());
  }

  private IEnumerator EndOverclockWhenDue()
  {
    // Polled rather than a single WaitForSeconds so a second Overclock can
    // extend the first, and so the end time is the single source of truth.
    while (Time.time < overclockEndsAt) yield return null;

    FireRateMultiplier = 1f;
    OnUsageChanged?.Invoke();
  }

  // The host object for the timed effects. Created on demand and destroyed with
  // the scene; ResetForLevel puts the static state back either way.
  private static BoosterEffects Runtime()
  {
    if (instance != null) return instance;

    var go = new GameObject("BoosterEffects");
    instance = go.AddComponent<BoosterEffects>();
    return instance;
  }

  private void OnDestroy()
  {
    if (instance == this) instance = null;
  }
}

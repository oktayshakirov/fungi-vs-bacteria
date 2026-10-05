using System.Collections.Generic;
using UnityEngine;

// One scene's run, including every continue. No persistent state or rewards.
public sealed class BattleReport
{
  public sealed class Escape
  {
    public EnemyConfig Config { get; internal set; }
    public bool IsChild { get; internal set; }
    public int Count { get; internal set; }
    public int HealthLost { get; internal set; }
    public string Name => Config == null ? "Unknown threat" : WaveIntel.Name(Config) + (IsChild ? " child" : "");
  }
  private readonly List<Escape> escapes = new List<Escape>();
  public int StartingHealth { get; }
  public int PreviousBest { get; }
  public int ReachedColony { get; private set; }
  public int HealthLost { get; private set; }
  public int ProtectedEscapes { get; private set; }
  public BattleReport(int startingHealth, int previousBest)
  {
    StartingHealth = Mathf.Max(1, startingHealth);
    PreviousBest = Mathf.Clamp(previousBest, 0, 3);
  }
  public void RecordEscape(EnemyConfig config, bool child, int healthLost)
  {
    healthLost = Mathf.Max(0, healthLost);
    ReachedColony++;
    HealthLost += healthLost;
    if (healthLost == 0) ProtectedEscapes++;
    var entry = escapes.Find(e => e.Config == config && e.IsChild == child);
    if (entry == null) { entry = new Escape { Config = config, IsChild = child }; escapes.Add(entry); }
    entry.Count++;
    entry.HealthLost += healthLost;
  }
  public List<Escape> RankedEscapes()
  {
    var ranked = new List<Escape>(escapes);
    ranked.Sort((a,b) => {
      int order = b.HealthLost.CompareTo(a.HealthLost);
      if (order == 0) order = b.Count.CompareTo(a.Count);
      if (order == 0) order = string.CompareOrdinal(a.Name,b.Name);
      return order;
    });
    return ranked;
  }
  public string Lesson()
  {
    var ranked = RankedEscapes();
    if (ranked.Count == 0) return "Keep overlapping tower ranges along a long stretch of path.";
    if (HealthLost == 0) return "The Colony Shield blocked these escapes. Add exit coverage for when it ends.";
    var threat = ranked[0];
    var c = threat.Config;
    if (c == null) return "Strengthen exit coverage and use FIRST to target enemies near the colony.";
    if (threat.IsChild || c.isSplitter) return "Leave Inferno or Shock coverage near the exit to catch Splitter children.";
    if (c.hasBossPhases) return "Pair Poison with Ice before the boss rush. Use STRONG on your Poison tower.";
    if (c.hasShield) return "Group rapid-fire towers so shields cannot recharge between hits.";
    if (c.isHealer) return "Concentrate tower damage in one area to overwhelm the healing pack.";
    if (c.isArmored) return "Poison ignores armor. Build it before costly upgrades; add Ice for more time.";
    if (c.isFast) return "Add Ice on a covered bend and use FIRST to catch runners near the exit.";
    if (c.maxHealth < 60) return "Use Inferno splash or Shock chains where groups stay in range.";
    return "Overlap tower ranges along the path and use FIRST near the exit.";
  }
  public static string Goal(int stars, int previousBest, int startingHealth, bool victory, int totalWaves)
  {
    if (!victory && previousBest == 0) return $"FIRST VICTORY: Clear all {Mathf.Max(1,totalWaves)} waves with colony health remaining.";
    int target = stars >= 3 ? 3 : previousBest > stars ? previousBest : Mathf.Min(3,stars + 1);
    if (!victory) target = Mathf.Min(3,previousBest + 1);
    string title = stars >= 3 ? "3-STAR DEFENSE" : previousBest > stars && victory ? $"MATCH YOUR BEST: {target} STARS" : $"NEXT GOAL: {target} STARS";
    return $"{title}: Finish with at least {LevelProgress.HealthForStars(target,startingHealth)} / {Mathf.Max(1,startingHealth)} health.";
  }
}

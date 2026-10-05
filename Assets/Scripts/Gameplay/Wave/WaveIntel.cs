using System.Collections.Generic;

// Shared by the persistent forecast and the short wave announcement.
public static class WaveIntel
{
  public struct Entry { public EnemyConfig config; public int count; }
  public static List<Entry> Entries(WaveConfig.Wave wave)
  {
    var result = new List<Entry>();
    if (wave?.enemyGroups == null) return result;
    foreach (var group in wave.enemyGroups)
    {
      if (group?.enemyConfig == null || group.count <= 0) continue;
      int index = result.FindIndex(e => e.config == group.enemyConfig);
      if (index < 0) result.Add(new Entry { config = group.enemyConfig, count = group.count });
      else { var entry = result[index]; entry.count += group.count; result[index] = entry; }
    }
    return result;
  }
  public static int Count(WaveConfig.Wave wave)
  {
    int count = 0;
    foreach (var entry in Entries(wave)) count += entry.count;
    return count;
  }
  public static string Name(EnemyConfig config) => config.enemyName.Replace("Enemy", "").Replace("enemy", "").Trim();
  public static UnityEngine.Sprite Portrait(EnemyConfig config) => UnityEngine.Resources.Load<UnityEngine.Sprite>("EnemyPortraits/" +
    (string.IsNullOrWhiteSpace(config.portraitResourceName)?config.name:config.portraitResourceName));
  public static string Hint(WaveConfig.Wave wave)
  {
    if (!string.IsNullOrWhiteSpace(wave?.planningHint)) return wave.planningHint;
    var entries = Entries(wave);
    if (entries.Exists(e => e.config.hasBossPhases)) return "Boss warns before fortifying, then rushes. Poison and Ice counter its phases.";
    if (entries.Exists(e => e.config.hasShield)) return "Sustained fire breaks shields before they recharge.";
    if (entries.Exists(e => e.config.isHealer)) return "Concentrate damage in one area to beat healing.";
    if (entries.Exists(e => e.config.isSplitter)) return "Keep splash damage near the exit for split children.";
    if (entries.Exists(e => e.config.isArmored)) return "Poison damage ignores armor; Ice gives it more time.";
    if (entries.Exists(e => e.config.isFast)) return "Ice slows runners. FIRST targets the closest to the exit.";
    if (entries.Exists(e => e.config.maxHealth < 60)) return "Shock chains and Inferno splash punish crowded groups.";
    return "Cover a long stretch of path. Tap a fungus to compare upgrades.";
  }
}

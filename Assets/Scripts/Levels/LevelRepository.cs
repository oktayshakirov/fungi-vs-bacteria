using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class LevelRepository
{
  private const string LevelsResourcePath = "Levels";

  public static List<LevelConfig> GetLevelsForEnvironment(string environmentName)
  {
    IEnumerable<LevelConfig> levels = Resources.LoadAll<LevelConfig>(LevelsResourcePath);
    if (!string.IsNullOrEmpty(environmentName))
    {
      levels = levels.Where(level => level.environmentName == environmentName);
    }
    return levels.OrderBy(level => level.levelNumber).ToList();
  }

  public static LevelConfig GetNextLevel(LevelConfig current)
  {
    if (current == null) return null;
    return GetLevelsForEnvironment(current.environmentName)
      .FirstOrDefault(level => level.levelNumber > current.levelNumber);
  }
}

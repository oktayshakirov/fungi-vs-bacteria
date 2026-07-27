using UnityEngine;

public static class LevelProgress
{
  // TESTING: unlocks every environment and level. Set to false before release.
  public const bool UnlockAll = true;

  private static string Key(string environmentName) => $"HighestCompletedLevel_{environmentName}";

  public static int GetHighestCompletedLevel(string environmentName)
  {
    return PlayerPrefs.GetInt(Key(environmentName), 0);
  }

  public static void MarkLevelCompleted(string environmentName, int levelNumber)
  {
    if (levelNumber > GetHighestCompletedLevel(environmentName))
    {
      PlayerPrefs.SetInt(Key(environmentName), levelNumber);
      PlayerPrefs.Save();
    }
  }

  public static bool IsLevelUnlocked(string environmentName, int levelNumber)
  {
    if (UnlockAll) return true;
    return levelNumber <= GetHighestCompletedLevel(environmentName) + 1;
  }

  private static string StarsKey(string environmentName, int levelNumber)
    => $"Stars_{environmentName}_{levelNumber}";

  public static int GetStars(string environmentName, int levelNumber)
  {
    return PlayerPrefs.GetInt(StarsKey(environmentName, levelNumber), 0);
  }

  // Keeps the player's best result for a level
  public static void SetStars(string environmentName, int levelNumber, int stars)
  {
    if (stars > GetStars(environmentName, levelNumber))
    {
      PlayerPrefs.SetInt(StarsKey(environmentName, levelNumber), stars);
      PlayerPrefs.Save();
    }
  }

  // 3 stars for finishing near-untouched, 2 for over half health, else 1
  public static int StarsForHealth(int healthRemaining, int startingHealth)
  {
    if (startingHealth <= 0) return 1;
    float ratio = (float)healthRemaining / startingHealth;
    if (ratio >= 0.9f) return 3;
    if (ratio >= 0.5f) return 2;
    return 1;
  }
}

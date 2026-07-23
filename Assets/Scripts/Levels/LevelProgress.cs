using UnityEngine;

public static class LevelProgress
{
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
    return levelNumber <= GetHighestCompletedLevel(environmentName) + 1;
  }
}

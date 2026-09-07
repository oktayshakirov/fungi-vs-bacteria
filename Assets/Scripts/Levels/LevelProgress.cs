using UnityEngine;

public static class LevelProgress
{
  // TESTING: unlocks every environment and level. MUST be false in a store
  // build. Flip it back to true to jump straight to a late level while
  // debugging - with it false you have to play there.
  // `static readonly`, not `const`: a compile-time constant makes every
  // `if (UnlockAll) return true;` below fold away and the compiler then reports
  // the returns as unreachable code, burying real warnings under noise.
  public static readonly bool UnlockAll = false;

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

  // An environment opens when the one before it is FINISHED, not part-done.
  // Difficulty is strictly sequential across the seven biomes (env 1 is
  // difficulty 1-10, env 7 is 61-70), so letting a player into env 7 with env 1
  // half-played drops them onto difficulty 61 with a level-1 wallet.
  //
  // This replaces a hand-authored `isLocked` flag on each entry of
  // EnvironmentsScreen's inspector list, which was set to false on all seven -
  // so with UnlockAll off, every biome was open from a fresh install while only
  // level 1 of each was playable. That combination was never rendered or played,
  // which is exactly why UnlockAll had to be turned off to find it.
  public static bool IsEnvironmentUnlocked(string previousEnvironmentName,
    int previousLevelCount)
  {
    if (UnlockAll) return true;

    // The first environment has nothing before it.
    if (string.IsNullOrEmpty(previousEnvironmentName)) return true;

    // An environment with no levels generated yet cannot gate anything, or a
    // gap in the level assets would lock the player out of the rest of the game.
    if (previousLevelCount <= 0) return true;

    return GetHighestCompletedLevel(previousEnvironmentName) >= previousLevelCount;
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

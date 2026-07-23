using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class Phase1Validator
{
  public static void Validate()
  {
    bool ok = true;

    const int gridWidth = DisplaySetup.BoardWidth;
    const int gridHeight = DisplaySetup.BoardHeight;

    var levels = Resources.LoadAll<LevelConfig>("Levels");
    if (levels.Length == 0)
    {
      Debug.LogError("VALIDATE FAIL: no LevelConfig assets found in Resources/Levels");
      ok = false;
    }

    var perEnvironment = new System.Collections.Generic.Dictionary<string, int>();
    foreach (var level in levels)
    {
      perEnvironment.TryGetValue(level.environmentName, out int count);
      perEnvironment[level.environmentName] = count + 1;

      if (level.pathConfig == null) { Debug.LogError($"VALIDATE FAIL: {level.name} pathConfig is null"); ok = false; }
      if (level.waveConfig == null) { Debug.LogError($"VALIDATE FAIL: {level.name} waveConfig is null"); ok = false; }
      else if (level.waveConfig.waves == null || level.waveConfig.waves.Length == 0)
      {
        Debug.LogError($"VALIDATE FAIL: {level.name} waveConfig has no waves"); ok = false;
      }
      else
      {
        foreach (var wave in level.waveConfig.waves)
        {
          foreach (var group in wave.enemyGroups)
          {
            if (group.enemyConfig == null || group.count <= 0)
            {
              Debug.LogError($"VALIDATE FAIL: {level.name} has an empty enemy group"); ok = false;
            }
          }
        }
      }

      if (level.pathConfig != null && !ValidatePath(level.name, level.pathConfig, gridWidth, gridHeight))
      {
        ok = false;
      }
    }

    foreach (var pair in perEnvironment)
    {
      Debug.Log($"VALIDATE INFO: '{pair.Key}' has {pair.Value} levels");
    }

    var victory = Resources.Load<VictoryScreen>("Screens/VictoryScreen");
    if (victory == null)
    {
      Debug.LogError("VALIDATE FAIL: VictoryScreen prefab/component not found in Resources/Screens");
      ok = false;
    }
    else
    {
      var so = new SerializedObject(victory);
      var next = so.FindProperty("nextLevelButton").objectReferenceValue as Button;
      var menu = so.FindProperty("mainMenuButton").objectReferenceValue as Button;
      if (next == null) { Debug.LogError("VALIDATE FAIL: nextLevelButton not wired"); ok = false; }
      if (menu == null) { Debug.LogError("VALIDATE FAIL: mainMenuButton not wired"); ok = false; }
    }

    var audioManager = AssetDatabase.LoadAssetAtPath<AudioManager>("Assets/Prefabs/Managers/AudioManager.prefab");
    if (audioManager == null)
    {
      Debug.LogError("VALIDATE FAIL: AudioManager prefab not found");
      ok = false;
    }
    else
    {
      foreach (AudioManager.SoundType type in System.Enum.GetValues(typeof(AudioManager.SoundType)))
      {
        bool found = false;
        foreach (var sound in audioManager.sounds)
        {
          if (sound.type == type && sound.clip != null) { found = true; break; }
        }
        if (!found)
        {
          Debug.LogError($"VALIDATE FAIL: AudioManager has no clip wired for SoundType.{type}");
          ok = false;
        }
      }
    }

    Debug.Log(ok ? "VALIDATE PASS: level assets are wired correctly" : "VALIDATE FAILED");
    EditorApplication.Exit(ok ? 0 : 1);
  }

  private static bool ValidatePath(string levelName, PathConfig path, int gridWidth, int gridHeight)
  {
    var cells = path.pathGridCoordinates;
    if (cells.Count < 2)
    {
      Debug.LogError($"VALIDATE FAIL: {levelName} path too short ({cells.Count} cells)");
      return false;
    }

    if (cells[0].x != 0 || cells[cells.Count - 1].x != gridWidth - 1)
    {
      Debug.LogError($"VALIDATE FAIL: {levelName} path must run from the left edge to the right edge");
      return false;
    }

    var seen = new System.Collections.Generic.HashSet<Vector2Int>();
    for (int i = 0; i < cells.Count; i++)
    {
      Vector2Int cell = cells[i];
      if (cell.x < 0 || cell.x >= gridWidth || cell.y < 0 || cell.y >= gridHeight)
      {
        Debug.LogError($"VALIDATE FAIL: {levelName} path cell {cell} is out of the {gridWidth}x{gridHeight} grid");
        return false;
      }
      if (!seen.Add(cell))
      {
        Debug.LogError($"VALIDATE FAIL: {levelName} path visits {cell} twice");
        return false;
      }
      if (i > 0 && Mathf.Abs(cell.x - cells[i - 1].x) + Mathf.Abs(cell.y - cells[i - 1].y) != 1)
      {
        Debug.LogError($"VALIDATE FAIL: {levelName} path jumps from {cells[i - 1]} to {cell}");
        return false;
      }
    }
    return true;
  }
}

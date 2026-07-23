using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LevelSelectionScreen : MonoBehaviour
{
  [Header("Level Card Setup")]
  [SerializeField] private GameObject levelCardPrefab;
  [SerializeField] private Transform cardsContainer;

  [Header("Back Button")]
  [SerializeField] private Button backButton;

  private void Start()
  {
    PopulateLevelCards();

    if (backButton != null)
    {
      backButton.onClick.AddListener(OnBack);
    }
  }

  private void PopulateLevelCards()
  {
    string environmentName = GameSession.SelectedEnvironment;
    List<LevelConfig> levels = LevelRepository.GetLevelsForEnvironment(environmentName);

    if (levels.Count == 0)
    {
      Debug.LogWarning($"No LevelConfig assets found in Resources/Levels for environment '{environmentName}'.");
      return;
    }

    foreach (LevelConfig level in levels)
    {
      GameObject cardGO = Instantiate(levelCardPrefab, cardsContainer);
      LevelCard card = cardGO.GetComponent<LevelCard>();
      if (card != null)
      {
        bool isLocked = !LevelProgress.IsLevelUnlocked(level.environmentName, level.levelNumber);
        LevelConfig selected = level;
        card.Setup(level.levelNumber, isLocked, _ => OnLevelSelected(selected));
      }
      else
      {
        Debug.LogWarning("LevelCard component not found on the levelCardPrefab.");
      }
    }
  }

  private void OnLevelSelected(LevelConfig level)
  {
    Debug.Log($"Selected Level: {level.levelNumber} ({level.environmentName})");
    GameSession.SelectedLevel = level;
    AudioManager.Instance.PlaySound(AudioManager.SoundType.LevelPicked);
    SceneController.Instance.LoadScene(SceneController.GameScene.MainGame);
    gameObject.SetActive(false);
  }

  private void OnBack()
  {
    Destroy(gameObject);
    if (EnvironmentsScreen.Instance != null)
    {
      AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
      EnvironmentsScreen.Instance.gameObject.SetActive(true);
    }
  }
}

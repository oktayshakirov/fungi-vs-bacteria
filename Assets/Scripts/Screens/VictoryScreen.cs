using UnityEngine;
using UnityEngine.UI;

public class VictoryScreen : MonoBehaviour
{
  [SerializeField] private Button nextLevelButton;
  [SerializeField] private Button mainMenuButton;

  public void Initialize()
  {
    if (nextLevelButton == null || mainMenuButton == null)
    {
      Debug.LogError("Buttons are not assigned in the inspector!");
      return;
    }

    nextLevelButton.onClick.RemoveAllListeners();
    mainMenuButton.onClick.RemoveAllListeners();

    nextLevelButton.onClick.AddListener(OnNextLevelClicked);
    mainMenuButton.onClick.AddListener(ReturnToMainMenu);

    bool hasNextLevel = LevelRepository.GetNextLevel(GameSession.SelectedLevel) != null;
    nextLevelButton.gameObject.SetActive(hasNextLevel);
  }

  private void OnNextLevelClicked()
  {
    LevelConfig nextLevel = LevelRepository.GetNextLevel(GameSession.SelectedLevel);
    if (nextLevel == null) return;

    GameSession.SelectedLevel = nextLevel;
    gameObject.SetActive(false);
    AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
    GameManager.Instance.RestartGame();
  }

  private void ReturnToMainMenu()
  {
    gameObject.SetActive(false);
    AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
    GameManager.Instance.ReturnToMainMenu();
  }
}

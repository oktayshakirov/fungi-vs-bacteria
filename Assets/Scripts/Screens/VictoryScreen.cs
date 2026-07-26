using UnityEngine;
using UnityEngine.UI;

public class VictoryScreen : MonoBehaviour
{
  [SerializeField] private Button nextLevelButton;
  [SerializeField] private Button mainMenuButton;

  private RectTransform starsRow;

  public void Initialize(int stars = 3)
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

    ShowStars(stars);
  }

  // Builds a row of star sprites at runtime, so no prefab wiring is required.
  private void ShowStars(int stars)
  {
    if (starsRow != null) Destroy(starsRow.gameObject);

    var go = new GameObject("StarsRow", typeof(RectTransform));
    go.transform.SetParent(transform, false);
    starsRow = (RectTransform)go.transform;
    starsRow.anchorMin = new Vector2(0.5f, 0.63f);
    starsRow.anchorMax = new Vector2(0.5f, 0.63f);
    starsRow.pivot = new Vector2(0.5f, 0.5f);
    starsRow.anchoredPosition = Vector2.zero;
    starsRow.sizeDelta = new Vector2(500f, 160f);
    starsRow.SetAsLastSibling();

    StarSprite.BuildRow(starsRow, Mathf.Clamp(stars, 0, 3), 130f);
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

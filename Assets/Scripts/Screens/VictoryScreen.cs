using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VictoryScreen : MonoBehaviour
{
  [SerializeField] private Button nextLevelButton;
  [SerializeField] private Button mainMenuButton;

  private RectTransform starsRow;

  public void Initialize(int stars = 3, int coinsEarned = 0)
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

    ScreenTheme.Apply(transform, nextLevelButton);
    ShowStars(stars);
    ShowCoinPayout(coinsEarned);

    // Asked once per level end; Ads decides whether an ad is actually due.
    // Deliberately here rather than on the button presses: the screen appearing
    // is the moment the level is over, and the player has not yet chosen what
    // to do next.
    Ads.OnLevelEnded();
  }

  // Silent when nothing was earned — a "+0 coins" line on a replay reads as a
  // punishment for playing again.
  private void ShowCoinPayout(int coins)
  {
    if (coins <= 0) return;

    var go = new GameObject("CoinPayout", typeof(RectTransform));
    go.transform.SetParent(transform, false);

    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(0.5f, 0.52f);
    rect.anchorMax = new Vector2(0.5f, 0.52f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = Vector2.zero;
    rect.sizeDelta = new Vector2(560f, 60f);
    rect.SetAsLastSibling();

    // A LayoutGroup on an ancestor would otherwise reposition this.
    go.AddComponent<LayoutElement>().ignoreLayout = true;

    var label = go.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.Heading, UiSkin.Gold);
    label.text = $"+{coins} COINS";
    label.alignment = TextAlignmentOptions.Center;
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
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    GameManager.Instance.RestartGame();
  }

  private void ReturnToMainMenu()
  {
    gameObject.SetActive(false);
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    GameManager.Instance.ReturnToMainMenu();
  }
}

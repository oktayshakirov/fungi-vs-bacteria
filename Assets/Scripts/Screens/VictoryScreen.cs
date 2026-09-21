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

    // A gold chip between the stars and the button card. It used to sit at
    // 52% of the height, which is inside the card on every device - the
    // payout was drawn straight over the NEXT LEVEL label.
    var go = new GameObject("CoinPayout", typeof(RectTransform));
    go.transform.SetParent(transform, false);

    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(0.5f, 0.605f);
    rect.anchorMax = new Vector2(0.5f, 0.605f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = Vector2.zero;
    rect.sizeDelta = new Vector2(300f, 56f);
    rect.SetAsLastSibling();

    // A LayoutGroup on an ancestor would otherwise reposition this.
    go.AddComponent<LayoutElement>().ignoreLayout = true;
    UiSkin.Panel(go.AddComponent<Image>(), new Color(0.05f, 0.06f, 0.10f, 0.85f), UiSkin.RadiusChip);

    Image coin = UiSkin.Icon(go.transform, UiSprites.Coin(), UiSkin.Gold, 36f);
    var coinRect = (RectTransform)coin.transform;
    coinRect.anchorMin = coinRect.anchorMax = new Vector2(0f, 0.5f);
    coinRect.pivot = new Vector2(0f, 0.5f);
    coinRect.anchoredPosition = new Vector2(18f, 0f);

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    var labelRect = UiSkin.Stretch((RectTransform)labelGo.transform);
    labelRect.offsetMin = new Vector2(60f, 0f);
    labelRect.offsetMax = new Vector2(-16f, 0f);
    var label = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.Value, UiSkin.Gold);
    label.fontSizeMax = 34f;
    label.text = $"+{coins}";
    label.alignment = TextAlignmentOptions.Midline;
    label.raycastTarget = false;
  }

  // Builds a row of star sprites at runtime, so no prefab wiring is required.
  private void ShowStars(int stars)
  {
    if (starsRow != null) Destroy(starsRow.gameObject);

    var go = new GameObject("StarsRow", typeof(RectTransform));
    go.transform.SetParent(transform, false);
    starsRow = (RectTransform)go.transform;
    // Between the title and the payout chip. At 63% with 130-unit stars the
    // row hung down over the top of the button card.
    starsRow.anchorMin = new Vector2(0.5f, 0.715f);
    starsRow.anchorMax = new Vector2(0.5f, 0.715f);
    starsRow.pivot = new Vector2(0.5f, 0.5f);
    starsRow.anchoredPosition = Vector2.zero;
    starsRow.sizeDelta = new Vector2(420f, 120f);
    starsRow.SetAsLastSibling();
    go.AddComponent<LayoutElement>().ignoreLayout = true;

    StarSprite.BuildRow(starsRow, Mathf.Clamp(stars, 0, 3), 104f);
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

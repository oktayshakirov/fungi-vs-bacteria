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

    // Short of three stars, offer the level again: stars are where the coin
    // payout comes from, and the only other route back was menu -> biome ->
    // level. Not offered at three stars, where there is nothing left to earn.
    Button replay = EnsureReplayButton();
    replay.gameObject.SetActive(stars < 3);

    ScreenTheme.Apply(transform, nextLevelButton, null, UiSkin.Gold);
    LowerCard();
    ShowStars(stars);
    ShowCoinPayout(coinsEarned);

    // Asked once per level end; Ads decides whether an ad is actually due.
    // Deliberately here rather than on the button presses: the screen appearing
    // is the moment the level is over, and the player has not yet chosen what
    // to do next.
    Ads.OnLevelEnded();
  }

  private Button EnsureReplayButton()
  {
    Transform parent = mainMenuButton.transform.parent;
    Transform existing = parent.Find("ReplayButton");
    Button replay;
    if (existing != null)
    {
      replay = existing.GetComponent<Button>();
    }
    else
    {
      replay = Instantiate(mainMenuButton, parent);
      replay.name = "ReplayButton";
      replay.transform.SetSiblingIndex(mainMenuButton.transform.GetSiblingIndex());
      TMP_Text label = replay.GetComponentInChildren<TMP_Text>(true);
      if (label != null) label.text = "REPLAY";
    }
    replay.onClick.RemoveAllListeners();
    replay.onClick.AddListener(OnReplayClicked);
    return replay;
  }

  // The stars and the payout chip sit between the title plate and the card, so
  // the card is centred lower here than on the other modals - with the third
  // (Replay) button it grew up into the payout chip at the shared height.
  private void LowerCard()
  {
    Transform panel = nextLevelButton.transform.parent;
    var rect = panel as RectTransform;
    if (rect == null) return;
    rect.anchorMin = new Vector2(0.5f, 0.33f);
    rect.anchorMax = new Vector2(0.5f, 0.33f);
  }

  private void OnReplayClicked()
  {
    gameObject.SetActive(false);
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    GameManager.Instance.RestartGame();
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
    rect.anchorMin = new Vector2(0.5f, 0.615f);
    rect.anchorMax = new Vector2(0.5f, 0.615f);
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
    starsRow.anchorMin = new Vector2(0.5f, 0.735f);
    starsRow.anchorMax = new Vector2(0.5f, 0.735f);
    starsRow.pivot = new Vector2(0.5f, 0.5f);
    starsRow.anchoredPosition = Vector2.zero;
    starsRow.sizeDelta = new Vector2(420f, 120f);
    starsRow.SetAsLastSibling();
    go.AddComponent<LayoutElement>().ignoreLayout = true;

    StarSprite.BuildRow(starsRow, Mathf.Clamp(stars, 0, 3), 104f);

    // Earned stars pop in one after another; empty slots are simply there.
    int earned = Mathf.Clamp(stars, 0, 3);
    for (int i = 0; i < starsRow.childCount; i++)
    {
      if (i >= earned) continue;
      starsRow.GetChild(i).gameObject.AddComponent<PopIn>().delay = 0.25f + i * 0.28f;
    }
  }

  // Scale 0 -> overshoot -> 1 after a delay, on unscaled time (the game may be
  // paused under this screen). In batch previews, where nothing updates, the
  // star is left at full size so renders still show it.
  private class PopIn : MonoBehaviour
  {
    public float delay;
    private float start = -1f;

    private void OnEnable()
    {
      if (!Application.isPlaying) return;
      start = Time.unscaledTime;
      transform.localScale = Vector3.zero;
    }

    private void Update()
    {
      if (start < 0f) return;
      float t = (Time.unscaledTime - start - delay) / 0.35f;
      if (t < 0f) return;
      if (t >= 1f)
      {
        transform.localScale = Vector3.one;
        if (Application.isPlaying) Haptics.Play(Haptics.Style.Light);
        Destroy(this);
        return;
      }
      // Back-out easing: overshoots to ~1.2 before settling.
      float c = 2.2f;
      float u = t - 1f;
      float s = 1f + (c + 1f) * u * u * u + c * u * u;
      transform.localScale = Vector3.one * s;
    }
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

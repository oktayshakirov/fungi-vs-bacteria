using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VictoryScreen : MonoBehaviour
{
  [SerializeField] private Button nextLevelButton;
  [SerializeField] private Button mainMenuButton;

  private RectTransform starsRow;
  private TMP_Text payoutLabel;
  private Button skipReveal;
  private int earnedStars, payout, shownCoins = -1, lastStar = -1;
  private float revealStarted;
  private bool revealing;
  private const float RevealDuration = 1.45f;

  public void Initialize(int stars = 3, int coinsEarned = 0)
  {
    if (nextLevelButton == null || mainMenuButton == null)
    {
      Debug.LogError("Buttons are not assigned in the inspector!");
      return;
    }

    revealing = false;
    earnedStars = Mathf.Clamp(stars,0,3);
    payout = Mathf.Max(0,coinsEarned);
    shownCoins = lastStar = -1;
    payoutLabel = null;
    foreach(string name in new[]{"CoinPayout","ProgressSummary","SkipReveal"})
    {
      var old = transform.Find(name);
      if(old != null) { old.name += "Retired"; old.gameObject.SetActive(false); if(Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
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
    ShowCoinPayout(payout);
    ShowProgress();
    BuildSkip();
    revealStarted = Time.unscaledTime;
    revealing = Application.isPlaying;
    if(revealing) AnimateReveal(0); else CompleteReveal();

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
    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.29f);
    var layout = panel.GetComponent<VerticalLayoutGroup>();
    if(layout != null) layout.spacing=12f;
    foreach(var button in panel.GetComponentsInChildren<Button>(true))
    {
      var element=button.GetComponent<LayoutElement>();
      if(element!=null) { element.minHeight=68f; element.preferredHeight=68f; }
    }
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

    // The chip keeps its 300 width to match the buttons below it, but the coin
    // and the number are laid out as one CENTRED group rather than the coin
    // being pinned to the left edge and the number centred in what is left of
    // the pill. Done the other way they end up a hundred units apart with the
    // coin floating off on its own, which reads as a broken layout rather than
    // as a reward. Same HorizontalLayoutGroup shape as HudTheme's stat chips.
    var layout = go.AddComponent<HorizontalLayoutGroup>();
    layout.padding = new RectOffset(16, 16, 8, 8);
    layout.spacing = 10;
    layout.childAlignment = TextAnchor.MiddleCenter;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = false;
    layout.childForceExpandHeight = false;

    Image coin = UiSkin.Icon(go.transform, UiSprites.Coin(), UiSkin.Gold, 36f);
    var coinElement = coin.gameObject.AddComponent<LayoutElement>();
    coinElement.preferredWidth = 36f;
    coinElement.preferredHeight = 36f;
    coinElement.flexibleWidth = 0f;

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    var label = labelGo.AddComponent<TextMeshProUGUI>();
    payoutLabel = label;
    UiSkin.Label(label, UiSkin.Role.Value, UiSkin.Gold);
    label.fontSizeMax = 34f;
    label.text = $"+{coins}";
    label.alignment = TextAlignmentOptions.MidlineLeft;
    label.raycastTarget = false;

    // Sized for the FINAL payout, not the current one: the number counts up
    // from +0, and a width that followed the text would shuffle the coin
    // leftward on every digit the count-up adds.
    var labelElement = labelGo.AddComponent<LayoutElement>();
    labelElement.preferredWidth = label.GetPreferredValues($"+{coins}").x;
    labelElement.preferredHeight = 36f;
    labelElement.flexibleWidth = 0f;
  }

  // Builds a row of star sprites at runtime, so no prefab wiring is required.
  private void ShowStars(int stars)
  {
    if(starsRow != null) { starsRow.name = "StarsRowRetired"; starsRow.gameObject.SetActive(false); if(Application.isPlaying) Destroy(starsRow.gameObject); else DestroyImmediate(starsRow.gameObject); }

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

  }

  private void ShowProgress()
  {
    var current=GameSession.SelectedLevel;
    var next=LevelRepository.GetNextLevel(current);
    string text="WELL DEFENDED";
    if(next!=null) text=$"UP NEXT: LEVEL {next.levelNumber}  •  {EnvironmentInfo.DisplayName(next.environmentName)}";
    else if(current!=null)
    {
      string[] parts=current.environmentName.Split(' ');
      if(int.TryParse(parts[parts.Length-1],out int number))
      {
        var levels=LevelRepository.GetLevelsForEnvironment("Environment "+(number+1));
        text=levels.Count>0 ? $"ENVIRONMENT COMPLETE\nNEXT: {EnvironmentInfo.DisplayName(levels[0].environmentName)}" : "ALL ENVIRONMENTS COMPLETE!";
      }
    }
    var go=new GameObject("ProgressSummary",typeof(RectTransform));go.transform.SetParent(transform,false);
    var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.525f);
    rect.sizeDelta=new Vector2(900,48);go.AddComponent<LayoutElement>().ignoreLayout=true;
    var label=go.AddComponent<TextMeshProUGUI>();UiSkin.Label(label,UiSkin.Role.Value,UiSkin.TextPrimary);
    label.fontSizeMax=22;label.fontSizeMin=16;label.alignment=TextAlignmentOptions.Center;label.text=text;label.raycastTarget=false;
  }

  private void BuildSkip()
  {
    var go=new GameObject("SkipReveal",typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(transform,false);
    var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.045f);rect.sizeDelta=new Vector2(240,42);
    go.AddComponent<LayoutElement>().ignoreLayout=true;
    var image=go.GetComponent<Image>();image.color=Color.clear;
    skipReveal=go.GetComponent<Button>();skipReveal.targetGraphic=image;skipReveal.onClick.AddListener(CompleteReveal);
    var text=new GameObject("Label",typeof(RectTransform));text.transform.SetParent(go.transform,false);UiSkin.Stretch((RectTransform)text.transform);
    var label=text.AddComponent<TextMeshProUGUI>();UiSkin.Label(label,UiSkin.Role.Value,UiSkin.TextMuted);
    label.fontSizeMax=18;label.text="SKIP ANIMATION";label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
  }

  private void Update()
  {
    if(!revealing) return;
    float elapsed=Time.unscaledTime-revealStarted;
    AnimateReveal(elapsed);
    if(elapsed>=RevealDuration) CompleteReveal();
  }

  private void AnimateReveal(float elapsed)
  {
    for(int i=0;i<earnedStars && i<starsRow.childCount;i++)
    {
      float t=Mathf.Clamp01((elapsed-.12f-i*.23f)/.30f);
      float u=t-1f;
      float scale=t<=0?0:1+2.6f*u*u*u+1.6f*u*u;
      starsRow.GetChild(i).localScale=Vector3.one*scale;
      if(t>=1 && i>lastStar) {lastStar=i;Haptics.PlayThrottled(Haptics.Style.Light,.12f);}
    }
    if(payoutLabel!=null)
    {
      float t=Mathf.Clamp01((elapsed-.55f)/.85f);
      int coins=Mathf.RoundToInt(payout*(1-(1-t)*(1-t)));
      if(coins!=shownCoins){shownCoins=coins;payoutLabel.text=$"+{coins}";}
    }
  }

  // Presentation only: rewards were already credited by GameManager.
  public void CompleteReveal()
  {
    revealing=false;
    if(starsRow!=null) foreach(Transform star in starsRow) star.localScale=Vector3.one;
    if(payoutLabel!=null) payoutLabel.text=$"+{payout}";
    if(skipReveal!=null) skipReveal.gameObject.SetActive(false);
  }

  private void OnDisable() { CompleteReveal(); }

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

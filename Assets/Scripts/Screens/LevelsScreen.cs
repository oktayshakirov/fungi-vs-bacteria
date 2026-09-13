using TMPro;
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
    // Same gap as EnvironmentsScreen: this prefab carries no Canvas of its own,
    // so DisplaySetup's edit-time pass never wraps it in a SafeArea and the
    // level tiles could render under a notch. Must run before BuildHomeButton,
    // which looks for this.
    ScreenTheme.EnsureSafeArea(transform);

    ScreenTheme.ApplyListScreen(transform, backButton, dimBackground: false);
    ShowBiomeBackdrop();
    SetTitleToBiome();
    ShortenBackLabel();
    BuildHomeButton();
    UseGridLayout();
    PopulateLevelCards();
    HideScrollbars();

    if (backButton != null)
    {
      backButton.onClick.AddListener(OnBack);
    }

    ScreenFade.In(transform);
  }

  // The cards were laid out in one long horizontal strip, so only about eight
  // fit before scrolling. Two fixed rows fit a whole environment's levels on
  // screen at once.
  //
  // The rows read LEFT TO RIGHT (1 2 3 4 5 / 6 7 8 9 10). They used to fill a
  // column before moving right, which put level 2 UNDER level 1 - a level list
  // is read as a sequence, and a grid that numbers down its columns makes the
  // player parse the layout before they can find the next level.
  private void UseGridLayout()
  {
    if (cardsContainer == null) return;

    // DestroyImmediate, not Destroy: Destroy is deferred to the end of the
    // frame, so the old layout group is still attached when AddComponent runs
    // and Unity refuses to add a second LayoutGroup — leaving grid null.
    foreach (var strip in cardsContainer.GetComponents<HorizontalOrVerticalLayoutGroup>())
    {
      DestroyImmediate(strip);
    }

    var grid = cardsContainer.GetComponent<GridLayoutGroup>();
    if (grid == null) grid = cardsContainer.gameObject.AddComponent<GridLayoutGroup>();
    if (grid == null) return;

    // Sized to the canvas rather than to the 1280-unit reference. The canvas is
    // match-height, so a 4:3 tablet is only 960 units wide - five 170-wide
    // tiles plus their gaps come to 1010 and simply ran off both edges there.
    const int columns = 5;
    const float gap = 32f;
    const float sidePadding = 44f;
    float available = ScreenTheme.LayoutWidth(cardsContainer)
                      - sidePadding * 2f - gap * (columns - 1);
    LevelCard.SetTileSize(available / columns);

    grid.cellSize = new Vector2(LevelCard.TileSize, LevelCard.CellHeight);
    grid.spacing = new Vector2(gap, 14f);
    grid.padding = new RectOffset((int)sidePadding, (int)sidePadding, 12, 12);
    grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
    grid.startAxis = GridLayoutGroup.Axis.Horizontal;   // fill a row, then move down
    grid.childAlignment = TextAnchor.MiddleCenter;
    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
    grid.constraintCount = columns;

    // The content rect was sized for the old single-row strip, which left the
    // grid hanging off the left edge of the viewport. Centre it and let the
    // fitter size it to the cards.
    var content = (RectTransform)cardsContainer;
    content.anchorMin = new Vector2(0.5f, 0.5f);
    content.anchorMax = new Vector2(0.5f, 0.5f);
    content.pivot = new Vector2(0.5f, 0.5f);
    content.anchoredPosition = Vector2.zero;

    var fitter = content.GetComponent<ContentSizeFitter>();
    if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
    fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

    // Content is centred inside the viewport, so the viewport itself has to
    // cover the area under the title or the grid sits low on the screen.
    var scroll = content.GetComponentInParent<ScrollRect>();
    if (scroll != null)
    {
      scroll.vertical = false;   // two fixed rows; only horizontal overflow
      var view = (RectTransform)scroll.transform;
      view.anchorMin = Vector2.zero;
      view.anchorMax = Vector2.one;
      view.pivot = new Vector2(0.5f, 0.5f);
      view.offsetMin = new Vector2(0f, 24f);
      // Clear of the header band (the title chip's plate plus its glow).
      view.offsetMax = new Vector2(0f, -(ScreenTheme.HeaderInset + 88f + 26f));
    }
  }


  // The screen takes the biome's own art as its backdrop, so picking an
  // environment visibly takes you somewhere instead of opening a dark list.
  // ApplyListScreen is called with dimBackground:false for exactly this reason —
  // its scrim tints the inherited menu art, which sits BEHIND this, so it would
  // have done nothing here except waste a draw.
  private void ShowBiomeBackdrop()
  {
    // Repaint the prefab's OWN Background rather than inserting a new object.
    // A backdrop added as first sibling is drawn under the prefab's background,
    // which is opaque — the whole screen came back black. This also leaves
    // BackgroundFill ([ExecuteAlways], and it rewrites this rect every frame)
    // in charge of the sizing, which a hand-stretched rect would fight.
    foreach (Transform child in GetComponentsInChildren<Transform>(true))
    {
      if (child.name != "Background") continue;

      var image = child.GetComponent<Image>();
      if (image == null) continue;

      image.sprite = EnvironmentInfo.CardArt(GameSession.SelectedEnvironment);
      image.type = Image.Type.Simple;
      image.preserveAspect = false;
      // Darkened well below full brightness so the tiles stay the most
      // prominent thing on screen, but the biome's colour still reads.
      image.color = new Color(0.52f, 0.55f, 0.60f, 1f);
      image.raycastTarget = false;
      return;
    }
  }

  // Ten tiles always fit, so the bar is pure noise — and it rendered as a raw
  // unstyled strip across the bottom of the screen.
  //
  // Hidden by fading it, NOT by deactivating it or clearing ScrollRect's
  // reference: with AutoHideAndExpandViewport the ScrollRect resizes its own
  // viewport around the bar, so pulling it out from under the component moves
  // the viewport and the content with it.
  private void HideScrollbars()
  {
    foreach (var bar in GetComponentsInChildren<Scrollbar>(true))
    {
      var group = bar.GetComponent<CanvasGroup>();
      if (group == null) group = bar.gameObject.AddComponent<CanvasGroup>();
      group.alpha = 0f;
      group.interactable = false;
      group.blocksRaycasts = false;
    }
  }

  // The prefab's title is the literal placeholder "Levels"; the biome name is
  // both more useful and the thing that ties this screen to the card you tapped.
  private void SetTitleToBiome()
  {
    foreach (var label in GetComponentsInChildren<TMP_Text>(true))
    {
      if (label.gameObject.name != "ScreenTitle") continue;

      label.text = EnvironmentInfo.DisplayName(GameSession.SelectedEnvironment).ToUpperInvariant();
      label.textWrappingMode = TextWrappingModes.NoWrap;
      ScreenTheme.TitleChip(label, EnvironmentInfo.AccentFor(GameSession.SelectedEnvironment));
      return;
    }
  }

  // The prefab authors this as "< Environments". The Groovy display font has no
  // "<" glyph so it silently rendered as "ENVIRONMENTS", which is both wrong and
  // far too long for a corner button.
  private void ShortenBackLabel()
  {
    if (backButton == null) return;
    var label = backButton.GetComponentInChildren<TMP_Text>(true);
    if (label != null) label.text = "BACK";
  }

  // A shortcut straight to the main menu, so getting out does not mean stepping
  // back through the environment list first.
  private void BuildHomeButton()
  {
    var go = new GameObject("Home", typeof(RectTransform));
    Transform host = transform.Find("SafeArea") ?? transform;
    go.transform.SetParent(host, false);

    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(1f, 1f);
    rect.anchorMax = new Vector2(1f, 1f);
    rect.pivot = new Vector2(1f, 1f);
    rect.anchoredPosition = new Vector2(-ScreenTheme.HeaderInset, -ScreenTheme.HeaderInset);
    rect.sizeDelta = new Vector2(ScreenTheme.HomeButtonWidth, ScreenTheme.HeaderButtonHeight);

    go.AddComponent<Image>();
    var home = go.AddComponent<Button>();
    ScreenTheme.CornerButton(home);

    Image icon = UiSkin.Icon(go.transform, UiSprites.Home(), UiSkin.TextPrimary, 40f);
    icon.raycastTarget = false;

    // Above the scroll view, which ApplyListScreen stretches across the screen.
    go.transform.SetAsLastSibling();
    home.onClick.AddListener(OnHome);
  }

  private void OnHome()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);

    // The environments screen owns the reference to the menu it came from, so
    // it is the only thing that can put the player back there.
    if (EnvironmentsScreen.Instance != null)
    {
      EnvironmentsScreen.Instance.ReturnToMenu();
    }
    Destroy(gameObject);
  }

  private void PopulateLevelCards()
  {
    string environmentName = GameSession.SelectedEnvironment;
    Color accent = EnvironmentInfo.AccentFor(environmentName);
    List<LevelConfig> levels = LevelRepository.GetLevelsForEnvironment(environmentName);

    if (levels.Count == 0)
    {
      Debug.LogWarning($"No LevelConfig assets found in Resources/Levels for environment '{environmentName}'.");
      return;
    }

    // The first unlocked level with no stars yet is where the player left off;
    // it gets the bright ring so the eye lands on it straight away.
    int nextLevel = -1;
    foreach (LevelConfig level in levels)
    {
      bool unlocked = LevelProgress.IsLevelUnlocked(level.environmentName, level.levelNumber);
      if (unlocked && LevelProgress.GetStars(level.environmentName, level.levelNumber) <= 0)
      {
        nextLevel = level.levelNumber;
        break;
      }
    }

    foreach (LevelConfig level in levels)
    {
      GameObject cardGO = Instantiate(levelCardPrefab, cardsContainer);
      LevelCard card = cardGO.GetComponent<LevelCard>();
      if (card != null)
      {
        bool isLocked = !LevelProgress.IsLevelUnlocked(level.environmentName, level.levelNumber);
        int stars = LevelProgress.GetStars(level.environmentName, level.levelNumber);
        LevelConfig selected = level;
        card.Setup(level.levelNumber, isLocked, stars, accent,
          level.levelNumber == nextLevel, _ => OnLevelSelected(selected));
      }
      else
      {
        Debug.LogWarning("LevelCard component not found on the levelCardPrefab.");
      }
    }

    // Force the grid and the ContentSizeFitter to resolve NOW. Layout is
    // normally deferred to the end of the frame, which left the content rect at
    // (0,0) — the ten tiles existed but collapsed to zero size and were clipped
    // away by the viewport mask, giving an empty screen with no error anywhere.
    // EnvironmentsScreen has always done this; this screen was relying on
    // something else to trigger it.
    var content = cardsContainer as RectTransform;
    if (content != null)
    {
      Canvas.ForceUpdateCanvases();
      LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }
  }

  private void OnLevelSelected(LevelConfig level)
  {
    Debug.Log($"Selected Level: {level.levelNumber} ({level.environmentName})");
    GameSession.SelectedLevel = level;
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.LevelPicked);
    SceneController.Instance.LoadScene(SceneController.GameScene.MainGame);
    gameObject.SetActive(false);
  }

  private void OnBack()
  {
    Destroy(gameObject);
    if (EnvironmentsScreen.Instance != null)
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      EnvironmentsScreen.Instance.gameObject.SetActive(true);
    }
  }
}

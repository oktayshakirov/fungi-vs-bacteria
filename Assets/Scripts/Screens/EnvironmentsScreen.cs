using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EnvironmentsScreen : MonoBehaviour
{
  public static EnvironmentsScreen Instance;

  [Header("Environment Card Setup")]
  [SerializeField] private GameObject environmentCardPrefab;
  [SerializeField] private Transform cardsContainer;

  [Header("Next Screen")]
  [SerializeField] private GameObject levelsScreenPrefab;

  [System.Serializable]
  public class EnvironmentData
  {
    public Sprite environmentSprite;
    public string environmentName;
    public bool isLocked;
  }

  [Header("Environments Data")]
  [SerializeField] private List<EnvironmentData> environments = new List<EnvironmentData>();

  private void Awake()
  {
    Instance = this;
  }

  private GameObject returnTarget;
  private Button backButton;

  // The main menu hands itself over before deactivating, so Back can restore it.
  public void SetReturnTarget(GameObject menu)
  {
    returnTarget = menu;
  }

  private void Start()
  {
    // This screen carries no Canvas of its own — DisplaySetup's edit-time pass
    // never finds one to wrap, so its content never got a safe-area inset. On a
    // notched phone in landscape the environment cards could render right
    // under the notch. Must run before BuildBackButton, which looks for this.
    ScreenTheme.EnsureSafeArea(transform);

    // The prefab has no back button — this was the only screen in the game with
    // no way out except the OS back gesture — so one is built here and then
    // positioned by the same ApplyListScreen call the levels screen uses.
    backButton = BuildBackButton();
    ScreenTheme.ApplyListScreen(transform, backButton);
    StyleTitle();
    DimBackdrop();
    CentreCards();
    PopulateEnvironmentCards();

    // Start on Environment 1. The content has to be laid out first — setting
    // the scroll position before the size fitter has run leaves the row parked
    // half a card left of the viewport.
    var scroll = GetComponentInChildren<ScrollRect>(true);
    if (scroll != null)
    {
      Canvas.ForceUpdateCanvases();
      if (scroll.content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
      scroll.vertical = false;   // a horizontal strip should not drift vertically

      // Positioned directly rather than via horizontalNormalizedPosition: the
      // content is anchored left with a left pivot, so zero already means "left
      // edge against the viewport", and the normalised form kept landing a
      // fraction of a card short.
      if (scroll.content != null)
      {
        scroll.content.anchoredPosition = new Vector2(0f, scroll.content.anchoredPosition.y);
      }
      // The bar tracks the content and is not updated by moving the rect. It
      // also rendered as an unstyled strip across the bottom of the screen, so
      // it is hidden entirely — the strip is dragged, not scrubbed.
      if (scroll.horizontalScrollbar != null)
      {
        scroll.horizontalScrollbar.value = 0f;
        scroll.horizontalScrollbar.gameObject.SetActive(false);
        scroll.horizontalScrollbar = null;
      }
      if (scroll.verticalScrollbar != null)
      {
        scroll.verticalScrollbar.gameObject.SetActive(false);
        scroll.verticalScrollbar = null;
      }
    }

    ScreenFade.In(transform);
  }


  // The same header plate the levels screen uses, so the two steps of one flow
  // do not look like two different games.
  private void StyleTitle()
  {
    foreach (var label in GetComponentsInChildren<TMP_Text>(true))
    {
      if (label.gameObject.name != "ScreenTitle") continue;
      ScreenTheme.TitleChip(label, UiSkin.Accent);
      return;
    }
  }

  private Button BuildBackButton()
  {
    var go = new GameObject("Back", typeof(RectTransform));
    // Parented to the SafeArea when there is one, so the button clears a notch.
    Transform host = transform.Find("SafeArea") ?? transform;
    go.transform.SetParent(host, false);

    go.AddComponent<Image>();
    var button = go.AddComponent<Button>();

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    var label = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.ButtonLabel);
    label.text = "BACK";
    label.alignment = TextAlignmentOptions.Center;
    label.raycastTarget = false;
    UiSkin.Stretch((RectTransform)labelGo.transform);

    // Above the scroll view, which ApplyListScreen stretches across the screen.
    go.transform.SetAsLastSibling();

    button.onClick.AddListener(OnBack);
    return button;
  }

  private void OnBack()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    ReturnToMenu();
  }

  // This screen holds the only reference to the menu it was opened from, so it
  // is the only thing that can restore it. The levels screen's Home button
  // calls this to skip straight past the environment list.
  public void ReturnToMenu()
  {
    if (returnTarget != null) returnTarget.SetActive(true);
    Destroy(gameObject);
  }

  // Darkens the menu art so the cards read as the foreground. Done by tinting
  // the background image itself rather than adding an overlay object, which the
  // prefab's own draw order kept swallowing.
  private void DimBackdrop()
  {
    foreach (Transform child in GetComponentsInChildren<Transform>(true))
    {
      if (child.name != "Background") continue;

      var image = child.GetComponent<Image>();
      if (image != null) image.color = new Color(0.42f, 0.46f, 0.54f, 1f);
      return;
    }
  }

  // Left-aligned, not centred: there are seven environments, so the strip is
  // wider than the screen and centring pushed the first two off the left edge.
  // It starts at the left and scrolls right.
  private void CentreCards()
  {
    var content = cardsContainer as RectTransform;
    if (content == null) return;

    content.anchorMin = new Vector2(0f, 0.5f);
    content.anchorMax = new Vector2(0f, 0.5f);
    content.pivot = new Vector2(0f, 0.5f);
    content.anchoredPosition = Vector2.zero;

    // Cards were nearly half the screen tall. Let the row drive their size so
    // several fit at once and the art reads as a set of thumbnails.
    var row = content.GetComponent<HorizontalLayoutGroup>();
    if (row != null)
    {
      row.spacing = 34f;
      row.padding = new RectOffset(48, 48, 0, 0);
      row.childAlignment = TextAnchor.MiddleLeft;
      // The group drives both axes from each card's LayoutElement; with control
      // off, the prefab's own stretched rect won and the cards filled the screen.
      row.childControlWidth = true;
      row.childControlHeight = true;
      row.childForceExpandWidth = false;
      row.childForceExpandHeight = false;
      row.reverseArrangement = false;
    }

    var fitter = content.GetComponent<ContentSizeFitter>();
    if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
    fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
    fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

    // The prefab's scroll view starts left of the screen edge, so the content —
    // anchored to it — dragged the first card half off-screen. Frame the view
    // to the screen, under the title.
    var scroll = content.GetComponentInParent<ScrollRect>();
    if (scroll != null)
    {
      var view = (RectTransform)scroll.transform;
      view.anchorMin = Vector2.zero;
      view.anchorMax = Vector2.one;
      view.pivot = new Vector2(0.5f, 0.5f);
      view.offsetMin = new Vector2(0f, 96f);
      view.offsetMax = new Vector2(0f, -166f);
    }
  }

  private void PopulateEnvironmentCards()
  {
    foreach (var envData in environments)
    {
      // Never enable an environment with no levels yet (would open an empty list)
      var levels = LevelRepository.GetLevelsForEnvironment(envData.environmentName);
      bool hasLevels = levels.Count > 0;
      bool isLocked = !hasLevels || (envData.isLocked && !LevelProgress.UnlockAll);

      int completed = Mathf.Clamp(
        LevelProgress.GetHighestCompletedLevel(envData.environmentName), 0, levels.Count);

      GameObject cardGO = Instantiate(environmentCardPrefab, cardsContainer);
      EnvironmentCard card = cardGO.GetComponent<EnvironmentCard>();
      if (card != null)
      {
        card.Setup(envData.environmentName, isLocked, completed, levels.Count);
      }

      Button cardButton = cardGO.GetComponent<Button>();
      if (cardButton != null)
      {
        string envName = envData.environmentName;
        cardButton.onClick.AddListener(() => OnEnvironmentSelected(envName));
        if (isLocked)
        {
          cardButton.interactable = false;
        }
      }
    }
  }

  public void SetLevelSelectionPrefab(GameObject prefab)
  {
    levelsScreenPrefab = prefab;
  }

  private void OnEnvironmentSelected(string environmentName)
  {
    Debug.Log("Selected Environment: " + environmentName);
    GameSession.SelectedEnvironment = environmentName;
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.EnvironmentPicked);
    if (levelsScreenPrefab != null)
    {
      Instantiate(levelsScreenPrefab, transform.parent);
    }
    gameObject.SetActive(false);
  }
}

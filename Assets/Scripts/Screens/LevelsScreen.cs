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
    ScreenTheme.ApplyListScreen(transform, backButton, dimBackground: true);
    UseGridLayout();
    PopulateLevelCards();

    if (backButton != null)
    {
      backButton.onClick.AddListener(OnBack);
    }

    ScreenFade.In(transform);
  }

  // The cards were laid out in one long horizontal strip, so only about eight
  // fit before scrolling. Two fixed rows flowing horizontally fit a whole
  // environment's levels on screen at once.
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
    grid.cellSize = new Vector2(190f, 190f);
    grid.spacing = new Vector2(26f, 26f);
    grid.padding = new RectOffset(30, 30, 16, 16);
    grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
    grid.startAxis = GridLayoutGroup.Axis.Vertical;   // fill a column, then move right
    grid.childAlignment = TextAnchor.MiddleCenter;
    grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
    grid.constraintCount = 2;

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
      view.offsetMin = new Vector2(0f, 40f);
      view.offsetMax = new Vector2(0f, -170f);   // clear of the title
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
        int stars = LevelProgress.GetStars(level.environmentName, level.levelNumber);
        LevelConfig selected = level;
        card.Setup(level.levelNumber, isLocked, stars, _ => OnLevelSelected(selected));
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

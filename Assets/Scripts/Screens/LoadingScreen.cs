using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
  [SerializeField] private TextMeshProUGUI loadingText;
  [SerializeField] private Slider progressBar;
  [SerializeField] private TextMeshProUGUI progressText;

  private bool styled;
  private TMP_Text tipText;
  private Image background;
  private Sprite defaultBackground;
  private Color defaultBackgroundColor;

  // Short, true, and each one about something the game does not otherwise
  // explain - the variety enemies, upgrades, the support towers, continues.
  private static readonly string[] Tips =
  {
    "Ice towers slow a whole group. Put your hardest hitters just past them.",
    "Tap a placed tower to upgrade it, or sell it for part of its price.",
    "Shielded bacteria regrow their shield if you stop hitting them.",
    "Splitters burst into smaller cells where they die - splash damage cleans them up.",
    "Healers mend the bacteria around them. Take them out first.",
    "Aura and Defense towers boost every tower next to them.",
    "Switch to 2x speed to breeze through the early waves.",
    "Lost a run? You can continue - once free with an ad, then for coins.",
    "Three stars pays the most coins. Replay a level to raise your rating.",
    "Boosters from the store can turn a lost wave around. The Spore Bomb clears the board.",
  };

  private void Start()
  {
    EnsureStyled();
  }

  private void EnsureStyled()
  {
    if (styled) return;
    styled = true;
    Style();
    BuildTip();
    // The screen's backdrop is a Background carrying BackgroundFill (the slider
    // has a "Background" of its own). The prefab has two, stacked; the LAST in
    // hierarchy order draws on top and is the one that shows.
    var fills = GetComponentsInChildren<BackgroundFill>(true);
    var fill = fills.Length > 0 ? fills[fills.Length - 1] : null;
    background = fill != null ? fill.GetComponent<Image>() : null;
    if (background != null)
    {
      defaultBackground = background.sprite;
      defaultBackgroundColor = background.color;
    }
  }

  // Called by SceneController before every show. Going into a level, the
  // screen names it and sits over that biome's art; going back to the menu it
  // stays the plain LOADING screen. Both get a tip - the few seconds of load
  // are the only idle moment the game has to teach anything.
  public void Prepare(bool enteringLevel)
  {
    EnsureStyled();
    LevelConfig level = GameSession.SelectedLevel;
    bool named = enteringLevel && level != null;

    if (loadingText != null)
    {
      loadingText.text = named ? ScreenTheme.RunSummary(withWave: false) : "LOADING...";
    }

    if (background != null)
    {
      if (named)
      {
        background.sprite = EnvironmentInfo.CardArt(level.environmentName);
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        // Dimmed well down: the labels sit straight on top of it.
        background.color = new Color(0.30f, 0.32f, 0.38f, 1f);
      }
      else
      {
        background.sprite = defaultBackground;
        background.color = defaultBackgroundColor;
      }
    }

    // BackgroundFill only re-fits when the canvas changes size; a new sprite
    // with a different aspect needs it re-run, or the art stretches.
    var fit = background != null ? background.GetComponent<BackgroundFill>() : null;
    if (fit != null)
    {
      fit.enabled = false;
      fit.enabled = true;
    }

    if (tipText != null)
    {
      tipText.text = $"<color=#9ED0FF>TIP</color>   {Tips[Random.Range(0, Tips.Length)]}";
    }

    if (progressBar != null) progressBar.value = 0f;
    if (progressText != null) progressText.text = "0%";
  }

  private void BuildTip()
  {
    Transform parent = loadingText != null ? loadingText.transform.parent : transform;
    var go = new GameObject("Tip", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var rect = (RectTransform)go.transform;
    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = new Vector2(0f, -200f);
    rect.sizeDelta = new Vector2(940f, 80f);
    tipText = go.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(tipText, UiSkin.Role.Body, UiSkin.TextPrimary);
    tipText.fontSizeMax = 26f;
    tipText.alignment = TextAlignmentOptions.Center;
    tipText.richText = true;
    tipText.outlineWidth = 0.15f;
    tipText.outlineColor = new Color32(10, 12, 20, 200);
    tipText.raycastTarget = false;
  }

  // The slider shipped with Unity's default flat sprites; rounding the track
  // and fill and skinning the two labels brings it in line with the rest.
  private void Style()
  {
    if (loadingText != null)
    {
      UiSkin.Label(loadingText, UiSkin.Role.Heading);
      loadingText.alignment = TextAlignmentOptions.Center;
      loadingText.outlineWidth = 0.2f;
      loadingText.outlineColor = new Color32(10, 12, 20, 220);
    }

    if (progressText != null)
    {
      UiSkin.Label(progressText, UiSkin.Role.Value, UiSkin.TextPrimary);
      progressText.alignment = TextAlignmentOptions.Center;
    }

    if (progressBar == null) return;

    // The three pieces live under different containers in the prefab, so
    // centring them in place leaves them scattered. Gather them onto one parent
    // first, then lay them out as a block.
    Transform root = loadingText != null ? loadingText.transform.parent : progressBar.transform.parent;
    if (root != null)
    {
      progressBar.transform.SetParent(root, false);
      if (progressText != null) progressText.transform.SetParent(root, false);
    }

    Centre((RectTransform)progressBar.transform, new Vector2(0f, -40f), new Vector2(900f, 34f));
    if (loadingText != null) Centre(loadingText.rectTransform, new Vector2(0f, 40f), new Vector2(900f, 70f));
    if (progressText != null) Centre(progressText.rectTransform, new Vector2(0f, -104f), new Vector2(300f, 50f));

    // Starts empty; the prefab's authored value showed a full bar at 0%
    progressBar.minValue = 0f;
    progressBar.maxValue = 1f;
    progressBar.value = 0f;

    // The track is whichever child Image is neither the fill nor the handle
    int tracks = 0;
    foreach (Image image in progressBar.GetComponentsInChildren<Image>(true))
    {
      bool isFill = progressBar.fillRect != null && image.transform.IsChildOf(progressBar.fillRect);
      bool isHandle = progressBar.handleRect != null && image.transform.IsChildOf(progressBar.handleRect);
      if (isFill || isHandle) continue;

      // Neutral, not PanelDark: the loading backdrop is already dark, so a dark
      // track was invisible and only the fill nub showed. The prefab also ships
      // this object disabled, so it has to be switched on.
      image.gameObject.SetActive(true);
      image.enabled = true;
      UiSkin.Panel(image, UiSkin.Neutral, UiSkin.RadiusChip);
      UiSkin.Stretch(image.rectTransform);
      tracks++;
    }

    // This slider ships with only a Fill Area and a Handle, so at 0% there was
    // nothing on screen but a tiny green nub. Give it a track of its own.
    if (tracks == 0)
    {
      var trackGo = new GameObject("Track", typeof(RectTransform));
      trackGo.transform.SetParent(progressBar.transform, false);
      trackGo.transform.SetAsFirstSibling();
      var track = trackGo.AddComponent<Image>();
      UiSkin.Panel(track, UiSkin.Neutral, UiSkin.RadiusChip);
      track.raycastTarget = false;
      UiSkin.Stretch(track.rectTransform);
    }

    if (progressBar.fillRect != null)
    {
      var fill = progressBar.fillRect.GetComponent<Image>();
      if (fill != null) UiSkin.Panel(fill, UiSkin.Primary, UiSkin.RadiusChip);
    }

    // The default slider ships a draggable handle; a progress bar has no grab
    if (progressBar.handleRect != null) progressBar.handleRect.gameObject.SetActive(false);
    progressBar.interactable = false;
    progressBar.transition = Selectable.Transition.None;
  }

  private static void Centre(RectTransform rect, Vector2 position, Vector2 size)
  {
    if (rect == null) return;
    rect.anchorMin = new Vector2(0.5f, 0.5f);
    rect.anchorMax = new Vector2(0.5f, 0.5f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = position;
    rect.sizeDelta = size;
  }

  public void UpdateProgress(float progress)
  {
    // AsyncOperation.progress stops at 0.9 until activation, so the raw value
    // never read higher than 90%.
    float shown = Mathf.Clamp01(progress / 0.9f);
    progressBar.value = shown;
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.Loading);
    progressText.text = $"{(shown * 100):0}%";
  }
}

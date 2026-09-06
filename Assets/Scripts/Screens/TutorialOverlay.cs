using UnityEngine;
using UnityEngine.UI;
using TMPro;

// First-run tutorial. Built entirely at runtime under the HUD canvas,
// so no prefab or scene wiring is required.
//
// Laid out as a card rather than as text floating on a black screen: the old
// version dimmed the whole board to 80% and centred bare white text over it,
// which read as an error dialog and hid the very thing each step is talking
// about. The card sits low, the scrim is light enough to still see the board
// through it, and a step counter plus a Skip button mean a returning player is
// never trapped in three taps of copy they have already read.
public class TutorialOverlay : MonoBehaviour
{
  private const string CompletedKey = "TutorialCompleted";

  // Width the towers panel claims down the right-hand edge, so the card can be
  // centred on what is left. Kept in step with HudTheme's frame by eye rather
  // than read from it: the panel is built by the HUD, which the tutorial has no
  // handle on, and being a little conservative here costs nothing.
  private const float TowersColumn = 380f;

  private static readonly string[] Steps =
  {
    "Bacteria are coming down the path.\n\nDrag a fungus from the panel on the right onto any free tile - or tap it, then tap the tile.",
    "Towers cost coins.\n\nEvery bacterium you kill and every wave you clear pays out more, so keep building as the level goes on.",
    "Press START WAVE when you are ready.\n\nAnything that reaches the end of the path costs you health. Lose it all and the run is over."
  };

  private TMP_Text stepText;
  private TMP_Text counterText;
  private Transform pipRow;
  private int currentStep;

  public static bool ShouldShow()
  {
    return PlayerPrefs.GetInt(CompletedKey, 0) == 0;
  }

  public static void Show(Transform canvasParent)
  {
    var root = new GameObject("TutorialOverlay", typeof(RectTransform));
    root.transform.SetParent(canvasParent, false);
    root.AddComponent<TutorialOverlay>();
  }

  private void Awake()
  {
    UiSkin.Stretch((RectTransform)transform);
    transform.SetAsLastSibling();

    // Lighter than the old 80% black. The steps talk about the path, the tile
    // grid and the Start Wave button, so the board has to stay readable behind
    // the card - a near-opaque scrim made every instruction abstract.
    Image scrim = gameObject.AddComponent<Image>();
    scrim.color = new Color(0.04f, 0.05f, 0.09f, 0.55f);

    // The whole scrim advances the tutorial, so there is no "where do I tap".
    Button advance = gameObject.AddComponent<Button>();
    advance.transition = Selectable.Transition.None;
    advance.onClick.AddListener(NextStep);

    BuildCard();
    ShowStep(0);
  }

  private void BuildCard()
  {
    var cardGo = new GameObject("Card", typeof(RectTransform));
    cardGo.transform.SetParent(transform, false);

    // Anchored bottom-left and centred over the BOARD rather than over the
    // screen. Two reasons, both of which only bite on a narrow (4:3) canvas,
    // where the matched-height scaling leaves the least width: a
    // screen-centred card runs over the towers panel on the right - which step
    // one is literally pointing at - and it would sit a single unit above the
    // Start Wave button that step three is about.
    var card = (RectTransform)cardGo.transform;
    card.anchorMin = new Vector2(0f, 0f);
    card.anchorMax = new Vector2(0f, 0f);
    card.pivot = new Vector2(0.5f, 0f);

    float available = ((RectTransform)transform).rect.width;
    float boardWidth = Mathf.Max(available - TowersColumn, 420f);
    card.sizeDelta = new Vector2(Mathf.Clamp(boardWidth - 80f, 460f, 780f), 292f);
    card.anchoredPosition = new Vector2(boardWidth * 0.5f, 128f);

    UiSkin.Panel(cardGo.AddComponent<Image>(), UiSkin.PanelDark, UiSkin.RadiusPanel);
    UiSkin.AddBorder(card, UiSkin.RadiusPanel);

    var layout = cardGo.AddComponent<VerticalLayoutGroup>();
    layout.padding = new RectOffset(34, 34, 24, 22);
    layout.spacing = 12f;
    layout.childAlignment = TextAnchor.UpperCenter;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = true;
    layout.childForceExpandHeight = false;

    counterText = Label(cardGo.transform, "Counter", UiSkin.Role.Caption, UiSkin.Accent, 24f);
    counterText.alignment = TextAlignmentOptions.Center;

    stepText = Label(cardGo.transform, "StepText", UiSkin.Role.Body, UiSkin.TextPrimary, 132f);
    stepText.alignment = TextAlignmentOptions.Top;

    BuildPips(cardGo.transform);
    BuildSkip(cardGo.transform);
  }

  // A dot per step, so the player can see this is three screens and not an
  // unbounded wall of tutorial.
  private void BuildPips(Transform parent)
  {
    var go = new GameObject("Pips", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    pipRow = go.transform;

    var row = go.AddComponent<HorizontalLayoutGroup>();
    row.spacing = 10f;
    row.childAlignment = TextAnchor.MiddleCenter;
    row.childControlWidth = false;
    row.childControlHeight = false;
    row.childForceExpandWidth = false;
    row.childForceExpandHeight = false;
    go.AddComponent<LayoutElement>().preferredHeight = 18f;

    for (int i = 0; i < Steps.Length; i++)
    {
      var pipGo = new GameObject("Pip", typeof(RectTransform));
      pipGo.transform.SetParent(go.transform, false);
      ((RectTransform)pipGo.transform).sizeDelta = new Vector2(14f, 14f);

      var pip = pipGo.AddComponent<Image>();
      pip.sprite = UiSprites.Circle();
      pip.raycastTarget = false;
      pip.preserveAspect = true;
    }
  }

  private void BuildSkip(Transform parent)
  {
    var go = new GameObject("Skip", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    go.AddComponent<Image>();

    var button = go.AddComponent<Button>();
    UiSkin.StyleButton(button, UiSkin.Primary, UiSkin.RadiusButton);
    go.AddComponent<LayoutElement>().preferredHeight = 62f;

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    var label = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.ButtonLabel);
    label.text = "GOT IT";
    label.alignment = TextAlignmentOptions.Midline;
    label.raycastTarget = false;
    UiSkin.Stretch((RectTransform)labelGo.transform);

    button.onClick.AddListener(NextStep);
  }

  private static TMP_Text Label(Transform parent, string name, UiSkin.Role role,
    Color color, float height)
  {
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var label = go.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, role, color);
    label.raycastTarget = false;
    go.AddComponent<LayoutElement>().preferredHeight = height;
    return label;
  }

  private void ShowStep(int step)
  {
    currentStep = step;
    stepText.text = Steps[step];
    counterText.text = $"STEP {step + 1} OF {Steps.Length}";

    for (int i = 0; i < pipRow.childCount; i++)
    {
      var pip = pipRow.GetChild(i).GetComponent<Image>();
      if (pip != null) pip.color = i <= step ? UiSkin.Primary : UiSkin.Neutral;
    }

    // Layout is deferred to the end of the frame, and the card is built and
    // filled inside a single Awake - without this the text measures against a
    // zero-size rect on the first step (see HANDOFF).
    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
  }

  private void NextStep()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);

    if (currentStep + 1 < Steps.Length)
    {
      ShowStep(currentStep + 1);
      return;
    }

    PlayerPrefs.SetInt(CompletedKey, 1);
    PlayerPrefs.Save();
    Destroy(gameObject);
  }
}

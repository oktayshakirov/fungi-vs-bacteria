using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uses the existing bottom-left information slot. Tower and booster panels
// take precedence; the forecast returns when the player finishes inspecting.
public class WavePreview : MonoBehaviour
{
  private RectTransform card;
  private Transform entries;
  private TMP_Text title, hint;
  private Button scout;
  private TMP_Text scoutLabel;
  private HUDManager hud;
  private bool requested;
  private bool available;
  private WaveConfig.Wave bound;

  public static WavePreview Create(Transform host, TMP_Text timer, HUDManager hud)
  {
    var root = new GameObject("WavePreview", typeof(RectTransform));
    root.transform.SetParent(host, false);
    UiSkin.Stretch((RectTransform)root.transform);
    var view = root.AddComponent<WavePreview>();
    view.hud = hud;
    view.Build(timer);
    return view;
  }

  private void Build(TMP_Text timer)
  {
    // Reuse the timer's established top HUD slot as a touch-sized scout button.
    scout = timer.gameObject.AddComponent<Button>();
    var plate = HudTheme.Backdrop(timer.rectTransform, UiSkin.PanelDark, UiSkin.RadiusButton, new Vector2(8, 3));
    scout.targetGraphic = plate;
    timer.raycastTarget = true;
    timer.fontSize = 17;
    timer.textWrappingMode = TextWrappingModes.NoWrap;
    timer.enableAutoSizing = false;
    timer.outlineWidth = 0;
    scoutLabel = timer;
    scout.onClick.AddListener(() => { requested = !requested; ApplyVisibility(); });

    card = SelectionScreenView.Rect("Forecast", transform, Vector2.zero, Vector2.zero);
    card.pivot = Vector2.zero;
    card.anchoredPosition = new Vector2(HudTheme.EdgeMargin, HudTheme.EdgeMargin);
    card.sizeDelta = new Vector2(400, 196);
    UiSkin.Panel(card.gameObject.AddComponent<Image>(), UiSkin.PanelDark);
    title = SelectionScreenView.Label(SelectionScreenView.Rect("Title", card, new Vector2(.035f,.79f), new Vector2(.80f,.98f)), "", 20, true);
    title.color = UiSkin.Gold;
    var hide=SelectionScreenView.Button(SelectionScreenView.Rect("Close",card,new Vector2(.86f,.79f),new Vector2(.97f,.96f)), "HIDE", UiSkin.Neutral,
      () => { requested = false; ApplyVisibility(); });
    var close=(RectTransform)hide.transform;close.anchorMin=new Vector2(0,1);close.anchorMax=Vector2.one;
    close.offsetMin=new Vector2(344,-36);close.offsetMax=new Vector2(-12,-4);
    var row = SelectionScreenView.Rect("Enemies",card,new Vector2(.03f,.29f),new Vector2(.97f,.78f));
    var layout = row.gameObject.AddComponent<GridLayoutGroup>();
    layout.cellSize = new Vector2(90, 94);
    layout.spacing = new Vector2(4, 3);
    layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
    layout.constraintCount = 4;
    entries = row;
    hint = SelectionScreenView.Label(SelectionScreenView.Rect("CounterHint",card,new Vector2(.035f,.02f),new Vector2(.965f,.28f)), "", 16);
    hint.color = UiSkin.TextPrimary;
    card.gameObject.SetActive(false);
  }

  public void Bind(WaveConfig.Wave wave, int number, bool preparation)
  {
    available = wave != null;
    scout.interactable = available;
    if (wave != bound)
    {
      bound = wave;
      foreach (Transform child in entries) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
      if (wave != null)
      {
        var kinds = WaveIntel.Entries(wave);
        bool twoRows = kinds.Count > 4;
        card.sizeDelta = new Vector2(400, twoRows ? 294 : 196);
        var entryRect = (RectTransform)entries;
        entryRect.anchorMin=new Vector2(0,1);entryRect.anchorMax=Vector2.one;
        entryRect.offsetMin=new Vector2(12,-42-(twoRows?191:94));entryRect.offsetMax=new Vector2(-12,-42);
        title.rectTransform.anchorMin=new Vector2(0,1);title.rectTransform.anchorMax=Vector2.one;
        title.rectTransform.offsetMin=new Vector2(14,-36);title.rectTransform.offsetMax=new Vector2(-80,-4);
        hint.rectTransform.anchorMax = new Vector2(.965f, twoRows ? .19f : .26f);
        title.text = $"NEXT WAVE {number}  /  {WaveIntel.Count(wave)} incoming";
        hint.text = WaveIntel.Hint(wave);
        foreach (var kind in kinds)
        {
          var tile = new GameObject(kind.config.name, typeof(RectTransform));
          tile.transform.SetParent(entries, false);
          var portrait = SelectionScreenView.Rect("Portrait",tile.transform,new Vector2(.23f,.49f),new Vector2(.77f,1));
          var icon = portrait.gameObject.AddComponent<Image>();
          icon.sprite = WaveIntel.Portrait(kind.config);
          icon.preserveAspect = true;
          icon.raycastTarget = false;
          var label = SelectionScreenView.Label(SelectionScreenView.Rect("Count",tile.transform,Vector2.zero,new Vector2(1,.45f)),
            $"{WaveIntel.Name(kind.config)}\nx{kind.count}", 15, true);
          label.alignment = TextAlignmentOptions.Center;
          label.enableAutoSizing = true;
          label.fontSizeMin = 14;
          label.fontSizeMax = 15;
          label.textWrappingMode = TextWrappingModes.NoWrap;
        }
      }
    }
    requested = preparation && available;
    ApplyVisibility();
  }

  public void SetStatus(string status) { if (scoutLabel.text != status) scoutLabel.text = status; }
  private void LateUpdate() { ApplyVisibility(); }
  public void ApplyVisibility()
  {
    bool busy = PlacementCancelButton.IsOpen || BoosterPanel.IsOpen || BasePanel.IsOpen
      || (hud != null && hud.HasTowerSelection);
    card.gameObject.SetActive(available && requested && !busy);
  }
}

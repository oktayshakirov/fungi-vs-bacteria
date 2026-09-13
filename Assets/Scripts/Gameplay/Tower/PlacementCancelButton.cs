using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TowerDefense.UI;

// The bar shown while a tower is armed for placement: what the tower is, what
// it does, what it costs, and a way to back out.
//
// It absorbed the tower info box rather than adding a second floating widget:
// the moment a player has picked a tower and is looking for a spot is exactly
// when "what does this one actually do?" matters, and one panel instead of two
// is one less thing that can overlap something else.
//
// Anchored bottom-LEFT, not bottom-centre where the bare Cancel button used to
// sit. Centre is not safe: the canvas is matched-height so its WIDTH shrinks on
// a 4:3 tablet, and a centred bar there runs into whatever is in both corners.
// The bottom-left corner belongs to this panel and to TowerActions alone since
// Start Wave moved under the towers rail.
//
// Its size, position and every row in it come from TowerInfoPanel, which
// TowerActions builds from as well - the two are one panel to the player and
// must not drift apart. See that file.
public class PlacementCancelButton : MonoBehaviour
{
  private static PlacementCancelButton instance;

  public static void Show(TowerConfig config, Action onCancel)
  {
    if (instance == null)
    {
      Canvas canvas = FindHudCanvas();
      if (canvas == null) return;

      var go = new GameObject("PlacementBar", typeof(RectTransform));
      go.transform.SetParent(canvas.transform, false);
      instance = go.AddComponent<PlacementCancelButton>();
      instance.Build();
    }

    instance.onCancel = onCancel;
    instance.SetTower(config);
    instance.gameObject.SetActive(true);
    instance.transform.SetAsLastSibling();
  }

  public static void Hide()
  {
    if (instance != null) instance.gameObject.SetActive(false);
  }

  private Action onCancel;
  private TMP_Text nameLabel;
  private TMP_Text costLabel;
  private TMP_Text descriptionLabel;
  private TMP_Text statsLabel;

  private void Build()
  {
    TowerInfoPanel.Place((RectTransform)transform, TowerInfoPanel.BaseHeight);
    TowerInfoPanel.Frame(gameObject);

    TMP_Text name = null;
    TowerInfoPanel.Header(transform, ref name, out costLabel);
    nameLabel = name;

    descriptionLabel = TowerInfoPanel.Description(transform);
    statsLabel = TowerInfoPanel.Stats(transform);

    Transform actions = TowerInfoPanel.Actions(transform);

    var cancelGo = new GameObject("Cancel", typeof(RectTransform));
    cancelGo.transform.SetParent(actions, false);
    cancelGo.AddComponent<Image>();
    var button = cancelGo.AddComponent<Button>();

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(cancelGo.transform, false);
    var cancelLabel = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(cancelLabel, UiSkin.Role.ButtonLabel);
    cancelLabel.text = "CANCEL";
    cancelLabel.raycastTarget = false;

    TowerInfoPanel.StyleAction(button, UiSkin.Danger);

    button.onClick.AddListener(() =>
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      onCancel?.Invoke();
    });
  }

  private void SetTower(TowerConfig config)
  {
    if (config == null) return;

    nameLabel.text = config.towerName;
    costLabel.text = config.cost.ToString();
    descriptionLabel.text = string.IsNullOrWhiteSpace(config.description)
      ? "Place it on any free tile."
      : config.description;
    statsLabel.text = TowerInfoPanel.StatLine(config);
  }

  private static Canvas FindHudCanvas()
  {
    Canvas best = null;
    Canvas anyCanvas = null;

    foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
    {
      if (anyCanvas == null || canvas.sortingOrder < anyCanvas.sortingOrder) anyCanvas = canvas;
      if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
      // Prefer the lowest-sorting overlay canvas (the main HUD, not popups)
      if (best == null || canvas.sortingOrder < best.sortingOrder) best = canvas;
    }

    // Falling back to a non-overlay canvas rather than giving up: the HUD is
    // ScreenSpaceOverlay in the real game, but UiPreview renders through a
    // ScreenSpaceCamera canvas (an overlay canvas draws straight to the
    // backbuffer and never lands in a RenderTexture - see UiPreview's header),
    // so without this the bar could not be previewed at all.
    return best != null ? best : anyCanvas;
  }
}

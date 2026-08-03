using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A persistent HUD control that toggles the game speed between 1x and 2x.
// Built at runtime and stacked under the stats chips.
public class GameSpeedButton : MonoBehaviour
{
  private TMP_Text label;
  private Image glyph;
  private Button button;

  public static void Create(Transform canvasParent, RectTransform below, int slot)
  {
    var go = new GameObject("GameSpeedButton", typeof(RectTransform));
    go.transform.SetParent(canvasParent, false);
    HudTheme.PlaceUnder((RectTransform)go.transform, below, slot);
    go.AddComponent<GameSpeedButton>().Build();
  }

  private void Build()
  {
    transform.SetAsLastSibling(); // draw above the HUD panels already in the canvas

    button = UiSkin.IconButton(gameObject, UiSprites.FastForward(), UiSkin.Neutral, out label);
    glyph = GetComponentInChildren<Image>() != null ? FindGlyph() : null;
    button.onClick.AddListener(OnClick);

    UpdateLabel(GameManager.Instance != null ? GameManager.Instance.PlaySpeed : 1f);
  }

  // The icon is the first child Image; the button's own background is on this
  // object, so GetComponentInChildren would return the background instead.
  private Image FindGlyph()
  {
    foreach (Transform child in transform)
    {
      var image = child.GetComponent<Image>();
      if (image != null) return image;
    }
    return null;
  }

  private void OnClick()
  {
    if (GameManager.Instance == null) return;
    float speed = GameManager.Instance.ToggleSpeed();
    UpdateLabel(speed);
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
  }

  // Highlighted only while fast-forwarding, so the colour means "boosted"
  // rather than just being a permanently loud button.
  private void UpdateLabel(float speed)
  {
    bool fast = Mathf.Approximately(speed, 2f);
    label.text = fast ? "2x" : "1x";

    Color tint = fast ? UiSkin.Accent : UiSkin.Neutral;
    UiSkin.StyleButton(button, tint, UiSkin.RadiusChip);
    if (glyph != null) glyph.color = fast ? UiSkin.TextDark : UiSkin.TextPrimary;
  }
}

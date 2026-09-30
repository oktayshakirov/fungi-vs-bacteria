using UnityEngine;
using UnityEngine.UI;

// HUD button that opens the store mid-level. A player who runs out of coins
// mid-wave had no way to reach the store without pausing and backing out to
// the menu.
//
// It used to be a labelled plate in the left-hand stack under the speed and
// camera controls. It sits in the TOP RIGHT now, immediately left of pause and
// the same size: the two are both "leave the board for a moment" controls, the
// left stack was three deep and crowding the booster bar, and a square glyph
// button beside pause reads as part of the same corner rather than as a third
// kind of control.
public class StoreHudButton : MonoBehaviour
{
  public static void Create(Transform canvasParent, RectTransform below, int slot)
  {
    // `below` and `slot` are kept for symmetry with the other runtime HUD
    // controls; this one measures off the pause button instead - see
    // HudTheme.PlaceLeftOfPause.
    var go = new GameObject("StoreHudButton", typeof(RectTransform));
    go.transform.SetParent(canvasParent, false);
    HudTheme.PlaceLeftOfPause((RectTransform)go.transform);
    go.AddComponent<StoreHudButton>().Build(canvasParent);
  }

  private Transform canvasParent;

  private void Build(Transform parent)
  {
    canvasParent = parent;
    transform.SetAsLastSibling(); // draw above the HUD panels already in the canvas

    gameObject.AddComponent<Image>();
    var button = gameObject.AddComponent<Button>();

    // Gold, matching the "VS" in the menu title - the one colour in the skin
    // that already reads as "special/currency-adjacent" rather than a neutral
    // control tint. The pause button beside it stays neutral, so the two are
    // never mistaken for each other at a glance.
    UiSkin.StyleButton(button, UiSkin.Gold, UiSkin.RadiusButton);

    Image glyph = UiSkin.Icon(transform, UiSprites.Bag(), UiSkin.TextDark, 34f);
    glyph.raycastTarget = false;
    var glyphRect = (RectTransform)glyph.transform;
    glyphRect.anchorMin = glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
    glyphRect.anchoredPosition = Vector2.zero;

    button.onClick.AddListener(OnClick);
  }

  private void OnClick()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    WalletScreen.OpenStore(canvasParent);
  }
}

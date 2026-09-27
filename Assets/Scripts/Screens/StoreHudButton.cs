using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD button that opens the store mid-level, stacked under the speed/camera
// buttons. A player who runs out of coins mid-wave had no way to reach the
// store without pausing and backing out to the menu.
public class StoreHudButton : MonoBehaviour
{
  private TMP_Text label;

  public static void Create(Transform canvasParent, RectTransform below, int slot)
  {
    var go = new GameObject("StoreHudButton", typeof(RectTransform));
    go.transform.SetParent(canvasParent, false);
    HudTheme.PlaceUnder((RectTransform)go.transform, below, slot);
    go.AddComponent<StoreHudButton>().Build(canvasParent);
  }

  private Transform canvasParent;

  private void Build(Transform parent)
  {
    canvasParent = parent;
    transform.SetAsLastSibling(); // draw above the HUD panels already in the canvas

    // Gold, matching the "VS" in the menu title - the one colour in the skin
    // that already reads as "special/currency-adjacent" rather than a neutral
    // control tint.
    Button button = UiSkin.IconButton(gameObject, UiSprites.Bag(), UiSkin.Gold, out label);
    label.text = "STORE";
    button.onClick.AddListener(OnClick);
  }

  private void OnClick()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    WalletScreen.Open(canvasParent);
  }
}

using UnityEngine;
using TMPro;

// HUD button that cycles the camera through its angle presets (cinematic /
// isometric / angled). Built at runtime, stacked under the speed button.
public class CameraViewButton : MonoBehaviour
{
  private TMP_Text label;

  public static void Create(Transform canvasParent, RectTransform below, int slot)
  {
    var go = new GameObject("CameraViewButton", typeof(RectTransform));
    go.transform.SetParent(canvasParent, false);
    HudTheme.PlaceUnder((RectTransform)go.transform, below, slot);
    go.AddComponent<CameraViewButton>().Build();
  }

  private void Build()
  {
    transform.SetAsLastSibling(); // draw above the HUD panels already in the canvas

    var button = UiSkin.IconButton(gameObject, UiSprites.Camera(), UiSkin.Neutral, out label);
    button.onClick.AddListener(OnClick);
    label.text = "1";
  }

  private void OnClick()
  {
    if (CameraRig.Instance == null) return;
    int index = CameraRig.Instance.CycleView();
    label.text = (index + 1).ToString();
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
  }
}

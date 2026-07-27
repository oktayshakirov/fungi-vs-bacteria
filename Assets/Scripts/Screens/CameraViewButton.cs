using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HUD button that cycles the camera through its angle presets (cinematic /
// isometric / angled). Built at runtime, anchored top-left under the speed button.
public class CameraViewButton : MonoBehaviour
{
  private TextMeshProUGUI label;

  public static void Create(Transform canvasParent)
  {
    var go = new GameObject("CameraViewButton", typeof(RectTransform));
    go.transform.SetParent(canvasParent, false);
    go.AddComponent<CameraViewButton>().Build();
  }

  private void Build()
  {
    var rect = (RectTransform)transform;
    rect.anchorMin = new Vector2(0f, 1f);
    rect.anchorMax = new Vector2(0f, 1f);
    rect.pivot = new Vector2(0f, 1f);
    rect.anchoredPosition = new Vector2(20f, -240f); // below the speed button
    rect.sizeDelta = new Vector2(150f, 80f);

    var image = gameObject.AddComponent<Image>();
    image.color = new Color(0.35f, 0.2f, 0.5f, 0.9f);

    var button = gameObject.AddComponent<Button>();
    button.targetGraphic = image;
    button.onClick.AddListener(OnClick);

    var textGo = new GameObject("Label", typeof(RectTransform));
    textGo.transform.SetParent(transform, false);
    var textRect = (RectTransform)textGo.transform;
    textRect.anchorMin = Vector2.zero;
    textRect.anchorMax = Vector2.one;
    textRect.offsetMin = Vector2.zero;
    textRect.offsetMax = Vector2.zero;

    label = textGo.AddComponent<TextMeshProUGUI>();
    UiFont.Apply(label);
    label.fontSize = 38f;
    label.enableAutoSizing = true;
    label.fontSizeMin = 18f;
    label.fontSizeMax = 38f;
    label.alignment = TextAlignmentOptions.Center;
    label.fontStyle = FontStyles.Bold;
    label.color = Color.white;
    label.raycastTarget = false;
    label.text = "VIEW";
  }

  private void OnClick()
  {
    if (CameraRig.Instance == null) return;
    int index = CameraRig.Instance.CycleView();
    label.text = $"VIEW {index + 1}";
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
  }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A persistent HUD control that toggles the game speed between 1x and 2x.
// Built at runtime, anchored top-left under the stats panel.
public class GameSpeedButton : MonoBehaviour
{
  private TextMeshProUGUI label;

  public static void Create(Transform canvasParent)
  {
    var go = new GameObject("GameSpeedButton", typeof(RectTransform));
    go.transform.SetParent(canvasParent, false);
    go.AddComponent<GameSpeedButton>().Build();
  }

  private void Build()
  {
    var rect = (RectTransform)transform;
    rect.anchorMin = new Vector2(0f, 1f);
    rect.anchorMax = new Vector2(0f, 1f);
    rect.pivot = new Vector2(0f, 1f);
    rect.anchoredPosition = new Vector2(20f, -150f); // below the stats panel
    rect.sizeDelta = new Vector2(150f, 80f);

    var image = gameObject.AddComponent<Image>();
    image.color = new Color(0.15f, 0.4f, 0.5f, 0.9f);

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
    label.fontSize = 44f;
    label.enableAutoSizing = true;
    label.fontSizeMin = 20f;
    label.fontSizeMax = 44f;
    label.alignment = TextAlignmentOptions.Center;
    label.fontStyle = FontStyles.Bold;
    label.color = Color.white;
    label.raycastTarget = false;

    UpdateLabel(GameManager.Instance != null ? GameManager.Instance.PlaySpeed : 1f);
  }

  private void OnClick()
  {
    if (GameManager.Instance == null) return;
    float speed = GameManager.Instance.ToggleSpeed();
    UpdateLabel(speed);
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
  }

  private void UpdateLabel(float speed)
  {
    label.text = Mathf.Approximately(speed, 2f) ? "FAST 2x" : "SPEED 1x";
  }
}

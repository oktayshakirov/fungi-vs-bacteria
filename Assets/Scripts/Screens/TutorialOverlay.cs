using UnityEngine;
using UnityEngine.UI;
using TMPro;

// First-run tutorial. Built entirely at runtime under the HUD canvas,
// so no prefab or scene wiring is required.
public class TutorialOverlay : MonoBehaviour
{
  private const string CompletedKey = "TutorialCompleted";

  private static readonly string[] Steps =
  {
    "Bacteria are invading!\n\nDrag fungi towers from the panel onto the field to block their path.",
    "Towers cost gold.\n\nDefeated bacteria and completed waves earn you more gold for reinforcements.",
    "Press START WAVE when your defenses are ready.\n\nDon't let the bacteria reach the end of the path!"
  };

  private TextMeshProUGUI stepText;
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
    var rect = (RectTransform)transform;
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.one;
    rect.offsetMin = Vector2.zero;
    rect.offsetMax = Vector2.zero;
    transform.SetAsLastSibling();

    Image background = gameObject.AddComponent<Image>();
    background.color = new Color(0f, 0f, 0f, 0.8f);

    Button button = gameObject.AddComponent<Button>();
    button.transition = Selectable.Transition.None;
    button.onClick.AddListener(NextStep);

    stepText = CreateText("StepText", 40f);
    var stepRect = (RectTransform)stepText.transform;
    stepRect.anchorMin = new Vector2(0.1f, 0.3f);
    stepRect.anchorMax = new Vector2(0.9f, 0.75f);
    stepRect.offsetMin = Vector2.zero;
    stepRect.offsetMax = Vector2.zero;

    TextMeshProUGUI hint = CreateText("TapHint", 26f);
    hint.text = "TAP TO CONTINUE";
    hint.color = new Color(1f, 1f, 1f, 0.6f);
    var hintRect = (RectTransform)hint.transform;
    hintRect.anchorMin = new Vector2(0.1f, 0.12f);
    hintRect.anchorMax = new Vector2(0.9f, 0.2f);
    hintRect.offsetMin = Vector2.zero;
    hintRect.offsetMax = Vector2.zero;

    ShowStep(0);
  }

  private TextMeshProUGUI CreateText(string name, float size)
  {
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(transform, false);
    var text = go.AddComponent<TextMeshProUGUI>();
    UiFont.Apply(text);
    text.fontSize = size;
    text.enableAutoSizing = true;
    text.fontSizeMin = 18f;
    text.fontSizeMax = size;
    text.alignment = TextAlignmentOptions.Center;
    text.color = Color.white;
    text.raycastTarget = false;
    return text;
  }

  private void ShowStep(int step)
  {
    currentStep = step;
    stepText.text = Steps[step];
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

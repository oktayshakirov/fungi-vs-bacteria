using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
  [SerializeField] private TextMeshProUGUI loadingText;
  [SerializeField] private Slider progressBar;
  [SerializeField] private TextMeshProUGUI progressText;

  private void Start()
  {
    Style();
  }

  // The slider shipped with Unity's default flat sprites; rounding the track
  // and fill and skinning the two labels brings it in line with the rest.
  private void Style()
  {
    if (loadingText != null)
    {
      UiSkin.Label(loadingText, UiSkin.Role.Heading);
      loadingText.alignment = TextAlignmentOptions.Center;
      loadingText.outlineWidth = 0.2f;
      loadingText.outlineColor = new Color32(10, 12, 20, 220);
    }

    if (progressText != null)
    {
      UiSkin.Label(progressText, UiSkin.Role.Value, UiSkin.TextPrimary);
      progressText.alignment = TextAlignmentOptions.Center;
    }

    if (progressBar == null) return;

    // The three pieces live under different containers in the prefab, so
    // centring them in place leaves them scattered. Gather them onto one parent
    // first, then lay them out as a block.
    Transform root = loadingText != null ? loadingText.transform.parent : progressBar.transform.parent;
    if (root != null)
    {
      progressBar.transform.SetParent(root, false);
      if (progressText != null) progressText.transform.SetParent(root, false);
    }

    Centre((RectTransform)progressBar.transform, new Vector2(0f, -40f), new Vector2(900f, 34f));
    if (loadingText != null) Centre(loadingText.rectTransform, new Vector2(0f, 40f), new Vector2(900f, 70f));
    if (progressText != null) Centre(progressText.rectTransform, new Vector2(0f, -104f), new Vector2(300f, 50f));

    // Starts empty; the prefab's authored value showed a full bar at 0%
    progressBar.minValue = 0f;
    progressBar.maxValue = 1f;
    progressBar.value = 0f;

    // The track is whichever child Image is neither the fill nor the handle
    int tracks = 0;
    foreach (Image image in progressBar.GetComponentsInChildren<Image>(true))
    {
      bool isFill = progressBar.fillRect != null && image.transform.IsChildOf(progressBar.fillRect);
      bool isHandle = progressBar.handleRect != null && image.transform.IsChildOf(progressBar.handleRect);
      if (isFill || isHandle) continue;

      // Neutral, not PanelDark: the loading backdrop is already dark, so a dark
      // track was invisible and only the fill nub showed. The prefab also ships
      // this object disabled, so it has to be switched on.
      image.gameObject.SetActive(true);
      image.enabled = true;
      UiSkin.Panel(image, UiSkin.Neutral, UiSkin.RadiusChip);
      UiSkin.Stretch(image.rectTransform);
      tracks++;
    }

    // This slider ships with only a Fill Area and a Handle, so at 0% there was
    // nothing on screen but a tiny green nub. Give it a track of its own.
    if (tracks == 0)
    {
      var trackGo = new GameObject("Track", typeof(RectTransform));
      trackGo.transform.SetParent(progressBar.transform, false);
      trackGo.transform.SetAsFirstSibling();
      var track = trackGo.AddComponent<Image>();
      UiSkin.Panel(track, UiSkin.Neutral, UiSkin.RadiusChip);
      track.raycastTarget = false;
      UiSkin.Stretch(track.rectTransform);
    }

    if (progressBar.fillRect != null)
    {
      var fill = progressBar.fillRect.GetComponent<Image>();
      if (fill != null) UiSkin.Panel(fill, UiSkin.Primary, UiSkin.RadiusChip);
    }

    // The default slider ships a draggable handle; a progress bar has no grab
    if (progressBar.handleRect != null) progressBar.handleRect.gameObject.SetActive(false);
    progressBar.interactable = false;
    progressBar.transition = Selectable.Transition.None;
  }

  private static void Centre(RectTransform rect, Vector2 position, Vector2 size)
  {
    if (rect == null) return;
    rect.anchorMin = new Vector2(0.5f, 0.5f);
    rect.anchorMax = new Vector2(0.5f, 0.5f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = position;
    rect.sizeDelta = size;
  }

  public void UpdateProgress(float progress)
  {
    progressBar.value = progress;
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.Loading);
    progressText.text = $"{(progress * 100):0}%";
  }
}

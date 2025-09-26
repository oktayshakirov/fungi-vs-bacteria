using UnityEngine;

[ExecuteAlways] // This allows the safe area to update in editor
public class SafeArea : MonoBehaviour
{
  private RectTransform rectTransform;
  private Rect lastSafeArea = Rect.zero;
  private Vector2 lastScreenSize = Vector2.zero;
  private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

  [SerializeField] private bool applyOnStart = true;
  [SerializeField] private bool continuousUpdate = true;

  void Awake()
  {
    rectTransform = GetComponent<RectTransform>();
    UpdateSafeArea();
  }

  void Start()
  {
    if (applyOnStart)
    {
      UpdateSafeArea();
    }
  }

  void Update()
  {
    if (continuousUpdate && Application.isPlaying)
    {
      bool orientationChanged = Screen.orientation != lastOrientation;
      bool screenSizeChanged = Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y;
      bool safeAreaChanged = Screen.safeArea != lastSafeArea;

      if (orientationChanged || screenSizeChanged || safeAreaChanged)
      {
        UpdateSafeArea();
      }
    }
  }

  void UpdateSafeArea()
  {
    if (rectTransform == null)
      return;

    Rect safeArea = Screen.safeArea;
    Vector2 anchorMin = safeArea.position;
    Vector2 anchorMax = safeArea.position + safeArea.size;

    anchorMin.x /= Screen.width;
    anchorMin.y /= Screen.height;
    anchorMax.x /= Screen.width;
    anchorMax.y /= Screen.height;

    rectTransform.anchorMin = anchorMin;
    rectTransform.anchorMax = anchorMax;

    // Store last values
    lastScreenSize = new Vector2(Screen.width, Screen.height);
    lastSafeArea = Screen.safeArea;
    lastOrientation = Screen.orientation;
  }

  // Public method to force update
  public void ForceUpdate()
  {
    UpdateSafeArea();
  }
}
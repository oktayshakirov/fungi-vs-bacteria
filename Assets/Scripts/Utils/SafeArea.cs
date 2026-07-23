using UnityEngine;

[ExecuteAlways] // This allows the safe area to update in editor
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
  private RectTransform rectTransform;
  private Rect lastSafeArea = Rect.zero;
  private Vector2 lastScreenSize = Vector2.zero;
  private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

  [SerializeField] private bool applyOnStart = true;
  [SerializeField] private bool continuousUpdate = true;

  [Tooltip("Extra inset in pixels kept on every edge, for rounded corners.")]
  [SerializeField] private float extraInset = 0f;

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
    if (!continuousUpdate) return;

    // Also run in the editor so the Device Simulator reflects changes live
    bool orientationChanged = Screen.orientation != lastOrientation;
    bool screenSizeChanged = Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y;
    bool safeAreaChanged = Screen.safeArea != lastSafeArea;

    if (orientationChanged || screenSizeChanged || safeAreaChanged)
    {
      UpdateSafeArea();
    }
  }

  void UpdateSafeArea()
  {
    if (rectTransform == null)
    {
      rectTransform = GetComponent<RectTransform>();
      if (rectTransform == null) return;
    }

    // Guard against the degenerate sizes reported during startup and layout reloads
    if (Screen.width <= 0 || Screen.height <= 0) return;

    Rect safeArea = Screen.safeArea;
    if (safeArea.width <= 0f || safeArea.height <= 0f) return;

    if (extraInset > 0f)
    {
      safeArea = new Rect(
        safeArea.x + extraInset,
        safeArea.y + extraInset,
        Mathf.Max(1f, safeArea.width - extraInset * 2f),
        Mathf.Max(1f, safeArea.height - extraInset * 2f));
    }

    Vector2 anchorMin = safeArea.position;
    Vector2 anchorMax = safeArea.position + safeArea.size;

    anchorMin.x /= Screen.width;
    anchorMin.y /= Screen.height;
    anchorMax.x /= Screen.width;
    anchorMax.y /= Screen.height;

    // Clamp: some devices report a safe area slightly outside the screen
    anchorMin = new Vector2(Mathf.Clamp01(anchorMin.x), Mathf.Clamp01(anchorMin.y));
    anchorMax = new Vector2(Mathf.Clamp01(anchorMax.x), Mathf.Clamp01(anchorMax.y));

    rectTransform.anchorMin = anchorMin;
    rectTransform.anchorMax = anchorMax;

    // Anchors alone do nothing if the rect carries offsets: without this the
    // panel keeps its old size and the safe area is silently ignored.
    rectTransform.offsetMin = Vector2.zero;
    rectTransform.offsetMax = Vector2.zero;
    rectTransform.pivot = new Vector2(0.5f, 0.5f);
    rectTransform.localScale = Vector3.one;
    rectTransform.anchoredPosition3D = Vector3.zero;

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

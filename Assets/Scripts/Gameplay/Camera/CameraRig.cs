using UnityEngine;

// Frames the whole board on any aspect ratio and drives the
// intro / play / end-of-level camera moves.
//
// The board is fitted by projecting its corners into camera space, so the
// framing is correct on every device instead of relying on a fixed height.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
  public static CameraRig Instance { get; private set; }

  private enum ViewState { Intro, Play, Outro }

  [System.Serializable]
  private struct Pose
  {
    public float pitch;
    public float yaw;
    public float zoom;   // 1 = fit the whole board, < 1 = closer
    public float height; // extra lift of the look-at pivot

    public static Pose Lerp(Pose a, Pose b, float t) => new Pose
    {
      pitch = Mathf.Lerp(a.pitch, b.pitch, t),
      yaw = Mathf.Lerp(a.yaw, b.yaw, t),
      zoom = Mathf.Lerp(a.zoom, b.zoom, t),
      height = Mathf.Lerp(a.height, b.height, t)
    };
  }

  [Header("Lens")]
  [Tooltip("A narrow FOV from further away gives the flat 'tabletop diorama' look.")]
  [SerializeField] private float fieldOfView = 35f;

  [Header("Framing")]
  [Tooltip("Fraction of the screen kept as a margin around the board.")]
  [SerializeField, Range(0f, 0.2f)] private float edgePadding = 0.02f;
  [Tooltip("Screen fraction reserved for the top HUD (stats bar).")]
  [SerializeField, Range(0f, 0.4f)] private float hudTopReserve = 0.12f;
  [Tooltip("Screen fraction reserved for the bottom HUD (towers panel).")]
  [SerializeField, Range(0f, 0.4f)] private float hudBottomReserve = 0.15f;
  [Tooltip("Fallback tower tray width in 720-height canvas units, before the HUD has laid out.")]
  [SerializeField] private float hudRightWidth = 252f;
  [Tooltip("Vertical headroom above the board so tall towers stay in frame.")]
  [SerializeField] private float towerHeadroom = 5f;

  [Header("Play view")]
  [SerializeField] private float playPitch = 42f;
  [SerializeField] private float playYaw = 0f;
  [Tooltip("Selectable camera angles (pitch, yaw, zoom) cycled by the view button.")]
  [SerializeField] private Vector3[] viewPresets =
  {
    new Vector3(42f, 0f, 1f),    // Clear default combat view
    new Vector3(50f, 0f, 1f),    // Tactical overview
    new Vector3(34f, 0f, 1f),    // Character view, same board orientation
  };
  private int viewIndex = 0;
  [Tooltip("Tilt further on wide phones and flatter on tablets, so the board " +
           "fills the usable screen area on every device.")]
  [SerializeField] private bool adaptPitchToAspect = false;
  [SerializeField] private float minPitch = 34f;
  [SerializeField] private float maxPitch = 58f;

  [Header("Intro fly-in")]
  [SerializeField] private bool playIntroOnStart = true;
  [SerializeField] private float introDuration = 3.8f;
  // Low, close, swung around to one side — orbits up and out to the play view
  [SerializeField] private Pose introPose = new Pose { pitch = 11f, yaw = -62f, zoom = 0.48f, height = 1.5f };

  [Header("End of level")]
  [SerializeField] private float outroDuration = 2.5f;
  [SerializeField] private Pose outroPose = new Pose { pitch = 34f, yaw = 16f, zoom = 0.72f, height = 1.5f };

  private Camera cam;
  private ViewState state = ViewState.Play;
  private Pose fromPose;
  private float transitionTime;
  private float transitionDuration;

  private Pose PlayPose
  {
    get
    {
      Vector3 v = CurrentPreset();
      float pitch = adaptPitchToAspect ? ResolvedPlayPitch() : v.x;
      return new Pose { pitch = pitch, yaw = v.y, zoom = v.z, height = 0f };
    }
  }

  private Vector3 CurrentPreset()
  {
    if (viewPresets == null || viewPresets.Length == 0) return new Vector3(playPitch, playYaw, 1f);
    return viewPresets[Mathf.Clamp(viewIndex, 0, viewPresets.Length - 1)];
  }

  public int ViewCount => (viewPresets == null || viewPresets.Length == 0) ? 1 : viewPresets.Length;

  // Cycles to the next camera angle with a smooth transition. Returns the new index.
  public int CycleView()
  {
    if (viewPresets == null || viewPresets.Length <= 1) return viewIndex;
    Pose from = state == ViewState.Outro ? outroPose : PlayPose;
    viewIndex = (viewIndex + 1) % viewPresets.Length;
    BeginTransition(ViewState.Play, from, PlayPose, 0.9f);
    return viewIndex;
  }

  // A tilted board covers boardWidth x (boardDepth * sin(pitch)) on screen.
  // Solving that against the usable screen area gives the pitch where the board
  // fills both axes at once, which is the largest it can be drawn.
  private float ResolvedPlayPitch()
  {
    if (!adaptPitchToAspect) return playPitch;

    Bounds board = GetBoardBounds();
    if (board.size.z < 0.01f) return playPitch;

    Rect viewport = GameplayViewport;
    float usableV = viewport.height;
    float usableH = viewport.width;
    float sin = board.size.x * usableV / (board.size.z * GetAspect() * usableH);

    return Mathf.Clamp(Mathf.Asin(Mathf.Clamp01(sin)) * Mathf.Rad2Deg, minPitch, maxPitch);
  }

  private void OnEnable()
  {
    Instance = this;
    cam = GetComponent<Camera>();
    ApplyPose(PlayPose);
  }

  private void OnDisable()
  {
    if (Instance == this) Instance = null;
  }

  private void Start()
  {
    if (!Application.isPlaying) return;

    if (playIntroOnStart)
    {
      BeginTransition(ViewState.Intro, introPose, PlayPose, introDuration);
    }
    else
    {
      ApplyPose(PlayPose);
    }
  }

  private void LateUpdate()
  {
    if (!Application.isPlaying)
    {
      // Keep the scene and game views framed correctly while editing
      ApplyPose(PlayPose);
      return;
    }

    if (transitionDuration <= 0f)
    {
      ApplyPose(state == ViewState.Outro ? outroPose : PlayPose);
      ApplyShake();
      return;
    }

    // Unscaled: the game is paused (timeScale 0) on victory and defeat
    transitionTime += Time.unscaledDeltaTime;
    float t = Mathf.Clamp01(transitionTime / transitionDuration);
    // Smootherstep: gentle acceleration and a soft settle at the end
    float eased = t * t * t * (t * (6f * t - 15f) + 10f);

    Pose target = state == ViewState.Outro ? outroPose : PlayPose;
    ApplyPose(Pose.Lerp(fromPose, target, eased));
    ApplyShake();

    if (t >= 1f)
    {
      transitionDuration = 0f;
      if (state == ViewState.Intro) state = ViewState.Play;
    }
  }

  private float shakeTime;
  private float shakeDuration;
  private float shakeMagnitude;

  // amount is a 0..1 intensity; scaled to world units for this camera distance
  public void Shake(float amount)
  {
    if(shakeDuration>0 && shakeTime<.12f) return;
    shakeMagnitude = Mathf.Clamp01(amount) * 1.2f;
    shakeDuration = 0.25f;
    shakeTime = 0f;
  }

  private void ApplyShake()
  {
    if (shakeDuration <= 0f) return;

    shakeTime += Time.unscaledDeltaTime;
    float remaining = 1f - Mathf.Clamp01(shakeTime / shakeDuration);
    if (remaining <= 0f)
    {
      shakeDuration = 0f;
      return;
    }

    Vector3 offset = Random.insideUnitSphere * shakeMagnitude * remaining;
    offset.z *= 0.3f; // less wobble along the view direction
    transform.position += offset;
  }

  public void PlayEndOfLevelView()
  {
    if (!Application.isPlaying || state == ViewState.Outro) return;
    BeginTransition(ViewState.Outro, CurrentPose(), outroPose, outroDuration);
  }

  private void BeginTransition(ViewState next, Pose from, Pose to, float duration)
  {
    state = next;
    fromPose = from;
    transitionTime = 0f;
    transitionDuration = Mathf.Max(0.01f, duration);
    ApplyPose(from);
  }

  private Pose CurrentPose()
  {
    // Good enough for blending out of the play view
    return state == ViewState.Outro ? outroPose : PlayPose;
  }

  private void ApplyPose(Pose pose)
  {
    if (cam == null) cam = GetComponent<Camera>();
    if (cam == null) return;

    cam.fieldOfView = fieldOfView;

    Bounds board = GetBoardBounds();
    Quaternion rotation = Quaternion.Euler(pose.pitch, pose.yaw, 0f);

    float aspect = GetAspect();
    float tanV = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
    float tanH = tanV * aspect;

    Rect viewport = GameplayViewport;
    Vector3 pivot = board.center + Vector3.up * pose.height;
    float distance = RequiredDistance(board, pivot, rotation, tanH, tanV, viewport) * pose.zoom;

    Vector3 forward = rotation * Vector3.forward;
    Vector3 up = rotation * Vector3.up;

    Vector3 right = rotation * Vector3.right;
    Vector2 centre = viewport.center * 2f - Vector2.one;
    // Aim into the usable rectangle, including the tray's horizontal exclusion.
    // The fit below includes this offset in its perspective inequalities;
    // shifting after a symmetric fit clips the nearer corners.
    transform.SetPositionAndRotation(pivot - forward * distance
      - right * (distance * tanH * centre.x)
      - up * (distance * tanV * centre.y), rotation);

    cam.nearClipPlane = 0.3f;
    float far = distance * 4f;
    if (scenery == null) scenery = FindFirstObjectByType<LevelDecorator>();
    if (scenery != null && scenery.HasVisualBounds)
    {
      Bounds bounds = scenery.VisualBounds;
      // A bounding sphere covers the entire scenery at every camera angle.
      // The old distance multiplier clipped distant islands during the fly-in.
      far = Mathf.Max(far, Vector3.Distance(transform.position, bounds.center) + bounds.extents.magnitude + 10f);
    }
    cam.farClipPlane = far;
  }

  // Smallest distance along the view direction that keeps every board corner
  // inside the frustum, solved directly from the frustum inequalities.
  private float RequiredDistance(Bounds board, Vector3 pivot, Quaternion rotation, float tanH, float tanV, Rect viewport)
  {
    Vector3 forward = rotation * Vector3.forward;
    Vector3 right = rotation * Vector3.right;
    Vector3 up = rotation * Vector3.up;

    Vector3 e = board.extents;
    Vector2 min = viewport.min * 2f - Vector2.one;
    Vector2 max = viewport.max * 2f - Vector2.one;
    Vector2 centre = (min + max) * 0.5f;
    float distance = 1f;

    for (int i = 0; i < 8; i++)
    {
      Vector3 corner = board.center + new Vector3(
        (i & 1) == 0 ? -e.x : e.x,
        (i & 2) == 0 ? -e.y : e.y,
        (i & 4) == 0 ? -e.z : e.z);

      Vector3 v = corner - pivot;
      float alongForward = Vector3.Dot(v, forward);

      float x = Vector3.Dot(v, right) / tanH;
      float y = Vector3.Dot(v, up) / tanV;
      distance = Mathf.Max(distance, (x - max.x * alongForward) / (max.x - centre.x));
      distance = Mathf.Max(distance, (min.x * alongForward - x) / (centre.x - min.x));
      distance = Mathf.Max(distance, (y - max.y * alongForward) / (max.y - centre.y));
      distance = Mathf.Max(distance, (min.y * alongForward - y) / (centre.y - min.y));
    }

    return distance;
  }

  private readonly Vector3[] hudCorners = new Vector3[4];

  // Coordinates are relative to the full camera image, not the SafeArea child.
  // Read the actual tray after layout so tablets and notched phones agree with
  // the UI. The fallback also makes standalone board previews representative.
  public Rect GameplayViewport
  {
    get
    {
#if UNITY_EDITOR
      if(suppressHudForPreview && !Application.isPlaying) return Rect.MinMaxRect(edgePadding,edgePadding,1f-edgePadding,1f-edgePadding);
#endif
      float left = edgePadding;
      float right = 1f - edgePadding - hudRightWidth / (720f * GetAspect());
      float bottom = hudBottomReserve + edgePadding;
      float top = 1f - hudTopReserve - edgePadding;
      RectTransform frame = HudTheme.RightRailRect;
      if (frame != null)
      {
        Canvas canvas = frame.GetComponentInParent<Canvas>();
        RectTransform root = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (root != null && root.rect.width > 0f)
        {
          if (frame.gameObject.activeInHierarchy)
          {
            frame.GetWorldCorners(hudCorners);
            float localLeft = root.InverseTransformPoint(hudCorners[0]).x;
            right = (localLeft - root.rect.xMin - 8f) / root.rect.width - edgePadding;
          }
          else right = 1f - edgePadding;

          RectTransform safe = HudTheme.GameplaySafeRect;
          if (safe != null)
          {
            safe.GetWorldCorners(hudCorners);
            left = Mathf.Max(left, (root.InverseTransformPoint(hudCorners[0]).x - root.rect.xMin) / root.rect.width + edgePadding);
            right = Mathf.Min(right, (root.InverseTransformPoint(hudCorners[2]).x - root.rect.xMin) / root.rect.width - edgePadding);
            bottom = Mathf.Max(bottom, (root.InverseTransformPoint(hudCorners[0]).y - root.rect.yMin) / root.rect.height + edgePadding);
            top = Mathf.Min(top, (root.InverseTransformPoint(hudCorners[2]).y - root.rect.yMin) / root.rect.height - edgePadding);
          }
        }
      }
      right = Mathf.Clamp(right, left + 0.2f, 1f - edgePadding);
      top = Mathf.Max(bottom + 0.2f, top);
      return Rect.MinMaxRect(left, bottom, right, top);
    }
  }

  private float GetAspect()
  {
    if (aspectOverride > 0.01f) return aspectOverride;
    if (cam != null && cam.aspect > 0.01f) return cam.aspect;
    return Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
  }

  private float aspectOverride;
  private LevelDecorator scenery;

#if UNITY_EDITOR
  // Used by the framing preview tool: 0 = play, 1 = intro, 2 = outro
  private bool suppressHudForPreview;
  public void EditorPreview(int poseIndex, float aspect, bool showHud=true)
  {
    aspectOverride = aspect;
    suppressHudForPreview=!showHud;
    ApplyPose(poseIndex == 1 ? introPose : poseIndex == 2 ? outroPose : PlayPose);
  }

  public float TopReserve => hudTopReserve;
  public float BottomReserve => hudBottomReserve;
#endif

  private Bounds GetBoardBounds()
  {
    GridManager grid = GridManager.Instance;
#if UNITY_EDITOR
    if (grid == null) grid = FindFirstObjectByType<GridManager>();
#endif

    if (grid != null)
    {
      // Frame close to the play grid (plus a little room for the base/portal at
      // the ends). Decorative props extend past this and spill off-frame, which
      // keeps the game area large and close instead of framing the whole island.
      // Tight: the decorative ring is scenery, not board. Framing it cost most
      // of the screen and shrank everything the player actually interacts with.
      const float framingMargin = 2f; // world units added on each side
      Vector3 size = new Vector3(grid.gridSize.x * grid.cellSize, 0f, grid.gridSize.y * grid.cellSize);
      Vector3 center = grid.originPosition + size * 0.5f;
      size.x += framingMargin * 2f;
      size.z += framingMargin * 2f;
      size.y = towerHeadroom;
      center.y = towerHeadroom * 0.5f;
      return new Bounds(center, size);
    }

    return new Bounds(new Vector3(0f, towerHeadroom * 0.5f, 0f), new Vector3(50f, towerHeadroom, 50f));
  }
}

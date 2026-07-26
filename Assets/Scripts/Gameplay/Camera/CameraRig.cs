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
  [SerializeField] private float fieldOfView = 36f;

  [Header("Framing")]
  [Tooltip("Fraction of the screen kept as a margin around the board.")]
  [SerializeField, Range(0f, 0.2f)] private float edgePadding = 0.02f;
  [Tooltip("Screen fraction reserved for the top HUD (stats bar).")]
  [SerializeField, Range(0f, 0.4f)] private float hudTopReserve = 0.09f;
  [Tooltip("Screen fraction reserved for the bottom HUD (towers panel).")]
  [SerializeField, Range(0f, 0.4f)] private float hudBottomReserve = 0.15f;
  [Tooltip("Vertical headroom above the board so tall towers stay in frame.")]
  [SerializeField] private float towerHeadroom = 5f;

  [Header("Play view")]
  [SerializeField] private float playPitch = 46f;
  [SerializeField] private float playYaw = 0f;
  [Tooltip("Tilt further on wide phones and flatter on tablets, so the board " +
           "fills the usable screen area on every device.")]
  [SerializeField] private bool adaptPitchToAspect = true;
  [SerializeField] private float minPitch = 34f;
  [SerializeField] private float maxPitch = 58f;

  [Header("Intro fly-in")]
  [SerializeField] private bool playIntroOnStart = true;
  [SerializeField] private float introDuration = 2.75f;
  [SerializeField] private Pose introPose = new Pose { pitch = 16f, yaw = -32f, zoom = 0.62f, height = 2f };

  [Header("End of level")]
  [SerializeField] private float outroDuration = 2.5f;
  [SerializeField] private Pose outroPose = new Pose { pitch = 34f, yaw = 16f, zoom = 0.72f, height = 1.5f };

  private Camera cam;
  private ViewState state = ViewState.Play;
  private Pose fromPose;
  private float transitionTime;
  private float transitionDuration;

  private Pose PlayPose => new Pose { pitch = ResolvedPlayPitch(), yaw = playYaw, zoom = 1f, height = 0f };

  // A tilted board covers boardWidth x (boardDepth * sin(pitch)) on screen.
  // Solving that against the usable screen area gives the pitch where the board
  // fills both axes at once, which is the largest it can be drawn.
  private float ResolvedPlayPitch()
  {
    if (!adaptPitchToAspect) return playPitch;

    Bounds board = GetBoardBounds();
    if (board.size.z < 0.01f) return playPitch;

    float usableV = Mathf.Max(0.2f, 1f - hudTopReserve - hudBottomReserve - edgePadding * 2f);
    float usableH = Mathf.Max(0.2f, 1f - edgePadding * 2f);
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
    float eased = t * t * (3f - 2f * t);

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
    shakeMagnitude = amount * 4f;
    shakeDuration = 0.35f;
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

    // Reserve space for the HUD bands and a uniform margin
    float usableV = Mathf.Max(0.2f, 1f - hudTopReserve - hudBottomReserve - edgePadding * 2f);
    float usableH = Mathf.Max(0.2f, 1f - edgePadding * 2f);
    float tanVFit = tanV * usableV;
    float tanHFit = tanH * usableH;

    Vector3 pivot = board.center + Vector3.up * pose.height;
    float distance = RequiredDistance(board, pivot, rotation, tanHFit, tanVFit) * pose.zoom;

    Vector3 forward = rotation * Vector3.forward;
    Vector3 up = rotation * Vector3.up;

    // Slide the image so the board sits inside the usable band rather than
    // behind the HUD: positive when the bottom reserve is larger than the top.
    float bandOffset = (hudBottomReserve - hudTopReserve) * 0.5f;
    float worldOffset = 2f * distance * tanV * bandOffset;

    transform.SetPositionAndRotation(pivot - forward * distance - up * worldOffset, rotation);

    cam.nearClipPlane = Mathf.Max(0.3f, distance * 0.05f);
    cam.farClipPlane = distance * 4f;
  }

  // Smallest distance along the view direction that keeps every board corner
  // inside the frustum, solved directly from the frustum inequalities.
  private float RequiredDistance(Bounds board, Vector3 pivot, Quaternion rotation, float tanH, float tanV)
  {
    Vector3 forward = rotation * Vector3.forward;
    Vector3 right = rotation * Vector3.right;
    Vector3 up = rotation * Vector3.up;

    Vector3 e = board.extents;
    float distance = 1f;

    for (int i = 0; i < 8; i++)
    {
      Vector3 corner = board.center + new Vector3(
        (i & 1) == 0 ? -e.x : e.x,
        (i & 2) == 0 ? -e.y : e.y,
        (i & 4) == 0 ? -e.z : e.z);

      Vector3 v = corner - pivot;
      float alongForward = Vector3.Dot(v, forward);

      distance = Mathf.Max(distance, Mathf.Abs(Vector3.Dot(v, right)) / tanH - alongForward);
      distance = Mathf.Max(distance, Mathf.Abs(Vector3.Dot(v, up)) / tanV - alongForward);
    }

    return distance;
  }

  private float GetAspect()
  {
    if (aspectOverride > 0.01f) return aspectOverride;
    if (cam != null && cam.aspect > 0.01f) return cam.aspect;
    return Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
  }

  private float aspectOverride;

#if UNITY_EDITOR
  // Used by the framing preview tool: 0 = play, 1 = intro, 2 = outro
  public void EditorPreview(int poseIndex, float aspect)
  {
    aspectOverride = aspect;
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
      Vector3 size = new Vector3(grid.gridSize.x * grid.cellSize, 0f, grid.gridSize.y * grid.cellSize);
      Vector3 center = grid.originPosition + size * 0.5f;
      size.y = towerHeadroom;
      center.y = towerHeadroom * 0.5f;
      return new Bounds(center, size);
    }

    return new Bounds(new Vector3(0f, towerHeadroom * 0.5f, 0f), new Vector3(50f, towerHeadroom, 50f));
  }
}

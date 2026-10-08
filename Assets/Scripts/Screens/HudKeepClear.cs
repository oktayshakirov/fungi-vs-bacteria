using UnityEngine;

// Keeps a centred HUD element from running into something anchored beside it.
//
// The wave badge sits in the middle of the top edge, and the stat chips sit at
// the left of it. On a 4:3 canvas - only 960 units wide - the two meet, and
// they meet by a DIFFERENT amount as the level runs: the gold chip grows with
// the number in it, so 515 and 15,000 are not the same width.
//
// That is why this is a live component and not a one-off nudge at load. It
// measures both rects through their world corners, which is the only reading
// that is true after the layout groups have run, and it costs two corner
// fetches a frame on one object.
public class HudKeepClear : MonoBehaviour
{
  private RectTransform self;
  private RectTransform follower;   // the label riding on the plate
  private RectTransform blocker;
  private float gap;
  private float homeX;
  private float followerHomeX;

  public static void Attach(RectTransform self, RectTransform follower,
    RectTransform blocker, float gap)
  {
    if (self == null || blocker == null) return;

    var keep = self.gameObject.AddComponent<HudKeepClear>();
    keep.self = self;
    keep.follower = follower;
    keep.blocker = blocker;
    keep.gap = gap;
    keep.homeX = self.anchoredPosition.x;
    keep.followerHomeX = follower != null ? follower.anchoredPosition.x : 0f;
  }

  private static readonly Vector3[] corners = new Vector3[4];

  // The x of one of `rect`'s corners, expressed in `space`'s local units so it
  // can be compared with an anchoredPosition.
  private static float EdgeIn(RectTransform rect, RectTransform space, int corner)
  {
    rect.GetWorldCorners(corners);
    return space.InverseTransformPoint(corners[corner]).x;
  }

  // The stats panel's own rect stops well short of the chips inside it: its
  // layout group is free to overflow, and the gold chip does. Measuring the
  // panel alone put its right edge ~95 units left of where the player can see
  // it, so this walks the children as well.
  private static float RightmostEdge(RectTransform rect, RectTransform space)
  {
    float right = EdgeIn(rect, space, 2);
    for (int i = 0; i < rect.childCount; i++)
    {
      if (rect.GetChild(i) is RectTransform child && child.gameObject.activeInHierarchy)
      {
        right = Mathf.Max(right, EdgeIn(child, space, 2));
      }
    }
    return right;
  }

  private void LateUpdate() => Apply();

  // Public so the batch-mode preview can drive it: LateUpdate never runs
  // there, and without this the shots would show the uncorrected position.
  public void Apply()
  {
    if (self == null || blocker == null) return;

    var space = self.parent as RectTransform;
    if (space == null) return;

    // Measure from the original centered position, not last frame's nudge.
    // Otherwise an already-cleared overlap yields zero push on the next
    // frame and makes the badge alternate between its two positions.
    float homeLeft = EdgeIn(self, space, 0) - (self.anchoredPosition.x - homeX);
    float blockerRight = RightmostEdge(blocker, space);

    // Only ever pushed RIGHT of centre, and only by as much as it takes. When
    // the chips shrink again the badge walks back to the middle on its own,
    // because homeX is what the offset is measured from.
    float push = Mathf.Max(0f, (blockerRight + gap) - homeLeft);
    float targetX = homeX + push;

    if (!Mathf.Approximately(self.anchoredPosition.x, targetX))
    {
      self.anchoredPosition = new Vector2(targetX, self.anchoredPosition.y);
      if (follower != null)
      {
        follower.anchoredPosition =
          new Vector2(followerHomeX + push, follower.anchoredPosition.y);
      }
    }
  }
}

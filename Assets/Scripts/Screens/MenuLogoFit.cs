using UnityEngine;

// Keeps the main menu's vs-battle art fitted to the band between the title and
// the PLAY plate.
//
// MenuLayout.ApplyLogo does the fit once, but one measurement is not enough:
// SafeArea resolves over the first frames and again on rotation, and the
// Device Simulator changes the canvas under the editor without a domain
// reload. This re-runs the fit whenever the band's size actually changes -
// never every frame, so it costs nothing on a stable screen.
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class MenuLogoFit : MonoBehaviour
{
  private Vector2 lastParentSize = Vector2.zero;

  private void OnEnable()
  {
    lastParentSize = Vector2.zero;   // force a fit on the next tick
  }

  private void LateUpdate()
  {
    var rect = (RectTransform)transform;
    if (rect.parent is not RectTransform parent) return;

    Vector2 size = parent.rect.size;
    if (size == lastParentSize) return;

    lastParentSize = size;
    MenuLayout.FitLogo(rect);
  }
}

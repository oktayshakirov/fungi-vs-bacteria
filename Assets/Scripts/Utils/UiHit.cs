using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Whether a screen point is over the UI, answered by raycasting the UI
// directly instead of asking the EventSystem what it last saw.
//
// EventSystem.IsPointerOverGameObject(fingerId) reads the input module's stored
// pointer state, which on a touch screen is only as fresh as the module's own
// Update. On the frame a finger lands the module may not have run yet (or, on
// release, may already have dropped the touch), so the answer depends on script
// order. That is how a tap on SELL, UPGRADE or FIRST/STRONG/NEAREST in the tower
// panel was sometimes ALSO read as a tap on the board behind it: the board tap
// deselected the tower and closed the panel before the button's click landed.
public static class UiHit
{
  private static readonly List<RaycastResult> results = new List<RaycastResult>();
  private static PointerEventData data;
  private static EventSystem dataOwner;

  public static bool Over(Vector2 screenPosition)
  {
    EventSystem system = EventSystem.current;
    if (system == null) return false;

    if (data == null || dataOwner != system)
    {
      data = new PointerEventData(system);
      dataOwner = system;
    }

    data.position = screenPosition;
    results.Clear();
    system.RaycastAll(data, results);
    return results.Count > 0;
  }

  // The first touch on a device, the mouse otherwise.
  public static Vector2 PointerPosition =>
    Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;

  public static bool OverPointer() => Over(PointerPosition);
}

using UnityEngine;

// The nest heaves when something climbs out of it: a quick stretch up and in,
// then a settle. The counterpart to BaseFlinch at the other end of the path -
// that one squashes when the base is hit, this one swells when the nest gives
// something up - so both landmarks answer what happens to them instead of
// sitting still through it.
//
// Driven straight from EnemySpawner rather than from an event, because unlike
// a base hit there is exactly one caller and it already knows the moment.
public class NestPulse : MonoBehaviour
{
  // A touch shorter than SpawnEffect's 0.62s, so the nest has finished moving
  // while the last of the mist is still thinning. The other way round, the
  // heave is the last thing on screen and reads as the nest twitching after
  // the fact.
  private const float Duration = 0.5f;

  private static NestPulse instance;

  private Vector3 restScale;
  private float startedAt = -1f;

  // The nest is built once per level and never moves, so one static is enough.
  // Cleared in OnDisable as well as OnDestroy: a scene load leaves the static
  // pointing at a destroyed object otherwise, and `instance != null` would
  // still be true for a frame against Unity's fake-null.
  public static void Pulse()
  {
    if (instance != null) instance.startedAt = Time.time;
  }

  private void Awake()
  {
    restScale = transform.localScale;
    instance = this;
  }

  private void OnDisable()
  {
    if (instance == this) instance = null;
  }

  private void Update()
  {
    if (startedAt < 0f) return;

    // SCALED time, unlike BaseFlinch's unscaled. A base hit has to play out
    // even when the hit ends the run and the game pauses on the game-over
    // screen; this one is tied to a spawn, and at 2x and 3x the spawns come
    // faster, so the heave has to keep up or they overlap into one long swell.
    float t = (Time.time - startedAt) / Duration;
    if (t >= 1f)
    {
      transform.localScale = restScale;
      startedAt = -1f;
      return;
    }

    transform.localScale = PulseScale(restScale, t);
  }

  // Up and in, then back: the opposite of the base's squash, because this is
  // something pushing out from underneath rather than something landing on
  // top. Damped, so it overshoots once and settles.
  //
  // Public and static for the same reason Enemy.EmergeScaleAt is: it is the
  // shape of the motion, and CameraPreview poses it to photograph a spawn
  // rather than guessing at what a batch render cannot show. `t` runs 0 to 1.
  public static Vector3 PulseScale(Vector3 rest, float t)
  {
    float wave = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 2.2f) * (1f - Mathf.Clamp01(t));
    return new Vector3(
      rest.x * (1f - 0.07f * wave),
      rest.y * (1f + 0.22f * wave),
      rest.z * (1f - 0.07f * wave));
  }
}

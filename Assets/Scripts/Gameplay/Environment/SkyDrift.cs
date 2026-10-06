using System.Collections.Generic;
using UnityEngine;

// Slow ambient motion in the sky around the play island: the cloud ring drifts
// round it on the wind and the small far-off islands and rock chunks bob.
//
// Cheap on purpose. One component and one Update for the lot, a few dozen
// transform writes a frame and no allocation. These pieces are the only
// scenery kept OUT of LevelDecorator's static batch (a batched object cannot
// move); with the SRP batcher on, the extra draw calls are the cheap kind.
//
// Unscaled time: the 2x/3x speed control should not make the weather race,
// and the sky carrying on behind the pause menu is the point of it.
public class SkyDrift : MonoBehaviour
{
  // Classes, not structs: a struct read out of a List is a copy, and writing
  // the angle back to the copy is how a drift silently never moves.
  private class Cloud
  {
    public Transform transform;
    public float angle;
    public float distance;
    public float height;
    public float speed;
  }

  private class Floater
  {
    public Transform transform;
    public Vector3 home;
    public float amplitude;
    public float frequency;
    public float phase;
  }

  // World units per second along the ring, before each cloud's own variation.
  private const float WindSpeed = 0.7f;

  private readonly List<Cloud> clouds = new List<Cloud>();
  private readonly List<Floater> floaters = new List<Floater>();
  private float halfW = 1f;
  private float halfD = 1f;

  public void SetExtent(float islandHalfW, float islandHalfD)
  {
    halfW = Mathf.Max(1f, islandHalfW);
    halfD = Mathf.Max(1f, islandHalfD);
  }

  // `angle` and `distance` are the cloud's place on the elliptical ring, in
  // the same terms LevelDecorator placed it with, so it orbits on that ellipse
  // and never swings in over the island.
  public void AddCloud(Transform cloud, float angle, float distance, System.Random rng)
  {
    float radius = (halfW + halfD) * 0.5f * distance;
    float variation = 0.7f + (float)rng.NextDouble() * 0.6f;
    clouds.Add(new Cloud
    {
      transform = cloud,
      angle = angle,
      distance = distance,
      height = cloud.position.y,
      speed = WindSpeed * variation / Mathf.Max(1f, radius),
    });
  }

  public void AddFloater(Transform floater, float amplitude, System.Random rng)
  {
    floaters.Add(new Floater
    {
      transform = floater,
      home = floater.position,
      amplitude = amplitude,
      frequency = 0.35f + (float)rng.NextDouble() * 0.3f,
      phase = (float)rng.NextDouble() * Mathf.PI * 2f,
    });
  }

  private void Update()
  {
    float dt = Time.unscaledDeltaTime;
    float time = Time.unscaledTime;

    for (int i = 0; i < clouds.Count; i++)
    {
      Cloud cloud = clouds[i];
      if (cloud.transform == null) continue;
      cloud.angle += cloud.speed * dt;
      cloud.transform.position = new Vector3(
        Mathf.Cos(cloud.angle) * halfW * cloud.distance,
        cloud.height,
        Mathf.Sin(cloud.angle) * halfD * cloud.distance);
    }

    for (int i = 0; i < floaters.Count; i++)
    {
      Floater floater = floaters[i];
      if (floater.transform == null) continue;
      float lift = Mathf.Sin(time * floater.frequency + floater.phase) * floater.amplitude;
      floater.transform.position = floater.home + new Vector3(0f, lift, 0f);
    }
  }
}

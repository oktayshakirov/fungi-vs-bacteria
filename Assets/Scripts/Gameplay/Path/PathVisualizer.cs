using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[DefaultExecutionOrder(-1)] // This makes it run before other scripts
public class PathVisualizer : MonoBehaviour
{
  [SerializeField] private Material pathMaterial;
  [SerializeField] private float heightOffset = 0.01f;

  private LineRenderer lineRenderer;
  private float currentPathWidth = 1f;

  private void Awake()
  {
    InitializeLineRenderer();
  }

  private void InitializeLineRenderer()
  {
    // Force create LineRenderer if it doesn't exist
    lineRenderer = gameObject.GetComponent<LineRenderer>();
    if (lineRenderer == null)
    {
      Debug.Log("Adding LineRenderer component...");
      lineRenderer = gameObject.AddComponent<LineRenderer>();
    }
    SetupLineRenderer();
  }

  private void SetupLineRenderer()
  {
    if (lineRenderer == null)
    {
      Debug.LogError("LineRenderer is still null after initialization!");
      return;
    }

    // Basic setup
    if (pathMaterial == null)
    {
      Debug.Log("Creating default path material...");
      pathMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
      pathMaterial.color = Color.yellow;
    }

    lineRenderer.material = pathMaterial;
    lineRenderer.useWorldSpace = true;

    // Width settings
    lineRenderer.startWidth = currentPathWidth;
    lineRenderer.endWidth = currentPathWidth;

    // Quality settings
    lineRenderer.numCornerVertices = 10;
    lineRenderer.numCapVertices = 5;

    // Make it lay flat on the ground
    lineRenderer.alignment = LineAlignment.TransformZ;  // This makes it lay flat
    transform.rotation = Quaternion.Euler(90, 0, 0);   // Rotate to face upward

    lineRenderer.positionCount = 0;
    lineRenderer.generateLightingData = true;

    // Make sure it's visible
    lineRenderer.sortingOrder = 1;
    lineRenderer.allowOcclusionWhenDynamic = false;
  }

  public void UpdatePath(Vector3[] points, float pathWidth)
  {
    currentPathWidth = pathWidth;
    SetupLineRenderer(); // Update line renderer with new width

    if (points == null || points.Length < 2)
    {
      Debug.LogWarning("Not enough points to create path!");
      return;
    }

    Debug.Log($"Updating path with {points.Length} points");

    if (lineRenderer == null)
    {
      Debug.LogError("LineRenderer is null! Trying to get component...");
      lineRenderer = GetComponent<LineRenderer>();
      if (lineRenderer == null)
      {
        Debug.LogError("Failed to get LineRenderer component!");
        return;
      }
      SetupLineRenderer();
    }

    // Generate smooth path
    Vector3[] smoothedPoints = GenerateSmoothPath(points);
    if (smoothedPoints == null)
    {
      Debug.LogError("Failed to generate smoothed points!");
      return;
    }

    Debug.Log($"Generated {smoothedPoints.Length} smoothed points");

    // Update line renderer
    try
    {
      lineRenderer.positionCount = smoothedPoints.Length;

      // Use GroundManager to get ground height for each point
      for (int i = 0; i < smoothedPoints.Length; i++)
      {
        float groundHeight = GroundManager.Instance.GetGroundHeight(smoothedPoints[i]);
        smoothedPoints[i] = new Vector3(
          smoothedPoints[i].x,
          groundHeight + heightOffset,
          smoothedPoints[i].z
        );
      }

      lineRenderer.SetPositions(smoothedPoints);
      Debug.Log("Successfully set positions in LineRenderer");
    }
    catch (System.Exception e)
    {
      Debug.LogError($"Error setting positions: {e.Message}");
    }
  }

  private Vector3[] GenerateSmoothPath(Vector3[] points)
  {
    if (points == null || points.Length < 2)
    {
      Debug.LogError("Invalid points array in GenerateSmoothPath");
      return null;
    }

    try
    {
      int pointsPerSegment = 10; // Increased for smoother curves
      int totalPoints = (points.Length - 1) * pointsPerSegment + 1;
      Vector3[] smoothedPoints = new Vector3[totalPoints];

      // Add first point
      smoothedPoints[0] = points[0];
      int currentIndex = 1;

      // Generate smooth points using Catmull-Rom spline
      for (int i = 0; i < points.Length - 1; i++)
      {
        Vector3 p0 = i > 0 ? points[i - 1] : points[i];
        Vector3 p1 = points[i];
        Vector3 p2 = points[i + 1];
        Vector3 p3 = i < points.Length - 2 ? points[i + 2] : p2;

        // Generate points along the curve
        for (int j = 0; j < pointsPerSegment && currentIndex < totalPoints; j++)
        {
          float t = j / (float)pointsPerSegment;
          smoothedPoints[currentIndex] = CatmullRomPoint(p0, p1, p2, p3, t);
          currentIndex++;
        }
      }

      return smoothedPoints;
    }
    catch (System.Exception e)
    {
      Debug.LogError($"Error in GenerateSmoothPath: {e.Message}");
      return null;
    }
  }

  private Vector3 CatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
  {
    float t2 = t * t;
    float t3 = t2 * t;

    return 0.5f * (
        (-t3 + 2f * t2 - t) * p0 +
        (3f * t3 - 5f * t2 + 2f) * p1 +
        (-3f * t3 + 4f * t2 + t) * p2 +
        (t3 - t2) * p3
    );
  }
}
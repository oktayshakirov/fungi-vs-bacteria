using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class PathCreatorWindow : EditorWindow
{
  private List<Vector3> pathPoints = new List<Vector3>();
  private PathConfig currentPathConfig;
  private bool isCreatingPath = false;
  private GameObject previewParent;
  [SerializeField] private GameObject pathSegmentPrefab;

  [MenuItem("Tower Defense/Path Creator")]
  public static void ShowWindow()
  {
    GetWindow<PathCreatorWindow>("Path Creator");
  }

  private void OnGUI()
  {
    GUILayout.Label("Path Creator", EditorStyles.boldLabel);

    currentPathConfig = (PathConfig)EditorGUILayout.ObjectField(
        "Path Config", currentPathConfig, typeof(PathConfig), false
    );

    pathSegmentPrefab = (GameObject)EditorGUILayout.ObjectField(
        "Path Segment Prefab", pathSegmentPrefab, typeof(GameObject), false
    );

    if (currentPathConfig == null || pathSegmentPrefab == null)
    {
      EditorGUILayout.HelpBox("Please assign both Path Config and Path Segment Prefab.", MessageType.Warning);
      return;
    }

    currentPathConfig.pathWidth = EditorGUILayout.FloatField("Path Width", currentPathConfig.pathWidth);

    if (!isCreatingPath)
    {
      if (GUILayout.Button("Start Creating Path"))
      {
        StartPathCreation();
      }
    }
    else
    {
      EditorGUILayout.HelpBox(
          "Click in the scene to place path points.\n" +
          "Press Escape to cancel.\n" +
          "Right-click to remove last point.",
          MessageType.Info
      );

      if (GUILayout.Button("Finish Path"))
      {
        FinishPath();
      }
    }

    if (pathPoints.Count > 0)
    {
      EditorGUILayout.LabelField($"Points: {pathPoints.Count}");
    }
  }

  private void StartPathCreation()
  {
    isCreatingPath = true;
    pathPoints.Clear();

    // Create parent object for visualization
    previewParent = new GameObject("PathPreview");
    SceneView.duringSceneGui += OnSceneGUI;
  }

  private void OnSceneGUI(SceneView sceneView)
  {
    Event e = Event.current;

    if (e.type == EventType.MouseDown)
    {
      Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
      RaycastHit hit;

      if (Physics.Raycast(ray, out hit))
      {
        if (e.button == 0) // Left click
        {
          AddPathPoint(hit.point);
          e.Use();
        }
        else if (e.button == 1 && pathPoints.Count > 0) // Right click
        {
          RemoveLastPoint();
          e.Use();
        }
      }
    }
    else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
    {
      CancelPathCreation();
      e.Use();
    }

    if (pathPoints.Count >= 2)
    {
      UpdatePreviewSegments();
    }

    // Draw the path
    Handles.color = Color.yellow;
    for (int i = 0; i < pathPoints.Count - 1; i++)
    {
      Handles.DrawLine(pathPoints[i], pathPoints[i + 1]);
      Handles.SphereHandleCap(
          0,
          pathPoints[i],
          Quaternion.identity,
          0.5f,
          EventType.Repaint
      );
    }

    if (pathPoints.Count > 0)
    {
      Handles.SphereHandleCap(
          0,
          pathPoints[pathPoints.Count - 1],
          Quaternion.identity,
          0.5f,
          EventType.Repaint
      );
    }

    sceneView.Repaint();
  }

  private void AddPathPoint(Vector3 point)
  {
    pathPoints.Add(point);
    CreateVisualPoint(point);
  }

  private void RemoveLastPoint()
  {
    if (pathPoints.Count > 0)
    {
      pathPoints.RemoveAt(pathPoints.Count - 1);
      DestroyLastVisualPoint();
    }
  }

  private void CreateVisualPoint(Vector3 position)
  {
    GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    point.transform.position = position;
    point.transform.localScale = Vector3.one * 0.5f;
    point.transform.parent = previewParent.transform;
  }

  private void DestroyLastVisualPoint()
  {
    if (previewParent != null && previewParent.transform.childCount > 0)
    {
      DestroyImmediate(previewParent.transform.GetChild(
          previewParent.transform.childCount - 1).gameObject
      );
    }
  }

  private void UpdatePreviewSegments()
  {
    ClearPreviewSegments();
    previewParent = new GameObject("PathPreview");

    for (int i = 0; i < pathPoints.Count - 1; i++)
    {
      Vector3 startPoint = pathPoints[i];
      Vector3 endPoint = pathPoints[i + 1];

      Vector3 midPoint = (startPoint + endPoint) / 2f;
      float length = Vector3.Distance(startPoint, endPoint);

      GameObject segment = Instantiate(pathSegmentPrefab, midPoint, Quaternion.identity, previewParent.transform);
      segment.transform.LookAt(endPoint);

      Vector3 scale = segment.transform.localScale;
      scale.z = length;
      scale.x = currentPathConfig.pathWidth;
      segment.transform.localScale = scale;
    }
  }

  private void ClearPreviewSegments()
  {
    if (previewParent != null)
    {
      foreach (Transform child in previewParent.transform)
      {
        DestroyImmediate(child.gameObject);
      }
      DestroyImmediate(previewParent);
    }
  }

  private void FinishPath()
  {
    if (pathPoints.Count < 2)
    {
      EditorUtility.DisplayDialog(
          "Invalid Path",
          "Path must have at least 2 points.",
          "OK"
      );
      return;
    }

    currentPathConfig.segments = new PathConfig.PathSegment[pathPoints.Count - 1];

    for (int i = 0; i < pathPoints.Count - 1; i++)
    {
      currentPathConfig.segments[i] = new PathConfig.PathSegment
      {
        startPoint = pathPoints[i],
        endPoint = pathPoints[i + 1]
      };
    }

    EditorUtility.SetDirty(currentPathConfig);
    AssetDatabase.SaveAssets();

    CancelPathCreation();
    Debug.Log("Path saved to config!");
  }

  private void CancelPathCreation()
  {
    isCreatingPath = false;
    pathPoints.Clear();
    SceneView.duringSceneGui -= OnSceneGUI;

    if (previewParent != null)
    {
      ClearPreviewSegments();
    }
  }

  private void OnDestroy()
  {
    CancelPathCreation();
  }
}
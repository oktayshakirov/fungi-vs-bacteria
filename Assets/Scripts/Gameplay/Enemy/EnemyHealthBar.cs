using UnityEngine;

// Builds a small billboarded health bar above the enemy at runtime,
// so no prefab or scene wiring is required.
public class EnemyHealthBar : MonoBehaviour
{
  private const float BarWidth = 1.6f;
  private const float BarHeight = 0.22f;
  private const float HeightMargin = 0.5f;

  private static Material backgroundMaterial;
  // Three shared fill materials rather than one per enemy. A per-enemy material
  // cannot batch, so a swarm cost one draw call each and allocated as it span up.
  private static Material[] fillMaterials;

  private Transform barRoot;
  private Transform fill;
  private MeshRenderer fillRenderer;
  private Camera mainCamera;
  private float verticalOffset;

  private void Awake()
  {
    mainCamera = Camera.main;
    CreateBar();
  }

  private void CreateBar()
  {
    // Measure the body before the bar quads add their own renderers
    Renderer bodyRenderer = GetComponentInChildren<MeshRenderer>();
    verticalOffset = (bodyRenderer != null ? bodyRenderer.bounds.size.y : 1f) + HeightMargin;

    barRoot = new GameObject("HealthBar").transform;
    barRoot.SetParent(transform, false);

    Transform background = CreateQuad("Background", barRoot);
    background.localScale = new Vector3(BarWidth, BarHeight, 1f);
    background.GetComponent<MeshRenderer>().sharedMaterial = GetBackgroundMaterial();

    fill = CreateQuad("Fill", barRoot);
    fillRenderer = fill.GetComponent<MeshRenderer>();
    fillRenderer.sharedMaterial = FillMaterial(1f);
    fill.localPosition = new Vector3(0f, 0f, -0.01f);
    fill.localScale = new Vector3(BarWidth, BarHeight * 0.7f, 1f);
  }

  // Banded, not interpolated: three shared materials keep every health bar in
  // one batch, and the bands read more clearly than a continuous gradient.
  private static Material FillMaterial(float percent)
  {
    if (fillMaterials == null)
    {
      Material src = GetBackgroundMaterial();
      fillMaterials = new[]
      {
        new Material(src) { color = new Color(0.85f, 0.22f, 0.20f) },  // critical
        new Material(src) { color = new Color(0.95f, 0.72f, 0.15f) },  // hurt
        new Material(src) { color = new Color(0.35f, 0.80f, 0.30f) },  // healthy
      };
      foreach (Material m in fillMaterials) m.enableInstancing = true;
    }

    if (percent <= 0.33f) return fillMaterials[0];
    return percent <= 0.66f ? fillMaterials[1] : fillMaterials[2];
  }

  private static Material GetBackgroundMaterial()
  {
    if (backgroundMaterial == null)
    {
      Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
      if (shader == null) shader = Shader.Find("Unlit/Color");
      backgroundMaterial = new Material(shader) { color = new Color(0.1f, 0.1f, 0.1f, 1f) };
    }
    return backgroundMaterial;
  }

  private static Transform CreateQuad(string name, Transform parent)
  {
    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
    quad.name = name;
    Destroy(quad.GetComponent<Collider>());
    quad.transform.SetParent(parent, false);
    MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    renderer.receiveShadows = false;
    return quad.transform;
  }

  public void SetHealth(float percent)
  {
    if (fill == null) return;

    percent = Mathf.Clamp01(percent);
    fill.localScale = new Vector3(BarWidth * percent, BarHeight * 0.7f, 1f);
    fill.localPosition = new Vector3(-BarWidth * (1f - percent) * 0.5f, 0f, -0.01f);
    fillRenderer.sharedMaterial = FillMaterial(percent);

    // Only show the bar once the enemy has taken damage
    barRoot.gameObject.SetActive(percent < 1f);
  }

  private void LateUpdate()
  {
    if (barRoot == null || mainCamera == null) return;

    barRoot.position = transform.position + Vector3.up * verticalOffset;
    barRoot.rotation = Quaternion.LookRotation(barRoot.position - mainCamera.transform.position);
  }

}

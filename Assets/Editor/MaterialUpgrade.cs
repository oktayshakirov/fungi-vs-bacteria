using UnityEditor;
using UnityEngine;

// The ground material was still on a built-in-pipeline shader, so it ignored
// lighting and could not receive shadows. Everything else in the project
// already uses URP shaders.
public static class MaterialUpgrade
{
  private const string GroundMaterialPath = "Assets/Materials/Environments/Ground 1.mat";

  [MenuItem("Tools/Display/Upgrade Ground Material to URP Lit")]
  public static void UpgradeGround()
  {
    Material material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
    if (material == null)
    {
      Debug.LogError($"MATERIAL UPGRADE FAIL: {GroundMaterialPath} not found");
      if (Application.isBatchMode) EditorApplication.Exit(1);
      return;
    }

    Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
    if (urpLit == null)
    {
      Debug.LogError("MATERIAL UPGRADE FAIL: URP Lit shader not found");
      if (Application.isBatchMode) EditorApplication.Exit(1);
      return;
    }

    // Preserve the existing texture and tiling across the shader swap
    Texture mainTexture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
    Vector2 scale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
    Vector2 offset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;

    material.shader = urpLit;

    if (mainTexture != null)
    {
      material.SetTexture("_BaseMap", mainTexture);
      material.SetTextureScale("_BaseMap", scale);
      material.SetTextureOffset("_BaseMap", offset);
    }
    material.SetColor("_BaseColor", Color.white);

    // The legacy material carried a grey emission that the old unlit shader
    // ignored; under Lit it washes the grass out to cream and blows out bloom.
    material.DisableKeyword("_EMISSION");
    if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

    // Matte ground: no specular sheen sliding across the board as it tilts
    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
    if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 0f);
    material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");

    EditorUtility.SetDirty(material);
    AssetDatabase.SaveAssets();

    Debug.Log($"MATERIAL UPGRADE OK: ground now uses {material.shader.name}");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }
}

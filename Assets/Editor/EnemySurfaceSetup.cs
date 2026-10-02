using UnityEditor;
using UnityEngine;

// Baked shared textures and opaque materials: no per-enemy texture generation.
public static class EnemySurfaceSetup
{
    const string Folder = "Assets/Materials/Enemies";
    public static Material ShieldMaterial()
    {
        return Surface("ShieldCarapace", new Color(.38f,.70f,.60f), true);
    }

    // Brightness lives in the TEXTURE, not in _BaseColor: Enemy writes the
    // type's colour and the biome tint into a MaterialPropertyBlock on the
    // renderer, which overrides _BaseColor on whichever material is bound. A
    // difference authored into the colour would simply be erased at runtime.

    static Material Surface(string name, Color color, bool plated, float brightness = 1f)
    {
        string path=$"{Folder}/{name}.asset";
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture==null)
        {
            const int size=256;
            texture=new Texture2D(size,size,TextureFormat.RGB24,true) {name=name,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;
                float noise=.5f+.25f*Mathf.Sin(u*Mathf.PI*8+Mathf.Sin(v*Mathf.PI*6))+.25f*Mathf.Cos(v*Mathf.PI*10+Mathf.Sin(u*Mathf.PI*4));
                float tone=(.78f+noise*.20f)*brightness;
                if(plated)
                {
                    // Staggered organic cells with pale seams baked into the skin.
                    float px=u*10,py=v*10;int ix=Mathf.FloorToInt(px),iy=Mathf.FloorToInt(py);
                    float first=100,second=100;
                    for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                    {
                        int cx=ix+dx,cy=iy+dy,wx=(cx%10+10)%10,wy=(cy%10+10)%10;
                        float jitter=Mathf.Sin(wx*12.9898f+wy*78.233f)*43758.5453f;jitter-=Mathf.Floor(jitter);
                        Vector2 delta=new Vector2(px-cx-.25f-jitter*.5f,py-cy-.3f-(1-jitter)*.4f);
                        float distance=delta.sqrMagnitude;
                        if(distance<first){second=first;first=distance;}else second=Mathf.Min(second,distance);
                    }
                    float seam=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((second-first)/.13f));
                    tone=Mathf.Lerp(.53f+noise*.15f,1f,seam)*brightness;
                }
                pixels[y*size+x]=new Color(tone,tone,tone,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,false);AssetDatabase.CreateAsset(texture,path);
        }
        string materialPath=$"{Folder}/{name}.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
        material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",color);
        material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.12f);
        material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        material.SetFloat("_EnvironmentReflections",0);material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        material.DisableKeyword("_EMISSION");material.SetColor("_EmissionColor",Color.black);
        material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
    }

    // The same skin WITHOUT the plating, worn once the absorb pool is empty.
    // Darkened a little as well as unplated: the plating is the loud half of
    // the cue, but a plain enemy that is also plainly duller reads at the
    // distance the play camera actually sits at.
    public static Material BareShieldMaterial()
    {
        return Surface("ShieldBare", new Color(.38f,.70f,.60f), false, .72f);
    }

    [MenuItem("Tools/Enemies/Apply Matte Enemy Surfaces")]
    public static void Apply()
    {
        var shield=ShieldMaterial();
        EditPrefab("ShieldedEnemy",shield,true);
        EditPrefab("BossEnemy",Surface("BossMatteSkin",new Color(.52f,.22f,.44f),false),false);
        var config=AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/ShieldedEnemy.asset");
        config.overrideBodyColor=true;config.bodyColor=new Color(.38f,.70f,.60f);EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("ENEMY SURFACES OK: shield orb removed, opaque patterned skin, matte nonmetallic boss");
    }
    static void EditPrefab(string name,Material material,bool removeOrb)
    {
        string path=$"Assets/Prefabs/Enemies/{name}.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            if(removeOrb)foreach(var trait in root.GetComponentsInChildren<EnemyTrait>(true))
                if(trait.hideWhileShieldDown || trait.name.StartsWith("ShieldBubble"))Object.DestroyImmediate(trait.gameObject);
            var body=Enemy.FindBodyRenderer(root);body.sharedMaterial=material;
            // Deleting the orb took the only in-world sign that the shield was
            // down with it. ShieldSkin puts that cue back as a material swap on
            // the body: plated while the pool holds, bare once it is empty.
            if(removeOrb)
            {
                var skin=root.GetComponent<ShieldSkin>();
                if(skin==null)skin=root.AddComponent<ShieldSkin>();
                skin.Bind(material,BareShieldMaterial());
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
}

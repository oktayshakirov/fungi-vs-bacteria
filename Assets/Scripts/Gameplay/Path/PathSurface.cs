using UnityEngine;

// One small seamless grain texture shared by gameplay, island previews and UI.
public static class PathSurface
{
    static Texture2D grain;
    public static Color Edge(Color road) => new Color(road.r*.89f,road.g*.89f,road.b*.89f,1);
    public static Texture2D Grain
    {
        get
        {
            if(grain!=null) return grain;
            const int size=128;
            grain=new Texture2D(size,size,TextureFormat.RGBA32,true) { name="Path mineral grain", wrapMode=TextureWrapMode.Repeat, filterMode=FilterMode.Trilinear };
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float u=x/(float)size, v=y/(float)size;
                float a=Mathf.PerlinNoise(u*5+17,v*5+29), b=Mathf.PerlinNoise(u*5+12,v*5+29);
                float c=Mathf.PerlinNoise(u*5+17,v*5+24), d=Mathf.PerlinNoise(u*5+12,v*5+24);
                float broad=(Mathf.Lerp(Mathf.Lerp(a,b,u),Mathf.Lerp(c,d,u),v)-.5f)*.14f;
                uint hash=unchecked((uint)(x*374761393+y*668265263)); hash=(hash^(hash>>13))*1274126177u;
                float fine=((hash&255)/255f-.5f)*.065f;
                byte tone=(byte)(Mathf.Clamp01(.93f+broad+fine)*255);
                pixels[y*size+x]=new Color32(tone,tone,tone,255);
            }
            grain.SetPixels32(pixels); grain.Apply(true,true);
            return grain;
        }
    }
    public static void Apply(Material material)
    {
        material.mainTexture=Grain;
        if(material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap",Grain);
        material.mainTextureScale=new Vector2(.28f,1);
        if(material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",.08f);
    }
}

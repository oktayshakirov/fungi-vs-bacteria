Shader "FungiVsBacteria/GradientSky"
{
    Properties
    {
        _Top ("Top Color", Color) = (0.20, 0.48, 0.90, 1)
        _Horizon ("Horizon Color", Color) = (0.72, 0.88, 0.98, 1)
        _Bottom ("Bottom Color", Color) = (0.50, 0.70, 0.60, 1)
        _Sharpness ("Horizon Sharpness", Float) = 1.2

        _SunDir ("Sun Direction", Vector) = (0.3, 0.6, 0.5, 0)
        _SunColor ("Sun Color", Color) = (1, 0.95, 0.8, 1)
        _SunSize ("Sun Size", Float) = 0.004
        _SunGlow ("Sun Glow", Float) = 8

        _HazeColor ("Haze Color", Color) = (1, 1, 1, 1)
        _HazeStrength ("Haze Strength", Float) = 0.25

        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
        _CloudStrength ("Cloud Strength", Float) = 0.0
        _CloudScale ("Cloud Scale", Float) = 2.0

        _StarStrength ("Star Strength", Float) = 0.0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            float4 _Top, _Horizon, _Bottom;
            float _Sharpness;
            float4 _SunDir, _SunColor;
            float _SunSize, _SunGlow;
            float4 _HazeColor;
            float _HazeStrength;
            float4 _CloudColor;
            float _CloudStrength, _CloudScale, _StarStrength;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float s = 0.0;
                float a = 0.5;
                for (int i = 0; i < 4; i++) { s += a * vnoise(p); p *= 2.0; a *= 0.5; }
                return s;
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dir);
                float y = dir.y;

                // Base vertical gradient
                float up = pow(saturate(y), _Sharpness);
                float down = pow(saturate(-y), _Sharpness);
                half3 col = _Horizon.rgb;
                col = lerp(col, _Top.rgb, up);
                col = lerp(col, _Bottom.rgb, down);

                // Atmospheric haze band around the horizon
                float haze = exp(-abs(y) * 6.0) * _HazeStrength;
                col = lerp(col, _HazeColor.rgb, saturate(haze));

                // Drifting clouds banded around the horizon, so they sit behind
                // the terrain and stay visible in the low sky the camera shows
                if (_CloudStrength > 0.0 && y > 0.0)
                {
                    float2 uv = dir.xz / max(y, 0.05) * _CloudScale + _Time.y * 0.01;
                    float c = fbm(uv);
                    float band = smoothstep(-0.02, 0.10, y) * (1.0 - smoothstep(0.45, 0.85, y));
                    c = smoothstep(0.42, 0.82, c) * band;
                    col = lerp(col, _CloudColor.rgb, c * _CloudStrength);
                }

                // Stars for night skies, visible from just above the horizon
                if (_StarStrength > 0.0 && y > 0.01)
                {
                    float2 suv = floor(dir.xz / max(y, 0.08) * 55.0);
                    float st = hash21(suv);
                    float star = step(0.975, st) * smoothstep(0.01, 0.12, y);
                    col += star * _StarStrength;
                }

                // Sun / moon disc and glow
                float3 sd = normalize(_SunDir.xyz);
                float sdot = saturate(dot(dir, sd));
                float glow = pow(sdot, _SunGlow);
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.3, sdot);
                col += _SunColor.rgb * (glow * 0.5 + disc);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

Shader "Custom/Sprite/CrayonOutlineSharpOuter_Expanded_BuiltIn"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _CrayonOutlineColor ("Crayon Outline Color", Color) = (1,1,1,1)
        _CrayonOutlineThickness ("Crayon Outline Thickness (px)", Range(0, 16)) = 5

        _OuterOutlineColor ("Outer Outline Color", Color) = (1,1,1,1)
        _OuterOutlineThickness ("Outer Outline Thickness (px)", Range(0, 24)) = 7

        _CrayonNoiseScale ("Crayon Noise Scale", Range(1, 80)) = 24
        _CrayonNoiseAmount ("Crayon Noise Amount", Range(0, 1)) = 0.2
        _CrayonGrain ("Crayon Grain", Range(0, 2)) = 0.4

        _WobbleAmount ("Wobble Amount", Range(0, 3)) = 0.6
        _WobbleSpeed ("Wobble Speed", Range(0, 3)) = 0.35
        _WobbleFrequency ("Wobble Frequency", Range(0, 12)) = 4.0

        _ExpandPixels ("Geometry Expand (px)", Range(0, 32)) = 10

        [MaterialToggle] PixelSnap ("Pixel Snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ PIXELSNAP_ON

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float2 originalUV    : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            fixed4 _Color;
            fixed4 _CrayonOutlineColor;
            fixed4 _OuterOutlineColor;

            float _CrayonOutlineThickness;
            float _OuterOutlineThickness;

            float _CrayonNoiseScale;
            float _CrayonNoiseAmount;
            float _CrayonGrain;

            float _WobbleAmount;
            float _WobbleSpeed;
            float _WobbleFrequency;

            float _ExpandPixels;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;

                v += noise2(p) * a; p = p * 2.03 + 13.1; a *= 0.5;
                v += noise2(p) * a; p = p * 2.01 + 7.7;  a *= 0.5;
                v += noise2(p) * a;

                return v;
            }

            float sampleSpriteAlpha(float2 uv)
            {
                // 枠外UVは透明として扱う
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return 0.0;

                return tex2D(_MainTex, uv).a;
            }

            float sampleExpandedAlpha(float2 uv, float radiusPx, float2 wobble)
            {
                float2 texel = _MainTex_TexelSize.xy;
                float a = 0.0;

                float2 d0  = float2( 1,  0);
                float2 d1  = float2(-1,  0);
                float2 d2  = float2( 0,  1);
                float2 d3  = float2( 0, -1);
                float2 d4  = normalize(float2( 1,  1));
                float2 d5  = normalize(float2(-1,  1));
                float2 d6  = normalize(float2( 1, -1));
                float2 d7  = normalize(float2(-1, -1));
                float2 d8  = normalize(float2( 2,  1));
                float2 d9  = normalize(float2(-2,  1));
                float2 d10 = normalize(float2( 2, -1));
                float2 d11 = normalize(float2(-2, -1));

                #define SAMPLE_AT(dir) a = max(a, sampleSpriteAlpha(uv + (dir) * texel * radiusPx + wobble))

                SAMPLE_AT(d0);
                SAMPLE_AT(d1);
                SAMPLE_AT(d2);
                SAMPLE_AT(d3);
                SAMPLE_AT(d4);
                SAMPLE_AT(d5);
                SAMPLE_AT(d6);
                SAMPLE_AT(d7);
                SAMPLE_AT(d8);
                SAMPLE_AT(d9);
                SAMPLE_AT(d10);
                SAMPLE_AT(d11);

                return a;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;

                // 元のUV
                float2 uv = IN.texcoord;
                OUT.originalUV = uv;

                // quad の各頂点がどの角かを UV から判定
                // (0,0)->(-1,-1), (1,1)->(+1,+1)
                float2 cornerSign = uv * 2.0 - 1.0;

                // expand量を object space に変換
                // Sprite quad は通常、ローカル空間で width = textureWidth / PPU, height = textureHeight / PPU
                // texel size = 1/textureSize なので、object space 拡張量は
                //   vertex.xy * (expandPixels * 2 / textureSize)
                // のスケール係数で近似できる
                float2 expandScale = _MainTex_TexelSize.xy * _ExpandPixels * 2.0;

                float4 pos = IN.vertex;
                pos.xy += cornerSign * abs(IN.vertex.xy) * expandScale;

                OUT.vertex = UnityObjectToClipPos(pos);

                // UV補正:
                // 広げたquad上でも見た目の元絵サイズを維持するため、
                // 0..1 の範囲を少し内側に圧縮し、外側領域では 0 未満 / 1 超えのUVを許可する
                float2 expandUV = _MainTex_TexelSize.xy * _ExpandPixels;
                OUT.uv = lerp(-expandUV, 1.0 + expandUV, uv);

                OUT.color = IN.color * _Color;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                fixed4 baseSample = 0;
                if (uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0)
                {
                    baseSample = tex2D(_MainTex, uv) * i.color;
                }

                float baseAlpha = baseSample.a;

                float t = _Time.y * _WobbleSpeed;
                float2 texel = _MainTex_TexelSize.xy;

                float2 wobbleA = float2(
                    sin(t + uv.y * _WobbleFrequency),
                    cos(t * 0.83 + uv.x * (_WobbleFrequency * 0.9))
                );

                float2 wobbleB = float2(
                    cos(t * 0.61 + (uv.x + uv.y) * (_WobbleFrequency * 0.7)),
                    sin(t * 0.73 + (uv.x - uv.y) * (_WobbleFrequency * 1.1))
                );

                float2 wobble = (wobbleA + wobbleB * 0.6) * (_WobbleAmount * texel);

                float n1 = fbm(uv * _CrayonNoiseScale + float2(t * 0.07, -t * 0.05));
                float n2 = fbm(uv * (_CrayonNoiseScale * 1.9) + float2(-t * 0.03, t * 0.04));
                float crayonNoise = saturate(n1 * 0.7 + n2 * 0.3);

                float thicknessJitter = lerp(0.9, 1.1, crayonNoise);

                float crayonRadius = max(1.0, _CrayonOutlineThickness * thicknessJitter);
                float outerRadius  = max(crayonRadius + 0.5, _OuterOutlineThickness);

                float expandedCrayon = sampleExpandedAlpha(uv, crayonRadius, wobble);
                float rawCrayonOutline = saturate(expandedCrayon - baseAlpha);

                float expandedOuter = sampleExpandedAlpha(uv, outerRadius, 0);
                float rawOuterBand = saturate(expandedOuter - expandedCrayon);

                float edgeNoise = lerp(1.0 - _CrayonNoiseAmount, 1.0, crayonNoise);
                float grain = noise2(uv * (_CrayonNoiseScale * 4.0) + float2(t * 0.1, t * 0.06));
                float grainMask = lerp(1.0, smoothstep(0.10, 1.0, grain), saturate(_CrayonGrain * 0.35));

                float crayonMask = lerp(0.85, 1.0, edgeNoise) * grainMask;
                float crayonAlpha = rawCrayonOutline * crayonMask * _CrayonOutlineColor.a;

                float outerAlpha = rawOuterBand * _OuterOutlineColor.a;

                float3 outerPremul  = _OuterOutlineColor.rgb  * outerAlpha;
                float3 crayonPremul = _CrayonOutlineColor.rgb * crayonAlpha;

                float outlineAlpha = saturate(outerAlpha + crayonAlpha);
                float3 outlinePremul = outerPremul * (1.0 - crayonAlpha) + crayonPremul;

                float3 spritePremul = baseSample.rgb * baseAlpha;

                fixed4 result;
                result.rgb = outlinePremul * (1.0 - baseAlpha) + spritePremul;
                result.a   = saturate(baseAlpha + outlineAlpha * (1.0 - baseAlpha));

                return result;
            }
            ENDCG
        }
    }
}
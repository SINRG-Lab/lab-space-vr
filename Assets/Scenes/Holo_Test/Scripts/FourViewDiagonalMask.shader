Shader "Hidden/FourViewDiagonalMask"
{
    Properties
    {
        _Inset ("Inset Size (0..1)", Range(0,1)) = 0.92
        _GapSize ("Center Gap (0..0.25)", Range(0,0.25)) = 0.08
        _BGColor ("Background", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _RightTex;
            sampler2D _LeftTex;
            sampler2D _TopTex;
            sampler2D _BottomTex;

            float4 _RightTex_TexelSize;
            float4 _LeftTex_TexelSize;
            float4 _TopTex_TexelSize;
            float4 _BottomTex_TexelSize;

            float _Inset;     // 1.0 = max square that fits in triangle
            float _GapSize;
            fixed4 _BGColor;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            // Triangles defined in centered coords p = uv - 0.5
            bool InRightTri(float2 p)  { return (p.x >= 0 && abs(p.x) >= abs(p.y)); }
            bool InLeftTri(float2 p)   { return (p.x <= 0 && abs(p.x) >= abs(p.y)); }
            bool InTopTri(float2 p)    { return (p.y >= 0 && abs(p.y) >= abs(p.x)); }
            bool InBottomTri(float2 p) { return (p.y <= 0 && abs(p.y) >= abs(p.x)); }

            // Fit a full texture into a square WITHOUT cropping (letterbox/pillarbox)
            float2 FitUV(float2 uv01, float texAspect)
            {
                // uv01 is 0..1 inside the inset square
                // We want to preserve aspect, so we compress one axis (letterbox)
                float squareAspect = 1.0;

                float2 u = uv01;

                if (texAspect > squareAspect)
                {
                    // texture is wider: compress X (pillarbox)
                    float k = squareAspect / texAspect;  // < 1
                    u.x = (u.x - 0.5) * k + 0.5;
                }
                else
                {
                    // texture is taller: compress Y (letterbox)
                    float k = texAspect / squareAspect;  // < 1
                    u.y = (u.y - 0.5) * k + 0.5;
                }
                return u;
            }

            fixed4 SampleInset(
                sampler2D tex, float4 texelSize, float2 uv, float2 centerUV, float halfSize)
            {
                // Convert screen uv -> local coords inside the inset square
                float2 local = (uv - centerUV) / (halfSize * 2.0) + 0.5; // 0..1 across square

                // If outside inset square, we don't draw the feed
                if (local.x < 0 || local.x > 1 || local.y < 0 || local.y > 1)
                    return _BGColor;

                // Preserve aspect (no crop)
                float texAspect = texelSize.z / max(texelSize.w, 1.0); // width/height
                float2 fit = FitUV(local, texAspect);

                // If FitUV pushed coords outside 0..1, that's the letterbox area
                if (fit.x < 0 || fit.x > 1 || fit.y < 0 || fit.y > 1)
                    return _BGColor;

                return tex2D(tex, fit);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 p  = uv - 0.5;

                // Center diamond gap (looks better in physical pyramids)
                float diamond = abs(p.x) + abs(p.y);
                if (diamond < _GapSize)
                    return _BGColor;

                // Max axis-aligned square that fits in a 45° wedge triangle has half-size = 1/6 in p-space.
                // (So side length ~ 1/3 of the full frame). We'll scale it by _Inset (0..1).
                float baseHalf = (1.0 / 6.0) * saturate(_Inset);

                // Centers of those inset squares (in uv space):
                // Right:  (0.5 + 1/3, 0.5) = (0.8333, 0.5)
                // Left:   (0.5 - 1/3, 0.5) = (0.1667, 0.5)
                // Top:    (0.5, 0.5 + 1/3) = (0.5, 0.8333)
                // Bottom: (0.5, 0.5 - 1/3) = (0.5, 0.1667)
                float2 cR = float2(0.8333333, 0.5);
                float2 cL = float2(0.1666667, 0.5);
                float2 cT = float2(0.5, 0.8333333);
                float2 cB = float2(0.5, 0.1666667);

                // Only show content in the correct triangle
                if (InRightTri(p))  return SampleInset(_RightTex,  _RightTex_TexelSize,  uv, cR, baseHalf);
                if (InLeftTri(p))   return SampleInset(_LeftTex,   _LeftTex_TexelSize,   uv, cL, baseHalf);
                if (InTopTri(p))    return SampleInset(_TopTex,    _TopTex_TexelSize,    uv, cT, baseHalf);
                if (InBottomTri(p)) return SampleInset(_BottomTex, _BottomTex_TexelSize, uv, cB, baseHalf);

                return _BGColor;
            }
            ENDCG
        }
    }
}
Shader "Hidden/FourViewDiagonalMaskRot"
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

            sampler2D _TopTex, _LeftTex, _BottomTex, _RightTex;

            float4 _TopTex_TexelSize;
            float4 _LeftTex_TexelSize;
            float4 _BottomTex_TexelSize;
            float4 _RightTex_TexelSize;

            float _Inset, _GapSize;
            fixed4 _BGColor;

            // orientation controls (floats for compatibility)
            float _TopRot, _TopFlipX, _TopFlipY;
            float _LeftRot, _LeftFlipX, _LeftFlipY;
            float _BottomRot, _BottomFlipX, _BottomFlipY;
            float _RightRot, _RightFlipX, _RightFlipY;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            bool InRightTri(float2 p)  { return (p.x >= 0 && abs(p.x) >= abs(p.y)); }
            bool InLeftTri(float2 p)   { return (p.x <= 0 && abs(p.x) >= abs(p.y)); }
            bool InTopTri(float2 p)    { return (p.y >= 0 && abs(p.y) >= abs(p.x)); }
            bool InBottomTri(float2 p) { return (p.y <= 0 && abs(p.y) >= abs(p.x)); }

            float2 ApplyTransform(float2 uv, float rotF, float flipXF, float flipYF)
            {
                int rot   = (int)round(rotF);    // 0..3
                int flipX = (int)round(flipXF);  // 0/1
                int flipY = (int)round(flipYF);  // 0/1

                if (flipX == 1) uv.x = 1.0 - uv.x;
                if (flipY == 1) uv.y = 1.0 - uv.y;

                float2 c = uv - 0.5;
                if (rot == 1)      c = float2( c.y, -c.x); // 90 cw
                else if (rot == 2) c = float2(-c.x, -c.y); // 180
                else if (rot == 3) c = float2(-c.y,  c.x); // 270 cw
                return c + 0.5;
            }

            float2 FitUV(float2 uv01, float texAspect)
            {
                float2 u = uv01;

                if (texAspect > 1.0)
                {
                    float k = 1.0 / texAspect;   // compress X
                    u.x = (u.x - 0.5) * k + 0.5;
                }
                else
                {
                    float k = texAspect;         // compress Y
                    u.y = (u.y - 0.5) * k + 0.5;
                }
                return u;
            }

            fixed4 SampleInset(
                sampler2D tex, float4 texelSize,
                float2 uv, float2 centerUV, float halfSize,
                float rotF, float flipXF, float flipYF)
            {
                float2 local = (uv - centerUV) / (halfSize * 2.0) + 0.5; // 0..1 in inset square
                if (local.x < 0 || local.x > 1 || local.y < 0 || local.y > 1)
                    return _BGColor;

                float texAspect = texelSize.z / max(texelSize.w, 1.0);
                float2 fit = FitUV(local, texAspect);

                // letterbox area -> background
                if (fit.x < 0 || fit.x > 1 || fit.y < 0 || fit.y > 1)
                    return _BGColor;

                fit = ApplyTransform(fit, rotF, flipXF, flipYF);
                return tex2D(tex, fit);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 p  = uv - 0.5;

                // Center gap (diamond)
                float diamond = abs(p.x) + abs(p.y);
                if (diamond < _GapSize) return _BGColor;

                float baseHalf = (1.0 / 6.0) * saturate(_Inset);

                float2 cR = float2(0.8333333, 0.5);
                float2 cL = float2(0.1666667, 0.5);
                float2 cT = float2(0.5, 0.8333333);
                float2 cB = float2(0.5, 0.1666667);

                if (InRightTri(p))
                    return SampleInset(_RightTex, _RightTex_TexelSize, uv, cR, baseHalf, _RightRot, _RightFlipX, _RightFlipY);

                if (InLeftTri(p))
                    return SampleInset(_LeftTex, _LeftTex_TexelSize, uv, cL, baseHalf, _LeftRot, _LeftFlipX, _LeftFlipY);

                if (InTopTri(p))
                    return SampleInset(_TopTex, _TopTex_TexelSize, uv, cT, baseHalf, _TopRot, _TopFlipX, _TopFlipY);

                if (InBottomTri(p))
                    return SampleInset(_BottomTex, _BottomTex_TexelSize, uv, cB, baseHalf, _BottomRot, _BottomFlipX, _BottomFlipY);

                return _BGColor;
            }
            ENDCG
        }
    }
}
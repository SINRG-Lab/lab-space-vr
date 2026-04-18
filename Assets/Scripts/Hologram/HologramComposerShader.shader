Shader "Custom/HologramComposerShader"
{
    Properties
    {
        _TopTex ("Top", 2D) = "black" {}
        _LeftTex ("Left", 2D) = "black" {}
        _BottomTex ("Bottom", 2D) = "black" {}
        _RightTex ("Right", 2D) = "black" {}

        _Background ("Background", Color) = (0,0,0,1)

        _DiamondHalfWidth ("Diamond Half Width", Float) = 0.22
        _DiamondHalfHeight ("Diamond Half Height", Float) = 0.22
        _ContentScale ("Content Scale", Float) = 0.9

        _FlipTopX ("Flip Top X", Float) = 0
        _FlipTopY ("Flip Top Y", Float) = 0
        _FlipLeftX ("Flip Left X", Float) = 0
        _FlipLeftY ("Flip Left Y", Float) = 0
        _FlipBottomX ("Flip Bottom X", Float) = 0
        _FlipBottomY ("Flip Bottom Y", Float) = 0
        _FlipRightX ("Flip Right X", Float) = 0
        _FlipRightY ("Flip Right Y", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _TopTex;
            sampler2D _LeftTex;
            sampler2D _BottomTex;
            sampler2D _RightTex;

            fixed4 _Background;

            float _DiamondHalfWidth;
            float _DiamondHalfHeight;
            float _ContentScale;

            float _FlipTopX, _FlipTopY;
            float _FlipLeftX, _FlipLeftY;
            float _FlipBottomX, _FlipBottomY;
            float _FlipRightX, _FlipRightY;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            bool InDiamond(float2 p, float2 center, float halfW, float halfH)
            {
                float2 d = abs(p - center);
                return (d.x / halfW + d.y / halfH) <= 1.0;
            }

            float2 DiamondUV(float2 p, float2 center, float halfW, float halfH, float contentScale)
            {
                float2 local = (p - center) / float2(halfW, halfH);
                local /= max(contentScale, 0.0001);
                return local * 0.5 + 0.5;
            }

            float2 Rotate90CW(float2 uv)   { return float2(uv.y, 1.0 - uv.x); }
            float2 Rotate90CCW(float2 uv)  { return float2(1.0 - uv.y, uv.x); }
            float2 Rotate180(float2 uv)    { return float2(1.0 - uv.x, 1.0 - uv.y); }

            float2 ApplyFlip(float2 uv, float flipX, float flipY)
            {
                if (flipX > 0.5) uv.x = 1.0 - uv.x;
                if (flipY > 0.5) uv.y = 1.0 - uv.y;
                return uv;
            }

            fixed4 SampleSafe(sampler2D tex, float2 uv)
            {
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return _Background;

                return tex2D(tex, uv);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                float2 topC    = float2(0.5, 0.75);
                float2 leftC   = float2(0.25, 0.5);
                float2 rightC  = float2(0.75, 0.5);
                float2 bottomC = float2(0.5, 0.25);

                float2 suv;

                if (InDiamond(uv, topC, _DiamondHalfWidth, _DiamondHalfHeight))
                {
                    suv = DiamondUV(uv, topC, _DiamondHalfWidth, _DiamondHalfHeight, _ContentScale);
                    suv = ApplyFlip(suv, _FlipTopX, _FlipTopY);
                    return SampleSafe(_TopTex, suv);
                }
                else if (InDiamond(uv, leftC, _DiamondHalfWidth, _DiamondHalfHeight))
                {
                    suv = DiamondUV(uv, leftC, _DiamondHalfWidth, _DiamondHalfHeight, _ContentScale);
                    suv = Rotate90CCW(suv);
                    suv = ApplyFlip(suv, _FlipLeftX, _FlipLeftY);
                    return SampleSafe(_LeftTex, suv);
                }
                else if (InDiamond(uv, rightC, _DiamondHalfWidth, _DiamondHalfHeight))
                {
                    suv = DiamondUV(uv, rightC, _DiamondHalfWidth, _DiamondHalfHeight, _ContentScale);
                    suv = Rotate90CW(suv);
                    suv = ApplyFlip(suv, _FlipRightX, _FlipRightY);
                    return SampleSafe(_RightTex, suv);
                }
                else if (InDiamond(uv, bottomC, _DiamondHalfWidth, _DiamondHalfHeight))
                {
                    suv = DiamondUV(uv, bottomC, _DiamondHalfWidth, _DiamondHalfHeight, _ContentScale);
                    suv = Rotate180(suv);
                    suv = ApplyFlip(suv, _FlipBottomX, _FlipBottomY);
                    return SampleSafe(_BottomTex, suv);
                }

                return _Background;
            }
            ENDCG
        }
    }
}
Shader "Hidden/FourViewDiagonal"
{
    Properties
    {
        _CenterClamp ("Center Clamp (0..0.3)", Range(0,0.3)) = 0.06
        _Feather     ("Center Feather (0..0.2)", Range(0,0.2)) = 0.03
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

            float _CenterClamp;
            float _Feather;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 p  = uv - 0.5;

                float ax = abs(p.x);
                float ay = abs(p.y);

                // "distance" from center in our square space (0..0.5)
                float r = max(ax, ay);

                // Clamp near center to avoid infinite-ish stretching:
                // when r is tiny, wedges go to a point -> extreme scale.
                float rClamped = max(r, _CenterClamp);

                // Optional feather factor: 0 at center, 1 outside feather zone
                float featherStart = _CenterClamp;
                float featherEnd   = _CenterClamp + max(_Feather, 1e-5);
                float k = smoothstep(featherStart, featherEnd, r);

                // Decide wedge: left/right vs top/bottom
                bool lr = (ax > ay);

                if (lr)
                {
                    // LEFT/RIGHT wedge
                    // Use rClamped instead of ax in denom to prevent blow-up at center
                    float denom = max(ax, rClamped);

                    // s: 0..1 from center to side edge
                    float s = saturate(rClamped / 0.5);

                    // t: -1..1 within wedge height
                    float t = p.y / denom;

                    float2 tuv = float2(s, t * 0.5 + 0.5);

                    fixed4 col = (p.x >= 0) ? tex2D(_RightTex, tuv) : tex2D(_LeftTex, tuv);

                    // Blend toward the exact center sample to hide residual distortion
                    float2 centerUV = float2(0.0, 0.5); // center column of the view
                    fixed4 centerCol = (p.x >= 0) ? tex2D(_RightTex, centerUV) : tex2D(_LeftTex, centerUV);

                    return lerp(centerCol, col, k);
                }
                else
                {
                    // TOP/BOTTOM wedge
                    float denom = max(ay, rClamped);

                    float s = saturate(rClamped / 0.5);
                    float t = p.x / denom;

                    float2 tuv = float2(t * 0.5 + 0.5, s);

                    fixed4 col = (p.y >= 0) ? tex2D(_TopTex, tuv) : tex2D(_BottomTex, tuv);

                    float2 centerUV = float2(0.5, 0.0); // center row of the view
                    fixed4 centerCol = (p.y >= 0) ? tex2D(_TopTex, centerUV) : tex2D(_BottomTex, centerUV);

                    return lerp(centerCol, col, k);
                }
            }
            ENDCG
        }
    }
}
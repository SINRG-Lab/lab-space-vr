Shader "Hidden/FourViewAtlasTransform"
{
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

            sampler2D _Tex0, _Tex1, _Tex2, _Tex3;

            // Use floats for max compatibility
            float _T0Rot, _T0FlipX, _T0FlipY;
            float _T1Rot, _T1FlipX, _T1FlipY;
            float _T2Rot, _T2FlipX, _T2FlipY;
            float _T3Rot, _T3FlipX, _T3FlipY;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            float2 ApplyTransform(float2 uv, float rotF, float flipXF, float flipYF)
            {
                // Convert to ints safely
                int rot   = (int)round(rotF);    // 0..3
                int flipX = (int)round(flipXF);  // 0/1
                int flipY = (int)round(flipYF);  // 0/1

                if (flipX == 1) uv.x = 1.0 - uv.x;
                if (flipY == 1) uv.y = 1.0 - uv.y;

                float2 c = uv - 0.5;

                // clockwise 90° steps
                if (rot == 1)      c = float2( c.y, -c.x);
                else if (rot == 2) c = float2(-c.x, -c.y);
                else if (rot == 3) c = float2(-c.y,  c.x);

                return c + 0.5;
            }

            fixed4 SampleTile(int tileIndex, float2 localUV)
            {
                if (tileIndex == 0)
                {
                    localUV = ApplyTransform(localUV, _T0Rot, _T0FlipX, _T0FlipY);
                    return tex2D(_Tex0, localUV);
                }
                if (tileIndex == 1)
                {
                    localUV = ApplyTransform(localUV, _T1Rot, _T1FlipX, _T1FlipY);
                    return tex2D(_Tex1, localUV);
                }
                if (tileIndex == 2)
                {
                    localUV = ApplyTransform(localUV, _T2Rot, _T2FlipX, _T2FlipY);
                    return tex2D(_Tex2, localUV);
                }

                localUV = ApplyTransform(localUV, _T3Rot, _T3FlipX, _T3FlipY);
                return tex2D(_Tex3, localUV);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                bool right = (uv.x >= 0.5);
                bool top   = (uv.y >= 0.5);

                float2 localUV = float2(
                    right ? (uv.x - 0.5) * 2.0 : uv.x * 2.0,
                    top   ? (uv.y - 0.5) * 2.0 : uv.y * 2.0
                );

                int tileIndex =
                    (!right && !top) ? 0 :
                    ( right && !top) ? 1 :
                    (!right &&  top) ? 2 : 3;

                return SampleTile(tileIndex, localUV);
            }
            ENDCG
        }
    }
}
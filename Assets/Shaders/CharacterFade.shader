Shader "CCCProject/CharacterFade"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Fade ("Fade", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        // addshadow 让阴影投射也走 surf：淡出时影子一起消失，不会留下"看不见的人还在投影"。
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Metallic;
        half _Glossiness;
        half _Fade;

        struct Input
        {
            float2 uv_MainTex;
            float4 screenPos;
        };

        // 屏幕空间 Bayer 4x4 抖动阈值：图案固定在世界之外的屏幕上，镜头移动时不会"游动"。
        float Bayer2 (float2 a)
        {
            a = floor(a);
            return frac(a.x * 0.5 + a.y * a.y * 0.75);
        }

        float Bayer4 (float2 a)
        {
            return Bayer2(a * 0.5) * 0.25 + Bayer2(a);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;

            float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);
            float2 pixel = screenUV * _ScreenParams.xy;
            float threshold = Bayer4(pixel);

            // _Fade = 1 时阈值上限 0.9375，全部保留；_Fade = 0 时全部丢弃。
            clip(_Fade - threshold - 1e-4);
        }
        ENDCG
    }

    FallBack "Diffuse"
}

Shader "UI/RightEyeFisheye"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FisheyeStrength ("Fisheye Strength", Range(0, 0.8)) = 0.45
        _BlurStrength ("Misalignment Blur", Range(0, 1)) = 0
        _BlurRadiusPixels ("Blur Radius", Range(0, 24)) = 12
        _FocusCenter ("Focus Center", Vector) = (0.5, 0.5, 0, 0)
        _FocusRadius ("Focus Radius", Float) = 0.08
        _FocusFeather ("Focus Feather", Float) = 0.12
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Fisheye"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;
            float _FisheyeStrength;
            float _BlurStrength;
            float _BlurRadiusPixels;
            float4 _FocusCenter;
            float _FocusRadius;
            float _FocusFeather;
            float4 _MainTex_TexelSize;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered = i.uv * 2.0 - 1.0;
                float radiusSquared = dot(centered, centered) * 0.5;
                float distortion = 1.0 + _FisheyeStrength * radiusSquared;
                float2 sourceUv = 0.5 + 0.5 * centered / distortion;

                float distanceFromTarget = distance(i.uv, _FocusCenter.xy);
                float environmentBlur = smoothstep(
                    _FocusRadius, _FocusRadius + max(0.001, _FocusFeather), distanceFromTarget);
                float blur = saturate(_BlurStrength * environmentBlur);
                float2 blurOffset = _MainTex_TexelSize.xy * (_BlurRadiusPixels * blur);

                fixed4 color = tex2D(_MainTex, sourceUv) * 0.25;
                color += tex2D(_MainTex, sourceUv + float2(blurOffset.x, 0)) * 0.125;
                color += tex2D(_MainTex, sourceUv - float2(blurOffset.x, 0)) * 0.125;
                color += tex2D(_MainTex, sourceUv + float2(0, blurOffset.y)) * 0.125;
                color += tex2D(_MainTex, sourceUv - float2(0, blurOffset.y)) * 0.125;
                color += tex2D(_MainTex, sourceUv + blurOffset) * 0.0625;
                color += tex2D(_MainTex, sourceUv - blurOffset) * 0.0625;
                color += tex2D(_MainTex, sourceUv + float2(blurOffset.x, -blurOffset.y)) * 0.0625;
                color += tex2D(_MainTex, sourceUv + float2(-blurOffset.x, blurOffset.y)) * 0.0625;
                color *= i.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}

Shader "TechnicalSwordAction/VideoSubjectLift"
{
    Properties { [PerRendererData] _MainTex ("Sprite",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _Color;
            struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            v2f vert(appdata v) { v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o; }
            float4 frag(v2f i):SV_Target
            {
                float4 sprite=tex2D(_MainTex,i.uv);
                // Keep a silhouette legible on the near-black slow background.
                // This is a presentation overlay; source palette and alpha stay intact.
                sprite.rgb=max(sprite.rgb,float3(.23,.28,.36));
                return sprite*i.color;
            }
            ENDHLSL
        }
    }
}

Shader "TechnicalSwordAction/VideoParryBacklight"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _LightWorld("Spark position",Vector)=(0,0,0,0)
        _Bloom("Light spill",Float)=0
        _KeepGold("Preserve yellow spark",Float)=1
        [HideInInspector] _RendererColor("Renderer tint",Color)=(1,1,1,1)
        [HideInInspector] _Flip("Flip",Vector)=(1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"
            float4 _MainTex_TexelSize,_LightWorld;
            float _Bloom,_KeepGold;
            struct output
            {
                float4 vertex:SV_POSITION;
                float2 uv:TEXCOORD0;
                float2 world:TEXCOORD1;
                float4 tint:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            output vert(appdata_t v)
            {
                output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v,o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                v.vertex=UnityFlipSprite(v.vertex,_Flip);
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xy;
                o.uv=v.texcoord;o.tint=v.color*_Color*_RendererColor;
                return o;
            }
            float4 frag(output i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                if(_Bloom>.5)
                {
                    float2 p=(i.uv-.5)*2;
                    float light=exp(-dot(p,p)*6)*.6;
                    return float4(1,.98,.9,light*i.tint.a);
                }
                float4 source=SampleSpriteTexture(i.uv);
                // Keep the artist's yellow spark, including its thin rays.
                bool gold=source.r>.6&&source.g>source.r*.45&&source.b<source.r*.45;
                if(gold&&_KeepGold>.5)return source*i.tint;
                float2 dx=float2(_MainTex_TexelSize.x,0),dy=float2(0,_MainTex_TexelSize.y);
                float left=SampleSpriteTexture(i.uv-dx).a,right=SampleSpriteTexture(i.uv+dx).a;
                float down=SampleSpriteTexture(i.uv-dy).a,up=SampleSpriteTexture(i.uv+dy).a;
                float edge=1-min(min(left,right),min(down,up));
                float2 normal=float2(left-right,down-up)*_Flip.xy;
                float2 worldNormal=mul((float2x2)unity_ObjectToWorld,normal);
                float2 lightDirection=_LightWorld.xy-i.world;
                float facing=saturate(dot(normalize(worldNormal+float2(.00001,.00001)),normalize(lightDirection+float2(.00001,.00001))));
                float proximity=exp(-length(lightDirection)*.55);
                float luminance=saturate(dot(source.rgb,float3(.2126,.7152,.0722)));
                float fill=(.78+.16*proximity)*lerp(.62,1,sqrt(luminance));
                float3 white=lerp(float3(.88,.94,1)*fill,1,edge*facing*.95);
                return float4(white,source.a)*i.tint;
            }
            ENDHLSL
        }
    }
}

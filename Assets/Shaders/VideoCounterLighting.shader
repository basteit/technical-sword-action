Shader "TechnicalSwordAction/VideoCounterLighting"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _Actor("Actor lighting",Float)=0
        _Charge("Charge progress",Float)=0
        _ImpactFrame("Real impact frame",Float)=-1
        _Age("Visual seconds",Float)=0
        _Facing("Kick direction",Float)=1
        _RimPulse("Slow rim pulse",Float)=1
        _ImpactWeight("Impact fade",Float)=0
        _LightWorld("Foot light",Vector)=(0,0,0,0)
        [HideInInspector] _RendererColor("Renderer tint",Color)=(1,1,1,1)
        [HideInInspector] _Flip("Flip",Vector)=(1,1,1,1)
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha One
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"
            float4 _MainTex_TexelSize,_LightWorld;
            float _Actor,_Charge,_ImpactFrame,_Age,_Facing,_RimPulse,_ImpactWeight;
            struct output {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;float4 tint:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            output vert(appdata_t v)
            {
                output o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                v.vertex=UnityFlipSprite(v.vertex,_Flip);o.vertex=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xy;
                o.uv=v.texcoord;o.tint=v.color*_Color*_RendererColor;return o;
            }
            float4 frag(output i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                bool impact=_ImpactFrame>=0;
                float peak=impact?_ImpactWeight:0;
                float charge=_Charge*_Charge;
                float2 p=i.world-_LightWorld.xy;
                if(_Actor>.5)
                {
                    float4 source=SampleSpriteTexture(i.uv);
                    float2 dx=float2(_MainTex_TexelSize.x,0),dy=float2(0,_MainTex_TexelSize.y);
                    float edge=1-min(min(SampleSpriteTexture(i.uv-dx).a,SampleSpriteTexture(i.uv+dx).a),min(SampleSpriteTexture(i.uv-dy).a,SampleSpriteTexture(i.uv+dy).a));
                    float near=exp(-dot(p,p)*.6);
                    float fill=peak*.46;
                    float chargeRim=impact?0:.55*lerp(.35,1,charge)*_RimPulse;
                    float rim=edge*(chargeRim+peak*1.22)*(near*.75+.25);
                    return float4(float3(.35,.76,1)*rim+float3(.8,.94,1)*fill+source.rgb*peak*.12,source.a)*i.tint;
                }
                // Keep the original soft foot light. Converging and outgoing lines
                // are reserved for the artist's separate pixel-effect submission.
                float glow=exp(-dot(p,p)*12)*(charge*.45+peak*.9);
                return float4(float3(.6,.87,1)*glow,1)*i.tint;
            }
            ENDHLSL
        }
    }
}

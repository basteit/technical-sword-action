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
            float _Actor,_Charge,_ImpactFrame,_Age,_Facing;
            struct output {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;float4 tint:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            output vert(appdata_t v)
            {
                output o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                v.vertex=UnityFlipSprite(v.vertex,_Flip);o.vertex=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xy;
                o.uv=v.texcoord;o.tint=v.color*_Color*_RendererColor;return o;
            }
            float segmentGlow(float2 p,float2 a,float2 b,float width)
            {float2 d=b-a;float h=saturate(dot(p-a,d)/max(dot(d,d),.00001));float2 q=p-a-h*d;return exp(-dot(q,q)/(width*width));}
            float4 frag(output i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                bool impact=_ImpactFrame>=0;
                float frame=max(0,_ImpactFrame);
                float peak=impact?(frame<3?1:frame<8?.42*(8-frame)/5:0):0;
                float tail=impact?saturate((16-frame)/13):0;
                float charge=_Charge*_Charge;
                float2 p=i.world-_LightWorld.xy;
                if(_Actor>.5)
                {
                    float4 source=SampleSpriteTexture(i.uv);
                    float2 dx=float2(_MainTex_TexelSize.x,0),dy=float2(0,_MainTex_TexelSize.y);
                    float edge=1-min(min(SampleSpriteTexture(i.uv-dx).a,SampleSpriteTexture(i.uv+dx).a),min(SampleSpriteTexture(i.uv-dy).a,SampleSpriteTexture(i.uv+dy).a));
                    float near=exp(-dot(p,p)*.6);
                    float fill=peak*.46;
                    float rim=edge*(charge*.55+peak*1.1+tail*.12)*(near*.75+.25);
                    return float4(float3(.35,.76,1)*rim+float3(.8,.94,1)*fill+source.rgb*peak*.12,source.a)*i.tint;
                }
                p.x*=_Facing;
                float glow=exp(-dot(p,p)*12)*(charge*.45+peak*.9);
                float rays=0;
                if(!impact)
                {
                    for(int n=0;n<5;n++)
                    {
                        float a=n*1.2566+.32;float2 d=float2(cos(a),sin(a)),normal=float2(-d.y,d.x);
                        float r=lerp(1.4,.13,frac(_Age*1.45+n*.193));
                        float2 start=d*r,mid=d*r*.62+normal*.07,end=d*r*.3;
                        rays+=(segmentGlow(p,start,mid,.012)+segmentGlow(p,mid,end,.009))*charge*.55*(1-smoothstep(.9,1,_Charge)*.65);
                    }
                }
                else
                {
                    float spread=saturate(frame/7);
                    for(int n=0;n<5;n++)
                    {
                        float a=-1.05+n*.49;float2 d=float2(cos(a),sin(a)),normal=float2(-d.y,d.x);
                        float reach=lerp(.7,2.5,spread)*(1-n*.07);
                        float2 start=d*(.1+spread*.25),mid=d*reach*.58+normal*((n==1||n==3)?-.12:.15),end=d*reach;
                        rays+=(segmentGlow(p,start,mid,.018)+segmentGlow(p,mid,end,.011))*tail*(frame<8?.85:.13);
                    }
                }
                return float4(float3(.22,.6,1)*rays+float3(.6,.87,1)*glow+float3(.8,.97,1)*min(rays*.45,1),1)*i.tint;
            }
            ENDHLSL
        }
    }
}

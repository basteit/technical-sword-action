Shader "TechnicalSwordAction/EnemyForecastLight"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(UnityFlipSprite(input.positionOS, unity_SpriteProps.xy));
                output.uv = input.uv;
                output.color = input.color * unity_SpriteColor;
                return output;
            }
            bool IsCue(half4 pixel)
            {
                // #FFFA96 is the authored cue; the body uses blue and grey.
                return pixel.a > .5h && min(pixel.r, pixel.g) - pixel.b > .20h;
            }
            float4 frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (IsCue(texel)) return texel * input.color;
                // A one-texel dark gold edge makes the same flash readable over white tiles.
                float2 dx = float2(_MainTex_TexelSize.x, 0);
                float2 dy = float2(0, _MainTex_TexelSize.y);
                if (IsCue(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + dx)) ||
                    IsCue(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - dx)) ||
                    IsCue(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + dy)) ||
                    IsCue(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - dy)))
                    return half4(.18h, .07h, .015h, 1) * input.color;
                discard;
                return 0;
            }
            ENDHLSL
        }
    }
}

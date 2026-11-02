Shader "TechnicalSwordAction/ParryShockwave"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Parry Radial Refraction"
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _ShockwaveCenter;
            float4 _ShockwavePulse;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 center = _ShockwaveCenter.xy;
                // URP's blit already handles the render-target orientation. An
                // additional API-origin flip mirrors the wave away from contact.
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 delta = (input.texcoord - center) * float2(aspect, 1.0);
                float distance = length(delta);
                float t = saturate(_ShockwavePulse.x);
                float radius = lerp(.025, _ShockwavePulse.w, t);
                float width = _ShockwavePulse.z * lerp(.75, 1.5, t);
                float band = (distance - radius) / max(width, .001);
                float ring = sin(band * PI * 1.5) * exp(-band * band * 2.0);
                // No new flash, color inversion, zoom or blur. Leave the contact core stable.
                float amplitude = ring * (1.0 - t) * (1.0 - t) * smoothstep(.02, .075, distance);
                amplitude *= _ShockwavePulse.y * 22.0 / max(1.0, _ScreenParams.y);
                float2 offset = delta / max(distance, .0001) * amplitude / float2(aspect, 1.0);
                float2 uv = clamp(input.texcoord + offset, .5 * abs(_BlitTexture_TexelSize.xy),
                    1.0 - .5 * abs(_BlitTexture_TexelSize.xy));
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
            }
            ENDHLSL
        }
    }
}

Shader "TechnicalSwordAction/VideoParryInversion"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Monochrome Negative"
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,input.texcoord);
                // Invert perceptual brightness rather than linear light, so the
                // white training background turns black and dark outlines turn white.
                half3 displayColor=color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    displayColor=LinearToSRGB(displayColor);
                #endif
                half negative=1.0h-saturate(dot(displayColor,half3(.2126h,.7152h,.0722h)));
                half3 result=negative.xxx;
                #ifndef UNITY_COLORSPACE_GAMMA
                    result=SRGBToLinear(result);
                #endif
                return half4(result,color.a);
            }
            ENDHLSL
        }
    }
}

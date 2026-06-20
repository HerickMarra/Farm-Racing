Shader "FullScreen/BorderlandsOutline"
{
    Properties
    {
        _OutlineThickness("Outline Thickness", Float) = 1.5
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _DepthThreshold("Depth Threshold", Range(0, 1)) = 0.05
        _NormalThreshold("Normal Threshold", Range(0, 1)) = 0.4
    }

    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float _OutlineThickness;
    float4 _OutlineColor;
    float _DepthThreshold;
    float _NormalThreshold;

    float GetLinearDepth(uint2 pixelCoords)
    {
        float depth = CustomPassLoadCameraDepth(pixelCoords);
        return Linear01Depth(depth, _ZBufferParams);
    }

    float3 GetNormal(float2 uv)
    {
        return SampleSceneNormals(uv);
    }

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        uint2 pixelCoords = uint2(varyings.positionCS.xy);

        // Sample current camera color
        float4 sceneColor = float4(CustomPassLoadCameraColor(pixelCoords, 0), 1.0);

        // Skip drawing outline on skybox pixels
        float centerRawDepth = CustomPassLoadCameraDepth(pixelCoords);
        #if UNITY_REVERSED_Z
        if (centerRawDepth <= 0.0001) return sceneColor;
        #else
        if (centerRawDepth >= 0.9999) return sceneColor;
        #endif

        float centerDepth = Linear01Depth(centerRawDepth, _ZBufferParams);
        
        // Normalized screen UV for sampling normals
        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;

        float3 centerNormal = GetNormal(uv);

        float depthDiff = 0.0;
        float normalDiff = 0.0;

        // Apply thickness multiplier to Sobel kernel
        int thickness = max(1, (int)round(_OutlineThickness));
        
        int2 offsets[8] = {
            int2(-1,  1) * thickness,
            int2( 0,  1) * thickness,
            int2( 1,  1) * thickness,
            int2(-1,  0) * thickness,
            int2( 1,  0) * thickness,
            int2(-1, -1) * thickness,
            int2( 0, -1) * thickness,
            int2( 1, -1) * thickness
        };

        for (int i = 0; i < 8; i++)
        {
            uint2 sampleCoords = clamp(pixelCoords + offsets[i], uint2(0, 0), uint2(_ScreenSize.xy) - 1);
            
            // Depth difference
            float d = GetLinearDepth(sampleCoords);
            depthDiff += abs(centerDepth - d);

            // Normal difference (dot product comparison)
            float2 sampleUv = sampleCoords * _ScreenSize.zw;
            float3 n = GetNormal(sampleUv);
            normalDiff += 1.0 - saturate(dot(centerNormal, n));
        }

        // Apply thresholds to detect edges (linear scaling depth threshold by depth)
        bool isDepthEdge = depthDiff > (_DepthThreshold * centerDepth * 2.0);
        bool isNormalEdge = normalDiff > _NormalThreshold;

        if (isDepthEdge || isNormalEdge)
        {
            return _OutlineColor;
        }

        return sceneColor;
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Borderlands Outline Fullscreen Pass"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}

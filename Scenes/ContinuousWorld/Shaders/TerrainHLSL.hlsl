// Helper function: Accepts RAW types (we unwrap them before calling this)
float inverseLerp_custom(float a, float b, float value) {
    return saturate((value - a) / (b - a));
}

float3 triplanar_custom(float3 worldPos, float scale, float3 blendAxes, int textureIndex, Texture2DArray texArray, SamplerState ss) {
    float3 scaledWorldPos = worldPos / scale;

    float3 xProjection = SAMPLE_TEXTURE2D_ARRAY(texArray, ss, float2(scaledWorldPos.y, scaledWorldPos.z), textureIndex).rgb * blendAxes.x;
    float3 yProjection = SAMPLE_TEXTURE2D_ARRAY(texArray, ss, float2(scaledWorldPos.x, scaledWorldPos.z), textureIndex).rgb * blendAxes.y;
    float3 zProjection = SAMPLE_TEXTURE2D_ARRAY(texArray, ss, float2(scaledWorldPos.x, scaledWorldPos.y), textureIndex).rgb * blendAxes.z;

    return xProjection + yProjection + zProjection;
}

void CalculateTerrainLayers_float(
    float3 WorldPos,
    float3 WorldNormal,
    float MinHeight,
    float MaxHeight,
    float LayerCount,
    UnityTexture2DArray TextureArray, // Boxed Texture Array
    UnityTexture2D DataTexture,       // Boxed Texture 2D
    UnitySamplerState Sampler,        // Boxed Sampler
    out float3 FinalColor
) {
    float heightPercent = inverseLerp_custom(MinHeight, MaxHeight, WorldPos.y);
    float3 blendAxes = abs(WorldNormal);
    blendAxes /= (blendAxes.x + blendAxes.y + blendAxes.z);

    FinalColor = float3(0, 0, 0);
    float width = LayerCount;

    // UNPACKING: We get the raw resources once here to keep the loop clean
    Texture2DArray rawTextureArray = TextureArray.tex;
    Texture2D rawDataTexture = DataTexture.tex;
    SamplerState rawSampler = Sampler.samplerstate;

    for (int i = 0; i < LayerCount; i++) {
        float u = (i + 0.5) / width;

        // Use rawDataTexture and rawSampler
        float4 dataColor = SAMPLE_TEXTURE2D(rawDataTexture, rawSampler, float2(u, 0.25));
        float3 baseCol = dataColor.rgb;

        float4 dataSettings = SAMPLE_TEXTURE2D(rawDataTexture, rawSampler, float2(u, 0.75));

        float startHeight = dataSettings.r;
        float blendStrength = dataSettings.g;
        float textureScale = dataSettings.b;
        float tintStrength = dataSettings.a;

        float epsilon = 1E-4;
        float drawStrength = inverseLerp_custom(-blendStrength / 2 - epsilon, blendStrength / 2, heightPercent - startHeight);

        // Pass raw resources to the helper
        float3 textureColour = triplanar_custom(WorldPos, textureScale, blendAxes, i, rawTextureArray, rawSampler);

        float3 baseColourCalculated = baseCol * tintStrength;
        float3 finalTextureComp = textureColour * (1 - tintStrength);

        FinalColor = FinalColor * (1 - drawStrength) + (baseColourCalculated + finalTextureComp) * drawStrength;
    }
}
#ifndef MSDFGLYPH_FS
#define MSDFGLYPH_FS

#include "sh_Utils.h"
#include "sh_Masking.h"
#include "sh_TextureWrapping.h"

layout(location = 2) in mediump vec2 v_TexCoord;

layout(set = 0, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 0, binding = 1) uniform lowp sampler m_Sampler;

layout(location = 0) out vec4 o_Colour;

// distanceRange ("pxrange") passed to msdf-atlas-gen when generating the test atlas
// (see Textures/Msdf/msdf-test-atlas.json and TestSceneMsdfGlyph for the exact command).
// Hardcoded here for this proof of concept -- a real integration would source this from
// the atlas metadata instead.
#define MSDF_PX_RANGE 4.0

// Reconstructs the signed distance from a multi-channel signed distance field texel.
float medianOfThree(float r, float g, float b)
{
    return max(min(r, g), min(max(r, g), b));
}

// Converts the distance field's texel-space range into a screen-space pixel range,
// so the antialiasing width stays constant regardless of how much the glyph is scaled.
float screenPxRange(vec2 texCoord)
{
    vec2 unitRange = vec2(MSDF_PX_RANGE) / vec2(textureSize(sampler2D(m_Texture, m_Sampler), 0));
    vec2 screenTexSize = vec2(1.0) / fwidth(texCoord);
    return max(0.5 * dot(unitRange, screenTexSize), 1.0);
}

void main(void)
{
    vec2 wrappedCoord = wrap(v_TexCoord, v_TexRect);
    vec3 msd = texture(sampler2D(m_Texture, m_Sampler), wrappedCoord).rgb;

    float signedDistance = medianOfThree(msd.r, msd.g, msd.b) - 0.5;
    float coverage = clamp(signedDistance * screenPxRange(v_TexCoord) + 0.5, 0.0, 1.0);

    o_Colour = getRoundedColor(vec4(1.0, 1.0, 1.0, coverage), wrappedCoord);
}

#endif

namespace SharpText.Veldrid
{
    public static class Shaders
    {
        private static string SharedVertexBufferDeclaration = @"
layout(set = 0, binding = 0) uniform TextVertexPropertiesBuffer
{
    mat4 matrix4;
    vec4 rect;
};
";

        private static string SharedFragmentBufferDeclaration = @"
layout(set = 0, binding = 1) uniform TextFragmentPropertiesBuffer
{
    float thicknessAndMode;
    float _padding1;
    float _padding2;
    float _padding3;
    vec4 glyphColor;
};
";

        public static string GlyphTextVertex = @"
#version 450

precision highp float;

" + SharedVertexBufferDeclaration + @"

layout(location = 0) in vec3 position3;
layout(location = 1) in vec2 coord2;

layout(location = 0) out vec2 _coord2;

void main() {
    _coord2 = coord2;
    gl_Position = matrix4 * vec4(position3.xy, 0.0, 1.0);
}
";

        public static string GlyphTextFragment = @"
#version 450

precision highp float;

" + SharedFragmentBufferDeclaration + @"

layout(location = 0) in vec2 _coord2;

layout(location = 0) out vec4 outputColor;

void main() {
    // Calculate the shaded area of the quadratic curve
    if (_coord2.x * _coord2.x - _coord2.y > 0.0) {
        discard;
    }

    // Upper 4 bits: front faces
    // Lower 4 bits: back faces
    outputColor = glyphColor * (gl_FrontFacing ? 16.0 / 255.0 : 1.0 / 255.0);
}
";

        public static string TextVertex = @"
#version 450

precision highp float;

" + SharedVertexBufferDeclaration + @"

layout(location = 0) in vec2 position2;

layout(location = 0) out vec2 _coord2;

void main() {
    _coord2 = mix(rect.xy, rect.zw, position2 * 0.5 + 0.5);
    gl_Position = vec4((_coord2 * 2.0 - 1.0) * vec2(1, -1), 0.0, 1.0);
}
";

        public static string TextFragment = @"
#version 450

precision highp float;

" + SharedFragmentBufferDeclaration + @"

layout(set = 1, binding = 0) uniform texture2D textureView;
layout(set = 1, binding = 1) uniform sampler textureSampler;

layout(location = 0) in vec2 _coord2;

layout(location = 0) out vec4 outputColor;

layout(constant_id = 0) const bool FlipSamplerUVs = false;

void main() {
    vec2 texCoord = _coord2;
    if (FlipSamplerUVs)
    {
        texCoord.y *= -1;
    }

    // Get samples for -2/3 and -1/3
    vec2 valueL = texture(sampler2D(textureView, textureSampler), vec2(texCoord.x + dFdx(texCoord.x), texCoord.y)).yz * 255.0;
    vec2 lowerL = mod(valueL, 16.0);
    vec2 upperL = (valueL - lowerL) / 16.0;
    vec2 alphaL = min(abs(upperL - lowerL), 2.0);

    // Get samples for 0, +1/3, and +2/3
    vec3 valueR = texture(sampler2D(textureView, textureSampler), texCoord).xyz * 255.0;
    vec3 lowerR = mod(valueR, 16.0);
    vec3 upperR = (valueR - lowerR) / 16.0;
    vec3 alphaR = min(abs(upperR - lowerR), 2.0);

    // Average the energy over the pixels on either side
    vec4 rgba = vec4(
        (alphaR.x + alphaR.y + alphaR.z) / 6.0,
        (alphaL.y + alphaR.x + alphaR.y) / 6.0,
        (alphaL.x + alphaL.y + alphaR.x) / 6.0,
        0.0);

    // Optionally scale by a color
    outputColor = glyphColor.a == 0.0 ? 1.0 - rgba : glyphColor * rgba;
}
";
    }
}

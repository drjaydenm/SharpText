namespace Veldrid.TextRendering
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

        public static string SmoothTextVertex = @"
#version 450

precision highp float;

" + SharedVertexBufferDeclaration + @"

layout(location = 0) in vec4 position4;
layout(location = 1) in vec4 coord4;

layout(location = 0) out vec2 _coord2;
layout(location = 1) out vec4 _coord4;

void main() {
    _coord2 = position4.zw;
    _coord4 = coord4;
    gl_Position = vec4(matrix4 * vec3(position4.xy, 1.0), 0.0).xywz;
}
";

        public static string SmoothTextFragment = @"
#version 450

precision highp float;

" + SharedFragmentBufferDeclaration + @"

layout(location = 0) in vec2 _coord2;
layout(location = 1) in vec4 _coord4;

layout(location = 0) out vec4 outputColor;

void main() {
    outputColor = _coord4 * min(1.0, min(_coord2.x, _coord2.y));
}
";

        public static string GlyphTextVertex = @"
#version 450

precision highp float;

" + SharedVertexBufferDeclaration + @"

layout(location = 0) in vec3 position3;
layout(location = 1) in vec2 coord2;

layout(location = 0) out vec2 _coord2;

void main() {
    _coord2 = coord2.xy;
    gl_Position = matrix4 * vec4(position3.xy, 1.0, 1.0);
}
";

        public static string GlyphTextFragment = @"
#version 450

precision highp float;

" + SharedFragmentBufferDeclaration + @"

layout(location = 0) in vec2 _coord2;

layout(location = 0) out vec4 outputColor;

void main() {
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
    gl_Position = vec4(_coord2 * 2.0 - 1.0, 0.0, 1.0);
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

void main() {
    // Get samples for -2/3 and -1/3
    vec2 valueL = texture(sampler2D(textureView, textureSampler), vec2(_coord2.x + dFdx(_coord2.x), _coord2.y)).yz * 255.0;
    vec2 lowerL = mod(valueL, 16.0);
    vec2 upperL = (valueL - lowerL) / 16.0;
    vec2 alphaL = min(abs(upperL - lowerL), 2.0);

    // Get samples for 0, +1/3, and +2/3
    vec3 valueR = texture(sampler2D(textureView, textureSampler), _coord2).xyz * 255.0;
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

        public static string EquationTextVertex = @"
#version 450

precision highp float;

" + SharedVertexBufferDeclaration + @"

layout(location = 0) in vec2 position2;

layout(location = 0) out vec2 _coord2;

void main() {
    _coord2 = (matrix4 * vec4(position2, 0.0, 1.0)).xy;
    gl_Position = vec4(position2, 0.0, 1.0);
}
";

        public static string EquationTextFragment = @"
#version 450

precision highp float;

" + SharedFragmentBufferDeclaration + @"

layout(location = 0) in vec2 _coord2;

layout(location = 0) out vec4 outputColor;

bool eq(float x, float y) {
    return (abs(x - y)) < 0.00001;
}

void main() {
    float x = _coord2.x;
    float y = _coord2.y;
    float dx = dFdx(x);
    float dy = dFdy(y);
    bool xEqualsY = eq(x, y);
    float z = 0.0;
    if (xEqualsY) {
        z = 1.0;
    }

    // Evaluate all 4 adjacent +/- neighbor pixels
    vec2 z_neg = vec2(eq(x - dx, y), eq(x, y - dy));
    vec2 z_pos = vec2(eq(x + dx, y), eq(x, y + dy));

    // Compute the x and y slopes
    vec2 slope = (z_pos - z_neg) * 0.5;

    // Compute the gradient (the shortest point on the curve is assumed to lie in this direction)
    vec2 gradient = normalize(slope);

    // Use the parabola a* t^2 + b* t + z = 0 to approximate the function along the gradient
    float a = dot((z_neg + z_pos) * 0.5 - z, gradient * gradient);
    float b = dot(slope, gradient);

    // The distance to the curve is the closest solution to the parabolic equation
    float distanceToCurve = 0.0;
    float thickness = abs(thicknessAndMode);

    // Linear equation: b*t + z = 0
    if (abs(a) < 1.0e-6) {
        distanceToCurve = abs(z / b);
    }

    // Quadratic equation: a*t^2 + b*t + z = 0
    else {
        float discriminant = b * b - 4.0 * a * z;
        if (discriminant< 0.0) {
            distanceToCurve = thickness;
        } else {
            discriminant = sqrt(discriminant);
            distanceToCurve = min(abs(b + discriminant), abs(b - discriminant)) / abs(2.0 * a);
        }
    }

    // Antialias the edge using the distance from the curve
    float edgeAlpha = clamp(abs(thickness) - distanceToCurve, 0.0, 1.0);

    // Combine edge and area for color
    float outVal = 0.0;
    if (thicknessAndMode == 0.0) {
        outVal = clamp(0.5 + z / b, 0.0, 1.0) * 0.25;
    } else {
        if (thicknessAndMode < 0.0) {
            float mixVal = 0.0;
            if (z > 0.0) {
                mixVal = 0.25;
            }
            outVal = mix(edgeAlpha, 1.0, mixVal);
        } else {
            outVal = edgeAlpha;
        }
    }
    outputColor = glyphColor * outVal;
}
";

        public static string VertexShader = @"
#version 450

layout(location = 0) in vec2 Position;
layout(location = 1) in vec4 Color;

layout(location = 0) out vec4 fsin_Color;

void main()
{
    gl_Position = vec4(Position, 0, 1);
    fsin_Color = Color;
}
";

        public static string FragmentShader = @"
#version 450

layout(location = 0) in vec4 fsin_Color;
layout(location = 0) out vec4 fsout_Color;

void main()
{
    fsout_Color = fsin_Color;
}
";
    }
}

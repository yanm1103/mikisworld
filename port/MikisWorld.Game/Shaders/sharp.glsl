#version 330
// Sharp bilinear: the picture is bilinear-filtered, but only across one output pixel at the edge of each source pixel, so the
// pixels stay crisp at any zoom, including the non-integer ones (the idea is public domain, from the libretro "sharp-bilinear").
in vec4 TEX0;
out vec4 FragColor;
uniform sampler2D Texture;
uniform vec2 TextureSize;
uniform vec2 OutputSize;

void main()
{
    vec2 scale = max(OutputSize / TextureSize, vec2(1.0));
    vec2 texel = TEX0.xy * TextureSize;
    vec2 floored = floor(texel);
    vec2 centerDist = fract(texel) - 0.5;
    vec2 region = 0.5 - 0.5 / scale;
    vec2 f = (centerDist - clamp(centerDist, -region, region)) * scale + 0.5;
    FragColor = vec4(texture(Texture, (floored + f) / TextureSize).rgb, 1.0);
}

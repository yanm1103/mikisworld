#version 330
// Shared vertex stage: raylib's default attributes, the texture coordinate handed on as TEX0 (the libretro shaders expect that name).
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec4 vertexColor;
uniform mat4 mvp;
out vec4 TEX0;
void main()
{
    TEX0 = vec4(vertexTexCoord * TEXSCALE, 0.0, 0.0);
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}

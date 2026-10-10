#version 330
// Screen effect: a phone-LCD look. A dark gap between the pixels of the game, slightly lifted brightness to make up for it.
// Works on the finished window picture (game + text); GameRect is where the game sits in the window (x, y, w, h; y from the top).
in vec4 TEX0;
out vec4 FragColor;
uniform sampler2D Texture;
uniform vec2 Resolution;
uniform vec4 GameRect;
uniform vec2 GameSize;

void main()
{
    vec3 c = texture(Texture, TEX0.xy).rgb;
    vec2 p = vec2(gl_FragCoord.x, Resolution.y - gl_FragCoord.y);
    vec2 g = (p - GameRect.xy) / GameRect.zw * GameSize;                  // position in game pixels
    if (any(lessThan(g, vec2(0.0))) || any(greaterThanEqual(g, GameSize))) { FragColor = vec4(c, 1.0); return; }
    float ps = GameRect.z / GameSize.x;                                    // window pixels per game pixel
    vec2 d = min(fract(g), 1.0 - fract(g));                                // distance to the nearest pixel border
    float gap = clamp(1.25 / ps, 0.05, 0.22);
    vec2 m = smoothstep(vec2(0.0), vec2(gap), d);
    float strength = clamp((ps - 1.5) / 2.5, 0.0, 1.0);                    // no grid when the pixels are too small to show it
    c *= mix(1.0, mix(0.5, 1.0, m.x * m.y) * 1.12, strength);
    FragColor = vec4(c, 1.0);
}

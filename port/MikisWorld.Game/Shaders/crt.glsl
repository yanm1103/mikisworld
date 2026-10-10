#version 330
// Screen effect: a simple CRT: gentle barrel curvature, scanlines, an RGB phosphor mask, a little glow and a vignette.
// (Not CRT-Royale: that needs a dozen passes and lookup textures.)
in vec4 TEX0;
out vec4 FragColor;
uniform sampler2D Texture;
uniform vec2 Resolution;
uniform vec4 GameRect;
uniform vec2 GameSize;

vec3 scene(vec2 windowTop)
{
    return texture(Texture, vec2(windowTop.x, Resolution.y - windowTop.y) / Resolution).rgb;
}

void main()
{
    vec2 p = vec2(gl_FragCoord.x, Resolution.y - gl_FragCoord.y);
    vec2 uv = (p - GameRect.xy) / GameRect.zw;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) { FragColor = vec4(scene(p), 1.0); return; }

    vec2 cc = uv * 2.0 - 1.0;
    cc *= vec2(1.0 + 0.025 * cc.y * cc.y, 1.0 + 0.035 * cc.x * cc.x);       // barrel
    vec2 uv2 = cc * 0.5 + 0.5;
    if (any(lessThan(uv2, vec2(0.0))) || any(greaterThan(uv2, vec2(1.0)))) { FragColor = vec4(0.0, 0.0, 0.0, 1.0); return; }

    float ps = GameRect.z / GameSize.x;
    vec2 q = GameRect.xy + uv2 * GameRect.zw;
    vec3 c = scene(q);
    // glow: a few taps around the point, weighted
    vec2 o = vec2(ps * 0.6);
    vec3 glow = (scene(q + vec2(o.x, 0.0)) + scene(q - vec2(o.x, 0.0)) + scene(q + vec2(0.0, o.y)) + scene(q - vec2(0.0, o.y))) * 0.25;
    c = c + 0.18 * glow;

    float fx = clamp((ps - 1.5) / 2.5, 0.0, 1.0);                          // effects fade out when the pixels are too small
    float row = fract(uv2.y * GameSize.y) - 0.5;
    float scan = mix(1.0, 0.45 + 0.55 * exp(-row * row * 11.0), fx);
    int tri = int(mod(floor(p.x), 3.0));
    vec3 mask = vec3(0.72);
    mask[tri] = 1.0;
    c *= scan * mix(vec3(1.0), mask * 1.15, fx);
    vec2 v = uv2 * (1.0 - uv2);
    c *= pow(clamp(v.x * v.y * 16.0, 0.0, 1.0), 0.18) * 1.1;
    FragColor = vec4(c, 1.0);
}

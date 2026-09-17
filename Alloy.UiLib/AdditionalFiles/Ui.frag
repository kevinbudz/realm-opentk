#version 450 core

in VS_OUT {
    vec4 Position1;
    flat uint Color;
    flat uint Override;
    vec2 Info;
    vec2 UVCoords;
    vec4 Scissor;
    vec4 Extra1;
    vec4 Extra2;
    vec4 ColorTransform;
} inp;

out vec4 FragColor;

uniform sampler2D GameAtlasTexture;
uniform sampler2D UiAtlasTexture;
uniform sampler2D UiAtlasTextureLinear;
uniform sampler2D MinimapTexture;

uniform float PixelRange;
uniform vec2 TextTextureSize;
uniform sampler2D TextTexture;
// Reserved for the cached, whole-text filter renderer.
layout(binding = 14) uniform sampler2D TextBodyMask;
layout(binding = 15) uniform sampler2D TextShadowMask;

uniform sampler2D TitleBackgroundTexture;
uniform sampler2D TitleGraphicTexture;


const float TextTypeNormal = 0.0;
const float TextTypeSmall = 1.0;

const float IdColor = 0.0;
const float IdGameAtlas = 1.0;
const float IdUiAtlas = 2.0;
const float IdUiAtlasLinear = 3.0;
const float IdUiSlice = 4.0;
const float IdText = 5.0;
const float IdTitleBackground =6.0;
const float IdTitleGraphic = 7.0;
const float IdMinimap = 8.0;
const float IdEllipse = 9.0;

vec4 unpackColor(uint color) {
    return vec4(
    float(color & 0x000000FFu) / 255.0,
    float((color & 0x0000FF00u) >> 8u) / 255.0,
    float((color & 0x00FF0000u) >> 16u) / 255.0,
    float((color & 0xFF000000u) >> 24u) / 255.0
    );
}

float map(float value, float originalMin, float originalMax, float newMin, float newMax) {
    return (value - originalMin) / (originalMax - originalMin) * (newMax - newMin) + newMin;
}

float scale(float val, vec2 rect, float border, float borderTex) {
    if (val <= border)
    return map(val, 0, border, rect.x, rect.x + borderTex);
    if (val >= 1.0 - border)
    return map(val, 1.0 - border, 1, rect.y - borderTex, rect.y);
    return map(val, border, 1.0 - border, rect.x + borderTex, rect.y - borderTex);
}

vec4 slice() {
    vec2 uv;
    uv.x = scale(inp.UVCoords.x, inp.Extra1.xy, inp.Extra2.z, inp.Extra2.x);
    uv.y = scale(inp.UVCoords.y, inp.Extra1.zw, inp.Extra2.w, inp.Extra2.y);
    return texture(UiAtlasTexture, uv);
}

float median(float a, float b, float c) {
    return max(min(a, b), min(max(a, b), c));
}

float screenPxRange(vec2 uv) {
    vec2 unitRange = vec2(PixelRange, PixelRange) / TextTextureSize;
    vec2 screenSize = vec2(1.0, 1.0) / fwidth(uv);
    return max(0.5 * dot(unitRange, screenSize), 1.0);
}

vec2 SafeNormalize(vec2 v) {
    float vLength = length(v);

    vLength = (vLength > 0.0) ?
    1.0 / vLength : 0.0;

    return v * vLength;
}

float GetOpacityFromDistance(float signedDistance, vec2 Jdx, vec2 Jdy) {
    const float distanceLimit = sqrt(2.0f) / 2.0f;
    float thickness = 1.0f / (PixelRange / 2.0);

    vec2 gradientDistance = SafeNormalize(vec2(dFdx(signedDistance), dFdy(signedDistance)));
    vec2 gradient = vec2(gradientDistance.x * Jdx.x + gradientDistance.y * Jdy.x, gradientDistance.x * Jdx.y + gradientDistance.y * Jdy.y);
    // The anisotropic term narrows below a screen pixel when magnified; floor
    // it at the isotropic width so magnified edges keep a full pixel of AA.
    float isotropicWidth = 0.5 * length(vec2(dFdx(signedDistance), dFdy(signedDistance)));
    float scaledDistanceLimit = min(max(thickness * distanceLimit * length(gradient), isotropicWidth), 0.5f);

    return smoothstep(-scaledDistanceLimit, scaledDistanceLimit, signedDistance);
}

vec4 RenderText() {
    vec4 mtsdf = texture(TextTexture, inp.UVCoords);
    float dist = median(mtsdf.r, mtsdf.g, mtsdf.b) - 0.5;
    float pxRange = screenPxRange(inp.UVCoords);

    float bodyDist = dist * pxRange;
    float glowDist = mtsdf.a;
    float glowSize = inp.Extra1.x / PixelRange;
    float bodyAlpha;
    float glowAlpha;

    if (inp.Extra1.y == TextTypeSmall) {
        vec2 pixelCoord = inp.UVCoords * TextTextureSize;
        vec2 Jdx = dFdx(pixelCoord);
        vec2 Jdy = dFdy(pixelCoord);
        bodyAlpha = GetOpacityFromDistance(bodyDist, Jdx, Jdy);
        glowAlpha = GetOpacityFromDistance(glowDist, Jdx, Jdy) * glowSize;
    } else {
        bodyAlpha = clamp(bodyDist + 0.5f, 0.0f, 1.0f);
        glowAlpha = glowDist * glowSize;
    }

    if (inp.Extra2.w == 1.0) return vec4(bodyAlpha);

    vec4 color = mix(unpackColor(inp.Override), unpackColor(inp.Color), bodyAlpha);
    float alpha = bodyAlpha + glowAlpha;
    return vec4(color.rgb, alpha);
}

// Fractional box radius, repeated by the renderer for the requested quality.
vec4 BlurTextMask() {
    float radius = inp.Extra1.z;
    int reach = int(ceil(radius));
    float sum = 0.0;
    float weightSum = 0.0;
    for (int i = -reach; i <= reach; ++i) {
        float weight = clamp(radius + 1.0 - abs(float(i)), 0.0, 1.0);
        vec2 uv = inp.UVCoords + float(i) * inp.Extra1.xy;
        float value = 0.0;
        if (all(greaterThanEqual(uv, vec2(0))) && all(lessThanEqual(uv, vec2(1))))
            value = texture(TextShadowMask, uv).r;
        sum += weight * value;
        weightSum += weight;
    }
    return vec4(sum / weightSum);
}

vec4 FilteredText() {
    vec4 bodyColor = unpackColor(inp.Color);
    vec4 shadowColor = unpackColor(inp.Override);
    float body = texture(TextBodyMask, inp.UVCoords).r * bodyColor.a;
    vec2 uv = inp.UVCoords - inp.Extra1.xy;
    float shifted = 0.0;
    if (all(greaterThanEqual(uv, vec2(0))) && all(lessThanEqual(uv, vec2(1))))
        shifted = texture(TextShadowMask, uv).r * bodyColor.a;
    bool inner = inp.Extra2.x > 0.0;
    bool knockout = inp.Extra2.y > 0.0;
    bool hideObject = inp.Extra2.z > 0.0;
    float effect = clamp((inner ? 1.0 - shifted : shifted) * inp.Extra1.z * inp.Extra1.w, 0.0, 1.0);
    if (inner) effect *= body;
    else if (knockout) effect *= 1.0 - body;
    if (hideObject || knockout) return vec4(shadowColor.rgb, effect);
    // Compose in premultiplied space, then return straight alpha to the UI blend state.
    float alpha = inner ? body : body + effect * (1.0 - body);
    vec3 rgb = inner
        ? bodyColor.rgb * (body - effect) + shadowColor.rgb * effect
        : bodyColor.rgb * body + shadowColor.rgb * effect * (1.0 - body);
    return vec4(alpha > 0.0 ? rgb / alpha : vec3(0), alpha);
}

float samp(vec2 uv, vec2 dx, vec2 dy) {
    return textureGrad(GameAtlasTexture, uv, dx, dy).a;
}

vec4 RenderOutline() {
    vec2 uv = inp.UVCoords;
    vec2 dx = dFdx(uv);
    vec2 dy = dFdy(uv);
    vec4 color = textureGrad(GameAtlasTexture, uv, dx, dy);

    if (inp.UVCoords.y > inp.Extra1.x) {
        color.rgb -= 0.241 * (((inp.UVCoords.y - inp.Extra1.y) / inp.Extra1.z) - 0.4);
    }

    if (color.a > 0) {
        return color;
    }

    if (inp.Extra1.w == -1 && inp.Extra2.z == -1){ // Outline and glow disabled
        discard;
    }

    vec4 outlineColor = unpackColor(inp.Override);
    float scale = min(4, inp.Extra2.y / 60.0); // Extra2.y is the texture height 
    
    vec2 texSize = vec2(textureSize(GameAtlasTexture, 0));
    ivec2 currentTexel = ivec2(uv * texSize);

    float pxW = length(dx);
    float pxH = length(dy);
    float invPxW = 1.0 / pxW;
    float invPxH = 1.0 / pxH;
    vec2 invPx = vec2(1.0 / pxW, 1.0 / pxH);

    float outlineSize = floor(max(1, scale));
    float glowSize = max(6, 6.0 * scale);

    // Base directions (unit steps in screen space), scaled by i in the loop
    vec2 dirs[8] = vec2[](
    -dx - dy, -dy, dx - dy, dx,
    dx + dy,  dy, -dx + dy, -dx
    );

    float outlineAlpha = 0.0;
    float nearestDist = 999.0;

    for (float i = 1; i <= glowSize && outlineAlpha == 0.0; i++) {
        for (int j = 0; j < 8; j++) {
            vec2 sampleUV = uv + dirs[j] * i;
            ivec2 neighborTexel = ivec2(sampleUV * texSize);
            if (neighborTexel == currentTexel){
                continue;
            }

            if (texelFetch(GameAtlasTexture, neighborTexel, 0).a == 0){
                continue;
            }

            // Distance from fragment to nearest point on solid texel
            vec2 nearestPoint = clamp(uv, vec2(neighborTexel) / texSize, vec2(neighborTexel + ivec2(1)) / texSize);
            vec2 distPx = abs(uv - nearestPoint) * invPx;

            if (max(distPx.x, distPx.y) <= outlineSize) {
                outlineAlpha = 1.0;
                break;
            }

            nearestDist = min(nearestDist, length(distPx));
        }
    }

    if (inp.Extra1.w != -1 && outlineAlpha > 0.0){
        return vec4(outlineColor.rgb, 1.0);
    }

    if (inp.Extra2.z != -1 && nearestDist < 999.0) {
        float normalized = nearestDist / glowSize;
        float glowAlpha = 0.8 * exp(-normalized * 4) * (1.0 - smoothstep(0.8, 1.0, normalized));
        if (glowAlpha > 0.0){
            return vec4(outlineColor.rgb, glowAlpha);
        }
    }

    discard;
}

vec4 RenderNoOutline(sampler2D tex) {
    return texture(tex, inp.UVCoords);
}

vec4 RenderMinimap() {
    vec2 coords = inp.UVCoords;
    if (coords.x < 0 || coords.x > 1 || coords.y < 0 || coords.y > 1) {
        return vec4(0, 0, 0, 1);
    }

    return texture(MinimapTexture, coords);
}

vec4 RenderEllipse() {
    float rx = inp.Extra1.x - inp.Extra1.z, ry = inp.Extra1.y - inp.Extra1.z;
    float x = inp.UVCoords.x, y = inp.UVCoords.y;

    float inner = x * x / (rx * rx) + y * y / (ry * ry);
    rx = inp.Extra1.x; ry = inp.Extra1.y;
    float outline = x * x / (rx * rx) + y * y / (ry * ry);
    if (x * x / (rx * rx) + y * y / (ry * ry) > 1)
    return vec4(0, 0, 0, 0);
    float color_val;

    if (inner > 1) {
        color_val = 1;
    } else {
        color_val = 0;
    }

    return mix(unpackColor(inp.Color), unpackColor(inp.Override), color_val);
}

void main() {
    vec4 pixel = vec4(0);


    //TODO: replace pos1 with gl_FragCoord and send screen coords in scissor instead
    if (!(inp.Extra2.w == 1.0 && (inp.Info.x == IdText || inp.Info.x == 11.0)) && (inp.Position1.x < inp.Scissor.x || inp.Position1.x > inp.Scissor.z || inp.Position1.y < inp.Scissor.w || inp.Position1.y > inp.Scissor.y)) {
        discard;
    }

    vec4 color = unpackColor(inp.Color);

    float type = inp.Info.x;

    if (type == 10.0) {
        pixel = FilteredText();
    } else if (type == 11.0) {
        pixel = BlurTextMask();
    } else if (type == IdColor) {
        pixel = color;
    } else if (type == IdGameAtlas) {
        pixel = RenderOutline();
    } else if (type == IdUiAtlas) {
        pixel = RenderNoOutline(UiAtlasTexture);
    } else if (type == IdUiAtlasLinear) {
        pixel = RenderNoOutline(UiAtlasTextureLinear);// todo: msdfa sampling
    } else if (type == IdUiSlice) {
        pixel = slice();
    } else if (type == IdText) {
        pixel = RenderText();
    } else if (type == IdTitleBackground) {
        pixel = RenderNoOutline(TitleBackgroundTexture);
    } else if (type == IdTitleGraphic) {
        pixel = RenderNoOutline(TitleGraphicTexture);
    } else if (type == IdMinimap) {
        pixel = RenderMinimap();
    } else if (type == IdEllipse) {
        pixel = RenderEllipse();
    }

    if (color.a > 0 && type != IdColor && type != IdText && type != IdEllipse && type != 10.0 && type != 11.0)
    pixel *= color;

    vec4 add = floor(inp.ColorTransform / 1000.0);
    vec4 mult = inp.ColorTransform - add * 1000.0;

    pixel = clamp(pixel, vec4(0.0), vec4(1.0));

    pixel = mult * pixel;
    pixel += add / 255.0;

    pixel.a *= inp.Info.y;
    FragColor = pixel;
}
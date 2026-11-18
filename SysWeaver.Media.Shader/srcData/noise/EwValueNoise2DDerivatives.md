# EwValueNoise2DDerivatives

Noise: 2D value noise that also returns its analytic gradient (Inigo Quilez). x = value, yz = gradient.

## Signature

```glsl
vec3 EwValueNoise2DDerivatives(vec2 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position. Each unit is one lattice cell. |

## Returns

`vec3`: `x` = noise value in [0, 1] (same as [EwValueNoise2D](EwValueNoise2D.md)), `yz` = partial
derivatives d/dx and d/dy with respect to `p` (each within about [-1.5, 1.5]).

## Description

The bilinear blend is expanded into `a + k1*u.x + k2*u.y + k4*u.x*u.y` with `k1 = b - a`,
`k2 = c - a`, `k4 = a - b - c + d`. Differentiating with the chain rule gives
`du * (vec2(k1, k2) + k4 * u.yx)`, where `u = f*f*(3-2f)` and `du = 6f(1-f)`.

Typical uses: iq style eroded terrain fbm (`height += amplitude * n.x / (1.0 + dot(slope, slope))`
where `slope` accumulates the gradients), cheap normals for height fields without extra samples,
flow / curl directions (`vec2(n.z, -n.y)`).

The gradient is with respect to the input `p`: when calling with `p * scale`, multiply the
returned gradient by `scale`. Inside an fbm with a rotation matrix the accumulated derivative
must be transformed back as well if exact derivatives are needed (the classic eroded terrain
ignores this on purpose).

Quintic variant: replace the weights with `u = f*f*f*(f*(f*6.0-15.0)+10.0)` and
`du = 30.0*f*f*(f*(f-2.0)+1.0)`; the rest of the code is unchanged. This gives C2 continuity and
continuous derivatives (smoother lighting on terrain).

Cost: 4 hashes, about the same as plain value noise.

## Requires

- [EwHash2To1](../hash/EwHash2To1.md)

## Example

```glsl
// Eroded terrain fbm (iq)
float terrain(vec2 p)
{
    float height = 0.0;
    float amplitude = 0.5;
    vec2 slope = vec2(0.0);
    for (int i = 0; i < 6; i++)
    {
        vec3 n = EwValueNoise2DDerivatives(p);
        slope += n.yz;
        height += amplitude * n.x / (1.0 + dot(slope, slope));
        amplitude *= 0.5;
        p = mat2(1.6, 1.2, -1.2, 1.6) * p;
    }
    return height;
}
```

## Replacing local implementations

Local `noised` / `valueNoiseWithDerivatives` functions usually follow iq exactly. Check:
- the interpolation curve (hermite here; local quintic versions need the quintic weights above),
- the value range: iq's original returns `-1 + 2 * value` and `2 * gradient`; map with
  `vec3 n = EwValueNoise2DDerivatives(p); n = vec3(n.x * 2.0 - 1.0, n.yz * 2.0);`,
- the component order (some locals return `vec3(dx, dy, value)`).

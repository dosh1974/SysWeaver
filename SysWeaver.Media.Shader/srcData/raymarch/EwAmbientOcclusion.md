# EwAmbientOcclusion

Raymarch: SDF ambient occlusion from 5 samples along the normal (iq). Cheap contact darkening in
creases and near other geometry.

## Signature

```glsl
float EwAmbientOcclusion(vec3 p, vec3 n)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec3 | Surface position (ray hit point). |
| n | vec3 | Normalized surface normal at `p` (e.g. from [EwCalcNormal](EwCalcNormal.md)). |

## Returns

`float` ambient visibility in [0, 1]: 1 open, 0 fully occluded. Multiply ambient / sky light by it.

## Description

The scene SDF is called through the macro `EW_SDF(p)`, which the effect must define before pasting
the snippet (the snippet does not define it):

```glsl
#define EW_SDF(p) mapScene(p)      // mapScene returns a float distance
#define EW_SDF(p) mapScene(p).x    // mapScene returns vec2(distance, materialId) / vec4
```

For a flat open surface the distance at height `h` above it is exactly `h`. Nearby geometry makes
the SDF smaller; the shortfall `h - d` is summed over 5 heights (0.01, 0.04, 0.07, 0.10, 0.13) with
weights decaying by 0.95, then mapped with `1.0 - 3.0 * occlusion`. The sample heights are fixed in
scene units, so the effect radius is about 0.13 units: scale the scene (or keep a local copy) for
scenes of a very different size. 5 SDF evaluations; no noise, no randomness.

The result is often softened at the call site, e.g. `0.5 + 0.5 * ao` or `pow(ao, 0.5)`.

## Requires

- `EW_SDF(p)` macro, defined by the effect (scene distance).

## Example

```glsl
#define EW_SDF(p) mapScene(p).x
// ... paste EwCalcNormal and EwAmbientOcclusion here ...

vec3 n = EwCalcNormal(hitPosition, 0.001);
float ao = EwAmbientOcclusion(hitPosition, n);
color += skyColor * (0.5 + 0.5 * n.y) * ao;
```

## Replacing local implementations

Local AO functions vary in sample count, sample spacing, weight decay and final scale, so a
replacement changes the look (mostly the darkness and reach of the creases):

- iq's `calcAO` with `h = 0.01 + 0.12 * float(i) / 4.0`, `sca *= 0.95`, `clamp(1.0 - 3.0 * occ, 0.0,
  1.0)` is identical (some add `* (0.5 + 0.5 * n.y)`; apply that at the call site).
- Versions with other step sizes (e.g. `h = 0.02 + 0.06 * i`) or more samples reach further; versions
  with `sca *= 0.5` or `0.75` weight near samples more (sharper, smaller AO). Compensate partly with
  a `pow(ao, x)` at the call site, or keep the local version if the look matters.
- Versions returning occlusion instead of visibility (`occ` rather than `1 - k * occ`): use
  `1.0 - ao`.
- Versions using `1.0 / (1.0 + strength * occ)` or `exp(-k * occ)` have a softer response curve;
  close, but not identical.

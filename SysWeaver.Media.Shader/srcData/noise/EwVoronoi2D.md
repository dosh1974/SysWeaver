# EwVoronoi2D

Noise: 2D Voronoi / Worley cells. x = F1 distance, y = F2 distance, z = random value of the nearest cell.

## Signature

```glsl
vec3 EwVoronoi2D(vec2 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position. One cell (and one feature point) per unit square. |

## Returns

`vec3`:
- `x` = F1, Euclidean distance to the nearest feature point, in [0, ~1.1] (typically < 0.8),
- `y` = F2, distance to the second nearest feature point (>= F1),
- `z` = `EwHash2To1(id)` of the nearest cell, in [0, 1): constant over a cell, use it for per
  cell colours or random parameters.

## Description

Each integer cell gets a feature point at `cell + EwHash2To2(cell)`. The 3x3 neighbourhood
around the sample cell is searched for the two closest points (9 vec2 hashes + 1 float hash).
Since points can lie anywhere in their cell, the 3x3 search is exact for F1 and almost always
for F2 (a 5x5 search would be needed for a guaranteed F2 in rare configurations).

Common derived values:
- `F1` - "cells" / bubbles / caustic like gradients,
- `F2 - F1` - approximates the distance to the cell border (crackle, stone walls, cells with
  thick edges: `smoothstep(0.0, 0.05, v.y - v.x)`). For exact border distances use iq's
  two pass algorithm (dot with the bisector) instead.
- `z` - random cell id for colouring.

Animate the points by replacing `EwHash2To2(...)` with
`0.5 + 0.5 * sin(iTime + 6.2831 * EwHash2To2(...))` in a local copy.

## Requires

- [EwHash2To2](../hash/EwHash2To2.md)
- [EwHash2To1](../hash/EwHash2To1.md)

## Example

```glsl
vec3 v = EwVoronoi2D(uv * 6.0);
vec3 cellColor = 0.5 + 0.5 * cos(6.2831 * (v.z + vec3(0.0, 0.33, 0.67)));
float border = smoothstep(0.0, 0.06, v.y - v.x);
vec3 color = cellColor * border * (1.0 - 0.6 * v.x);
```

## Replacing local implementations

Local `voronoi` / `worley` / `cellular` functions often return only `F1` (use `.x`), squared
distances (`dot(r, r)` instead of `length`: use `v.x * v.x`), or `vec2(F1, cellId)`. Versions
returning the nearest point offset or the cell id vector are not covered; keep those local.
Manhattan / Chebyshev distance variants differ in shape. A different feature point hash changes
the cell layout but not the look. Animated versions need a local copy (see Description).

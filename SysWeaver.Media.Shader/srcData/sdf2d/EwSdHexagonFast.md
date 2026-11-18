# EwSdHexagonFast

SDF 2D: cheap hexagonal distance metric. Returns the hexagonal norm of `p`; the set of points with
value `r` is a regular hexagon with apothem `r`.

## Signature

```glsl
float EwSdHexagonFast(vec2 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position relative to the hexagon center (scale / rotate `p` before calling). |

## Returns

`float` >= 0, 0 at the center, `r` on a hexagon with apothem `r`. Use `EwSdHexagonFast(p) - r`
as a signed shape (negative inside).

## Description

Folds `p` into the first quadrant and takes the maximum of the projections onto two edge normals,
`(0.5, 0.8660254)` (60 degrees) and `(1, 0)`. The edges of the resulting hexagon:

- Orientation: vertical flat sides left and right (at `x = +-r`), pointed vertices at the top and
  bottom (at `y = +-r / cos(30) = +-1.1547 r`) - a "pointy top" hexagon. Swap `p.yx` for a flat top
  / bottom hexagon.
- `EwSdHexagonFast(p) - r` is the exact signed distance inside the hexagon and outside in the
  regions facing an edge. Outside near the vertices it is a metric, not the euclidean distance: the
  level sets stay hexagons instead of rounding off around the corners. That is fine for fills,
  borders, hex grids and glow falloff, but for raymarching or exact offsets use iq's exact
  `sdHexagon` (not provided as a snippet).

Extremely cheap (one abs, one dot, one max). Typical use: hex tiling, where the cell local
coordinate is fed in and the result compared against 0.5 (cell apothem).

## Requires

None.

## Example

```glsl
float hexValue = EwSdHexagonFast(cellLocal);
float border = smoothstep(0.45, 0.5, hexValue);
```

## Replacing local implementations

Local `hexDistance(p)` / `hexagonSDF(p)` / `hexDist(p)` with
`p = abs(p); return max(dot(p, vec2(0.5, 0.8660254)), p.x);` are identical. Variants written as
`max(p.x * 0.8660254 + p.y * 0.5, p.y)` are the rotated (flat top / bottom) hexagon: replace with
`EwSdHexagonFast(p.yx)`. Versions with `0.866` instead of `0.8660254` differ only in the 4th decimal.

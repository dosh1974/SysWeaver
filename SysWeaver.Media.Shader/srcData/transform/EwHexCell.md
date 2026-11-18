# EwHexCell

Transform: hexagonal tiling. Returns the position relative to the nearest hexagon center and a cell id.

## Signature

```glsl
vec4 EwHexCell(vec2 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | 2D position. Multiply by a scale to change the cell size (neighbour centers are 1.0 apart). |

## Returns

`vec4`: `xy` = position relative to the cell center, `zw` = cell id. The id steps in halves
(grid A ids are `n + 0.5`, grid B ids are integers); the cell center is `id * vec2(1.0, 1.7320508)` and
`p == xy + id * vec2(1.0, 1.7320508)`.

## Description

Pointy top hexagons: vertical edges at `x = +-0.5` (inradius 0.5), vertices at `y = +-0.57735`, neighbour
centers at distance 1 (left / right and at +-60 degrees). Two rectangular grids of period `(1, sqrt(3))`, offset by
half a period, are tested and the nearer center wins. Distance to the cell border (hexagon SDF):

```glsl
vec2 a = abs(h.xy);
float hexRadius = max(dot(a, vec2(0.5, 0.8660254)), a.x);   // 0.5 on the border
float edgeDistance = 0.5 - hexRadius;
```

For flat top hexagons, swap the input and output components (`EwHexCell(p.yx)`, then use `.yx` / `.wz`).
The ids are not integers: hash them directly (`EwHash2To1(h.zw)`) or use `floor(h.zw * 2.0)` for integer keys.

## Requires

None.

## Example

```glsl
vec4 h = EwHexCell(uv * 8.0);
vec2 a = abs(h.xy);
float edgeDistance = 0.5 - max(dot(a, vec2(0.5, 0.8660254)), a.x);
float cellRandom = EwHash2To1(h.zw);
vec3 col = vec3(cellRandom) * smoothstep(0.0, 0.03, edgeDistance);
```

## Replacing local implementations

- `floor(vec4(p, p - vec2(0.5, 1.0)) / s.xyxy) + 0.5` with `s = vec2(1.0, 1.7320508)` returning
  `vec4(local, hexCenters.xy)` / `vec4(local, hexCenters.zw + 0.5)`: identical.
- Versions returning the center position (`hc.xy * s`, e.g. `calcHexCellCoordinates`) instead of the id:
  use `h.zw * vec2(1.0, 1.7320508)`.
- The `mod` form (`cellA = mod(p, s) - 0.5 * s; cellB = mod(p - 0.5 * s, s) - 0.5 * s;` nearest wins,
  center `p - local`): same tiling, same local `xy` (up to ties exactly on borders), and the center
  `p - local == h.zw * vec2(1.0, 1.7320508)`. Hashes of that center keep the same pattern when hashing
  `h.zw * vec2(1.0, 1.7320508)`; hashing `h.zw` instead changes the random pattern.
- Different spacing (`vec2(1.7320508, 1.0)` = flat top, or a scaled grid): use the swizzle / scale described above.

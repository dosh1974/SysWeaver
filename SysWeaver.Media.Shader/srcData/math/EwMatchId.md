# EwMatchId

Math: `1.0` when two float ids are equal (within 0.5), else `0.0`.

## Signature

```glsl
float EwMatchId(float a, float b)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| a | float | First id (integer value stored in a float). |
| b | float | Second id. |

## Returns

`float`: `1.0` if `abs(a - b) < 0.5`, `0.0` otherwise (`1.0 - step(0.5, abs(a - b))`).

## Description

Branch free equality test for material / object ids that are passed around as floats (e.g. the `.y` of a
raymarch result). The 0.5 tolerance absorbs rounding from interpolation or arithmetic, so ids must be at
least 1.0 apart. Use the result as a mask: `col = mix(col, glassColor, EwMatchId(materialId, 3.0))`.

## Requires

None.

## Example

```glsl
float isFloor = EwMatchId(hit.y, 1.0);
albedo = mix(albedo, checkerColor, isFloor);
```

## Replacing local implementations

- `1.0 - step(0.5, abs(a - b))`: identical.
- `step(abs(a - b), 0.5)`: same except exactly at `abs(a - b) == 0.5` (1.0 there vs 0.0); for integer ids identical.
- `float(abs(a - b) < 0.5)` / `a == b ? 1.0 : 0.0` with integer valued ids: identical in practice.
- `abs(a - b) < 0.5` used in an `if`: keep the branch, or multiply by the mask.

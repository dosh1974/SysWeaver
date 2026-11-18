# EwLumaRec601

Color: luma with Rec.601 weights (0.299, 0.587, 0.114).

## Signature

```glsl
float EwLumaRec601(vec3 c)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| c | vec3 | Rgb color (traditionally gamma encoded). |

## Returns

`float` luma; 1.0 for white, 0.0 for black.

## Description

The classic NTSC / JPEG / "grayscale" weights. Gives more weight to red and blue than
[EwLuminance](EwLuminance.md) (Rec.709). Prefer EwLuminance for physically based work on linear
colors; use this one to keep the exact look of existing effects or for gamma space grayscale.

## Requires

None.

## Example

```glsl
vec3 grey = vec3(EwLumaRec601(col));
col = mix(col, grey, 0.5);
```

## Replacing local implementations

`dot(col, vec3(0.299, 0.587, 0.114))` maps 1:1. Rounded variants (`vec3(0.3, 0.59, 0.11)`) differ
by less than 0.002.

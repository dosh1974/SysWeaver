# EwTonemapReinhard

Color: Reinhard tone mapping `x / (1 + x)`.

## Signature

```glsl
vec3 EwTonemapReinhard(vec3 x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | vec3 | Linear HDR color (>= 0), exposure applied first. |

## Returns

`vec3` color in [0, 1), still linear.

## Description

Per channel Reinhard. Never clips, very cheap, but flattens and desaturates highlights (1.0 maps to
0.5, so the image gets darker unless exposure is raised). For more contrast use
[EwTonemapAces](EwTonemapAces.md). Encode the result with [EwLinearToSrgb](EwLinearToSrgb.md) or
[EwLinearToSrgbFast](EwLinearToSrgbFast.md).

## Requires

None.

## Example

```glsl
col = EwTonemapReinhard(col * 1.5);
```

## Replacing local implementations

`col / (1.0 + col)` and `col / (col + 1.0)` map 1:1. Luminance based Reinhard
(`col / (1.0 + EwLuminance(col))`) and the extended version with a white point
(`x * (1.0 + x / (w * w)) / (1.0 + x)`) are different curves; keep them local.

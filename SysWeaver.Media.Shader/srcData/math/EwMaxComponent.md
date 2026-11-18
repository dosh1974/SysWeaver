# EwMaxComponent

Math: largest component of a vec3 / vec4.

## Signature

```glsl
float EwMaxComponent(vec3 v)
float EwMaxComponent(vec4 v)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| v | vec3 / vec4 | Input vector. |

## Returns

`float`: the largest component (signed, no abs).

## Description

Typical uses: box SDF interior term (`min(EwMaxComponent(q), 0.0)`), color luminance-like peak for
normalization / tone mapping (`col / max(EwMaxComponent(col), 1.0)`), Chebyshev distance
(`EwMaxComponent(abs(p))`).

## Requires

None.

## Example

```glsl
vec3 q = abs(p) - boxSize;
float d = length(max(q, 0.0)) + min(EwMaxComponent(q), 0.0);
```

## Replacing local implementations

- `max(v.x, max(v.y, v.z))`, `max(max(v.x, v.y), v.z)`, `vmax(v)`, `maxComp(v)`: identical.
- Versions applying `abs` inside (`max(abs(v.x), ...)`): use `EwMaxComponent(abs(v))`.
- vec2 versions (`max(v.x, v.y)`): no overload, keep `max(v.x, v.y)`.

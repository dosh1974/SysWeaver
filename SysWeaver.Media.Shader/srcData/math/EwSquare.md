# EwSquare

Math: `x * x` (component wise for vectors).

## Signature

```glsl
float EwSquare(float x)
vec2  EwSquare(vec2 x)
vec3  EwSquare(vec3 x)
vec4  EwSquare(vec4 x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | float / vec2 / vec3 / vec4 | Input value. |

## Returns

Same type as `x`: `x * x` (>= 0).

## Description

Convenience for squaring an expression without evaluating it twice. Prefer it over `pow(x, 2.0)`: GLSL `pow`
is undefined for negative bases and compiles to `exp2(log2(x) * 2.0)` on many GPUs. For the squared length
of a vector use `dot(v, v)` instead.

## Requires

None.

## Example

```glsl
float falloff = EwSquare(1.0 - clamp(dist / radius, 0.0, 1.0));
```

## Replacing local implementations

- `sq(x)`, `sqr(x)`, `square(x)` returning `x * x`: identical.
- `pow(x, 2.0)`: identical for x >= 0 (and well defined for x < 0, where `pow` is undefined).
- Helpers named `sq` that return `dot(v, v)` for vectors (a float): not the same, use `dot(v, v)`.

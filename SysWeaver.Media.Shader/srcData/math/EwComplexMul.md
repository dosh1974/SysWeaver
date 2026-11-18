# EwComplexMul

Math: complex number multiplication (x = real, y = imaginary).

## Signature

```glsl
vec2 EwComplexMul(vec2 a, vec2 b)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| a | vec2 | First complex number. |
| b | vec2 | Second complex number. |

## Returns

`vec2` = `vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x)`.

## Description

Commutative. `EwComplexMul(z, z)` squares (Mandelbrot / Julia iteration `z = EwComplexMul(z, z) + c`).
Multiplying by `EwAngleToDir(a)` ([EwAngleToDir](EwAngleToDir.md)) rotates counter-clockwise by `a`, the same as
`EwRotate2D(a) * z` ([EwRotate2D](../transform/EwRotate2D.md)). Lengths multiply, angles add.

## Requires

None.

## Example

```glsl
vec2 z = vec2(0.0);
for (int i = 0; i < 64; i++)
{
    z = EwComplexMul(z, z) + c;
    if (dot(z, z) > 4.0) break;
}
```

## Replacing local implementations

- `cmul(a, b)` / `vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x)`: identical (any term order).
- `mat2(a, -a.y, a.x) * b`: identical.
- `vec2(z.x * z.x - z.y * z.y, 2.0 * z.x * z.y)` (square): identical to `EwComplexMul(z, z)`.
- Conjugate multiplications (`vec2(a.x * b.x + a.y * b.y, a.y * b.x - a.x * b.y)`) are not the same:
  use `EwComplexMul(a, vec2(b.x, -b.y))`.

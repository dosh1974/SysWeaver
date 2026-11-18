# EwConstants

Shared math constants: pi, tau, half pi, 1/pi and sqrt(3) as `#define`s.

## Signature

```glsl
#define EW_PI      3.14159265359
#define EW_TAU     6.28318530718
#define EW_HALF_PI 1.57079632679
#define EW_INV_PI  0.31830988618
#define EW_SQRT3   1.73205080757
```

## Parameters

None (preprocessor constants).

| Name | Type | Description |
| :--- | :--- | :--- |
| EW_PI | float literal | pi |
| EW_TAU | float literal | 2 * pi, one full turn in radians |
| EW_HALF_PI | float literal | pi / 2 |
| EW_INV_PI | float literal | 1 / pi |
| EW_SQRT3 | float literal | sqrt(3), e.g. for hexagonal / triangular grids |

## Returns

Nothing; the macros expand to float literals.

## Description

Defines (not `const float`) so they can be used anywhere, including other constant expressions
and array sizes computed from floats via `int(...)`. Precision is 11 decimals, more than a 32 bit
float can hold, so the values round to the nearest float.

Other snippets do not depend on this file: they use literal numbers so they can be pasted alone.

## Requires

None.

## Example

```glsl
float angle = fract(iTime * 0.1) * EW_TAU;
vec2 direction = vec2(cos(angle), sin(angle));
```

## Replacing local implementations

Effects commonly define their own constants, e.g. `#define PI 3.14159265`, `const float PI = 3.141592;`,
`TAU`, `TWO_PI`, `PI2`, `HALF_PI`, `PI_HALF`. These map directly:

- `PI` -> `EW_PI`
- `TAU`, `TWO_PI`, `PI2` -> `EW_TAU`
- `HALF_PI` -> `EW_HALF_PI`
- `1.0 / PI`, `INV_PI` -> `EW_INV_PI`
- `sqrt(3.0)`, `SQRT3` -> `EW_SQRT3`

Either replace every use, or keep the local name as an alias to avoid touching the effect body:

```glsl
#define PI EW_PI
#define TWO_PI EW_TAU
```

Local constants with fewer digits (e.g. `3.1416`) differ slightly; the result changes only by
float rounding.

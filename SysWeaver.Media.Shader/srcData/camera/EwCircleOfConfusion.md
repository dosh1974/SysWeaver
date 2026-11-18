# EwCircleOfConfusion

Camera: thin lens circle of confusion (blur size) for a depth.

## Signature

```glsl
float EwCircleOfConfusion(float depth, float focusDistance, float aperture)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| depth | float | Distance of the sample from the camera, must be > 0. |
| focusDistance | float | Distance in perfect focus. |
| aperture | float | Lens size / blur scale; the blur approached at infinite depth. |

## Returns

`float` >= 0: `aperture * abs(depth - focusDistance) / depth`, in the units of `aperture`
(typically screen units or pixels).

## Description

Simplified thin lens model (focal length folded into `aperture`). The blur is 0 at `focusDistance`, approaches
`aperture` for far samples and grows like `1 / depth` in front of the focus plane, so clamp the result
(`min(coc, maxBlur)`) and keep `depth` away from 0 (`max(depth, 0.05)`) when samples can be very close.
Used to size sprite / glint blur or to pick a blur radius per sample.

## Requires

None.

## Example

```glsl
float blurSize = min(EwCircleOfConfusion(max(sampleDepth, 0.05), 3.0, 0.02), 0.12);
float glint = smoothstep(radius + blurSize, radius - blurSize, distanceToGlint);
```

## Replacing local implementations

- `aperture * abs(depth - focus) / depth`: identical.
- `min(aperture * abs(depth - focus) / max(depth, eps), maxCoc)`:
  `min(EwCircleOfConfusion(max(depth, eps), focus, aperture), maxCoc)`.
- `aperture * abs(1.0 - focus / depth)` is the same formula (for depth > 0).
- Linear models `abs(depth - focus) * k` (no division by depth) are not equivalent; keep them.

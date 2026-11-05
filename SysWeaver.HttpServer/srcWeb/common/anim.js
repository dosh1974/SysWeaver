/**
 * @file Animation and interpolation helpers (easing, Hermite curves, smoothed values, springs, projectile / bounce
 * motion). Pure maths: no DOM access, no dependency on common.js or Sw (only `performance.now()` as a time fallback).
 *
 * Loaded as a classic script with `<script defer src="../common/anim.js">` by the media viewer pages of
 * SysWeaver.MicroService.Media (mediaView/MediaView.html, MediaPreview.html and the effect library pages); used by
 * mediaPlayer/media_player_effect.js (SmoothBool, SmoothValue, SpringAnimation, BounceAnimation, Smoothness).
 *
 * Globals (global lexical bindings, the public API, kept unchanged): `Smoothness`, `FalseValue`, `TrueValue` and the
 * classes `MathTools`, `CubicHermite`, `CubicHermiteInterpolator`, `SmoothValue`, `SmoothBool`, `SpringAnimation`,
 * `ProjectileMotion`, `BounceMotion`, `BounceAnimation`. The same objects are also available as `Sw.Anim.<Name>`.
 *
 * Conventions: arguments named `timeMs` are milliseconds; members that fall back to `performance.now()` for a
 * non-number `timeMs` say so. Physics helpers (ProjectileMotion / BounceMotion) use seconds.
 */

/**
 * Interpolation smoothness (frozen enum); the value indexes MathTools.GetSmoothDoubleFunction.
 * @type {Readonly<{Normal:number, Smooth:number, Smoother:number, Step:number}>}
 */
const Smoothness = Object.freeze({
    /** Linear interpolation (C0 continuity). */
    Normal: 0,
    /** "Smooth step" (C0 and C1 continuity). */
    Smooth: 1,
    /** Smoother step (C0, C1 and C2 continuity). */
    Smoother: 2,
    /** Hard step (no continuity). */
    Step: 3,
});

/**
 * Clamp a value to [0, 1].
 * @param {number} t - Value.
 * @returns {number} Clamped value.
 */
function animClamp01(t) {
    return t < 0 ? 0 : (t > 1 ? 1 : t);
}

/**
 * Static easing and smoothing functions on normalized values in [0, 1] (never instantiated).
 */
class MathTools {
    /**
     * Smooth step (ease in / out), C0 and C1 continuous.
     * @param {number} t - Input [0, 1] (clamped).
     * @returns {number} Smoothed value [0, 1].
     */
    static Smooth(t) {
        if (t <= 0)
            return 0;
        if (t >= 1)
            return 1;
        return t * t * (3 - t - t);
    }

    /**
     * Smoother step (ease in / out), C0, C1 and C2 continuous.
     * @param {number} t - Input [0, 1] (clamped).
     * @returns {number} Smoothed value [0, 1].
     */
    static Smoother(t) {
        if (t <= 0)
            return 0;
        if (t >= 1)
            return 1;
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    /**
     * Hard step: 0 below 0.5, else 1.
     * @param {number} t - Input [0, 1].
     * @returns {number} 0 or 1.
     */
    static Step(t) {
        return t < 0.5 ? 0 : 1;
    }

    /**
     * Ease in / out factor for an interval: rises from 0 to 1 during the first `easeDuration` of the normalized time
     * and falls back to 0 during the last `easeDuration` (Smooth curve).
     * @param {number} time - Normalized time [0, 1] (clamped).
     * @param {number} [easeDuration=0.1] - Ease fraction (capped at 1); <= 0 means no easing (returns 1).
     * @returns {number} Factor [0, 1].
     */
    static EaseSmooth(time, easeDuration) {
        return MathTools.#ease(MathTools.Smooth, time, easeDuration);
    }

    /**
     * Ease in / out factor with separate in / out fractions (Smooth curve). A fraction <= 0 disables that side.
     * @param {number} time - Normalized time [0, 1] (clamped).
     * @param {number} easeInDuration - Ease in fraction [0, 1].
     * @param {number} easeOutDuration - Ease out fraction [0, 1].
     * @returns {number} Factor [0, 1].
     */
    static EaseSmooth2(time, easeInDuration, easeOutDuration) {
        return MathTools.#ease2(MathTools.Smooth, time, easeInDuration, easeOutDuration);
    }

    /**
     * Like EaseSmooth with the Smoother curve.
     * @param {number} time - Normalized time [0, 1] (clamped).
     * @param {number} [easeDuration=0.1] - Ease fraction (capped at 1); <= 0 means no easing (returns 1).
     * @returns {number} Factor [0, 1].
     */
    static EaseSmoother(time, easeDuration) {
        return MathTools.#ease(MathTools.Smoother, time, easeDuration);
    }

    /**
     * Like EaseSmooth2 with the Smoother curve.
     * @param {number} time - Normalized time [0, 1] (clamped).
     * @param {number} easeInDuration - Ease in fraction [0, 1].
     * @param {number} easeOutDuration - Ease out fraction [0, 1].
     * @returns {number} Factor [0, 1].
     */
    static EaseSmoother2(time, easeInDuration, easeOutDuration) {
        return MathTools.#ease2(MathTools.Smoother, time, easeInDuration, easeOutDuration);
    }

    /**
     * The interpolation function of a smoothness.
     * @param {number} s - A Smoothness value.
     * @returns {function(number):number} Function [0, 1] -> [0, 1] (undefined for an unknown value).
     */
    static GetSmoothDoubleFunction(s) {
        return MathTools.#DoubleSmoothers[s];
    }

    /**
     * Symmetric ease (shared by EaseSmooth / EaseSmoother).
     * @param {function(number):number} curve - Curve.
     * @param {number} time - Normalized time.
     * @param {number} [easeDuration=0.1] - Ease fraction.
     * @returns {number} Factor.
     */
    static #ease(curve, time, easeDuration) {
        if (typeof easeDuration !== "number")
            easeDuration = 0.1;
        if (easeDuration <= 0)
            return 1;
        time = animClamp01(time);
        easeDuration = easeDuration > 1 ? 1 : easeDuration;
        const t0 = time / easeDuration;
        const t1 = 1 / easeDuration - t0;
        return curve(t0) * curve(t1);
    }

    /**
     * Asymmetric ease (shared by EaseSmooth2 / EaseSmoother2).
     * @param {function(number):number} curve - Curve.
     * @param {number} time - Normalized time.
     * @param {number} easeIn - Ease in fraction.
     * @param {number} easeOut - Ease out fraction.
     * @returns {number} Factor.
     */
    static #ease2(curve, time, easeIn, easeOut) {
        time = animClamp01(time);
        easeIn = easeIn > 1 ? 1 : easeIn;
        easeOut = easeOut > 1 ? 1 : easeOut;
        const t0 = easeIn > 0 ? (time / easeIn) : 1;
        const t1 = easeOut > 0 ? ((1 - time) / easeOut) : 1;
        return curve(t0) * curve(t1);
    }

    /** Smoothers indexed by Smoothness: linear, Smooth, Smoother, Step. @type {Array<function(number):number>} */
    static #DoubleSmoothers = [
        x => x,
        MathTools.Smooth,
        MathTools.Smoother,
        MathTools.Step,
    ];
}

/**
 * Cubic Hermite basis weights and their derivatives (never instantiated).
 */
class CubicHermite {
    /**
     * Hermite basis weights: fromW = (1+2t)(1-t)^2, initW = t(1-t)^2, toW = t^2(3-2t), exitW = t^2(t-1). t is not clamped.
     * @param {number} t - Time [0, 1].
     * @returns {{fromW:number, initW:number, toW:number, exitW:number}} Weights.
     */
    static CalcBasis(t) {
        const t2 = t * t;
        const it = 1 - t;
        const td = t + t;
        const it2 = it * it;
        return { fromW: (1 + td) * it2, initW: t * it2, toW: t2 * (3 - td), exitW: t2 * (t - 1) };
    }

    /**
     * Derivatives of the basis weights: fromW = 6(t^2-t), initW = 3t^2-4t+1, toW = 6(t-t^2), exitW = 3t^2-2t.
     * @param {number} t - Time [0, 1].
     * @returns {{fromW:number, initW:number, toW:number, exitW:number}} Weights.
     */
    static CalcDerivativeBasis(t) {
        const t2 = t * t;
        const t2_3 = t2 * 3;
        return { fromW: 6 * (t2 - t), initW: t2_3 - 4 * t + 1, toW: 6 * (t - t2), exitW: t2_3 - 2 * t };
    }
}

/**
 * An immutable cubic Hermite segment from `From` (tangent `Init`) to `To` (tangent `Exit`), evaluated at a normalized
 * segment time t in [0, 1]. Copy with `new CubicHermiteInterpolator(other)`.
 */
class CubicHermiteInterpolator {
    /** @type {number} Start value. */
    #From;
    /** @type {number} Start tangent. */
    #Init;
    /** @type {number} End (target) value. */
    #To;
    /** @type {number} End tangent. */
    #Exit;

    /**
     * @param {number|CubicHermiteInterpolator} from - Start value, or an interpolator to copy (other args ignored).
     * @param {number} to - Target value.
     * @param {number} [init=0] - Start tangent.
     * @param {number} [exit=0] - End tangent.
     */
    constructor(from, to, init, exit) {
        if ((typeof from === "object") && from) {
            this.#From = from.#From;
            this.#Init = from.#Init;
            this.#To = from.#To;
            this.#Exit = from.#Exit;
            return;
        }
        this.#From = from;
        this.#To = to;
        this.#Init = typeof init === "number" ? init : 0;
        this.#Exit = typeof exit === "number" ? exit : 0;
    }

    /** @returns {number} The target value. */
    get Target() {
        return this.#To;
    }

    /**
     * The value at a segment time (not clamped).
     * @param {number} t - Segment time [0, 1].
     * @returns {number} Value.
     */
    ValueAt(t) {
        return this.#weigh(CubicHermite.CalcBasis(t));
    }

    /**
     * The derivative with respect to the segment time (not clamped). The name (without the "e") is kept for
     * compatibility; `DerivativeAt` is an alias.
     * @param {number} t - Segment time [0, 1].
     * @returns {number} Derivative.
     */
    DerivativAt(t) {
        return this.#weigh(CubicHermite.CalcDerivativeBasis(t));
    }

    /**
     * Alias of DerivativAt.
     * @param {number} t - Segment time [0, 1].
     * @returns {number} Derivative.
     */
    DerivativeAt(t) {
        return this.DerivativAt(t);
    }

    /**
     * Weighted sum of the control values.
     * @param {{fromW:number, initW:number, toW:number, exitW:number}} c - Weights.
     * @returns {number} Sum.
     */
    #weigh(c) {
        return this.#From * c.fromW + this.#Init * c.initW + this.#To * c.toW + this.#Exit * c.exitW;
    }
}

/**
 * A scalar that glides C1 continuously toward new targets: every Update with a new target (or duration) starts a new
 * Hermite segment from the current value, carrying over the current derivative. Copy with `new SmoothValue({}, other)`.
 */
class SmoothValue {
    /** @type {number} Start time (ms) of the current segment. */
    #Time = 0;
    /** @type {CubicHermiteInterpolator} Current segment. */
    #Hermite;
    /** @type {number} Duration (ms) of the current segment; 0 = stationary at the segment start. */
    #AdaptionTime = 0;
    /** @type {number} Default duration (ms) of changes. */
    #FixedTime = 0;

    /**
     * @param {number|object} [timeMs=performance.now()] - Time stamp; a non-null object selects the copy form, copying
     * the SmoothValue passed as the second argument.
     * @param {number|SmoothValue} [value=0] - Start (and target) value, or the SmoothValue to copy.
     * @param {number} [duration=1000] - Default duration (ms) of changes.
     * @param {number} [init=0] - Initial derivative.
     * @param {number} [from] - If set the value starts here and `value` is the target (moves on the first Update).
     */
    constructor(timeMs, value, duration, init, from) {
        if ((typeof timeMs === "object") && timeMs) {
            this.#Hermite = new CubicHermiteInterpolator(value.#Hermite);
            this.#Time = value.#Time;
            this.#AdaptionTime = value.#AdaptionTime;
            this.#FixedTime = value.#FixedTime;
            return;
        }
        if (typeof value !== "number")
            value = 0;
        this.#Time = SmoothValue.#now(timeMs);
        this.#Hermite = new CubicHermiteInterpolator(typeof from === "number" ? from : value, value, typeof init === "number" ? init : 0);
        this.#FixedTime = typeof duration === "number" ? duration : 1000;
    }

    /**
     * A time stamp or performance.now().
     * @param {*} timeMs - Time stamp.
     * @returns {number} ms.
     */
    static #now(timeMs) {
        return typeof timeMs === "number" ? timeMs : performance.now();
    }

    /**
     * Normalized time of the current segment at a time stamp, clamped to [0, 1].
     * @param {number} timeMs - Time stamp.
     * @returns {number} Segment time.
     */
    #segmentTime(timeMs) {
        const elapsed = Math.max(0, timeMs - this.#Time);
        const a = this.#AdaptionTime;
        const dt = a > 0 ? (elapsed / a) : 0;
        return dt > 1 ? 1 : dt;
    }

    /**
     * Set a new target. If the target or duration differs from the current segment a new segment starts at timeMs
     * from the current value, with the current derivative as start tangent; `duration <= 0` jumps.
     * @param {number} [timeMs=performance.now()] - Time stamp.
     * @param {number} value - New target.
     * @param {number} [duration] - Duration (ms), default the constructor's.
     * @param {number} [exitVelocity=0] - End tangent.
     * @returns {number} The value at timeMs (evaluated before the retarget).
     */
    Update(timeMs, value, duration, exitVelocity) {
        timeMs = SmoothValue.#now(timeMs);
        if (typeof exitVelocity !== "number")
            exitVelocity = 0;
        if (typeof duration !== "number")
            duration = this.#FixedTime;
        const dt = this.#segmentTime(timeMs);
        const h = this.#Hermite;
        const tv = duration <= 0 ? value : h.ValueAt(dt);
        if ((value != h.Target) || (this.#AdaptionTime != duration)) {
            this.#Time = timeMs;
            this.#Hermite = new CubicHermiteInterpolator(tv, value, h.DerivativAt(dt), exitVelocity);
            this.#AdaptionTime = duration;
        }
        return tv;
    }

    /** @returns {number} The target value (where the value ends up). */
    get Target() {
        return this.#Hermite.Target;
    }

    /**
     * The value at a time (holds at the target after the segment ended).
     * @param {number} [timeMs=performance.now()] - Time stamp.
     * @returns {number} Value.
     */
    ValueAt(timeMs) {
        return this.#Hermite.ValueAt(this.#segmentTime(SmoothValue.#now(timeMs)));
    }

    /**
     * The derivative (with respect to the normalized segment time) at a time.
     * @param {number} [timeMs=performance.now()] - Time stamp.
     * @returns {number} Derivative.
     */
    DerivativeAt(timeMs) {
        return this.#Hermite.DerivativAt(this.#segmentTime(SmoothValue.#now(timeMs)));
    }

    /**
     * Set the value immediately, removing any target (breaks continuity).
     * @param {number} [timeMs=performance.now()] - Time stamp.
     * @param {number} value - Value.
     * @param {number} [init=0] - Tangent.
     * @returns {number} The value.
     */
    SetTo(timeMs, value, init) {
        if (typeof init !== "number")
            init = 0;
        this.#Hermite = new CubicHermiteInterpolator(value, value, init, init);
        this.#Time = SmoothValue.#now(timeMs);
        this.#AdaptionTime = 0;
        return value;
    }
}

/** @type {number} Internal value of SmoothBool false (slightly below 0, so the clamped output reaches 0). */
const FalseValue = -0.0001;

/** @type {number} Internal value of SmoothBool true (slightly above 1, so the clamped output reaches 1). */
const TrueValue = 1.0001;

/**
 * A boolean that changes smoothly: a SmoothValue between FalseValue and TrueValue, clamped to [0, 1] and shaped with
 * a Smoothness function (0 = false, 1 = true).
 */
class SmoothBool {
    /** @type {function(number):number} Shaping function. */
    #Smooth;
    /** @type {SmoothValue} The underlying value. */
    #Spline;
    /** @type {boolean} The current target. */
    #InternalTarget;

    /** @returns {boolean} The target state. */
    get Target() {
        return this.#InternalTarget;
    }

    /**
     * @param {number} [timeMs=performance.now()] - Time stamp.
     * @param {boolean} [startState=false] - Start state.
     * @param {number} [duration=1000] - Default duration (ms) of changes.
     * @param {number} [smoothness=Smoothness.Normal] - A Smoothness value.
     */
    constructor(timeMs, startState, duration, smoothness) {
        this.#Spline = new SmoothValue(timeMs, startState ? TrueValue : FalseValue, duration);
        this.#Smooth = MathTools.GetSmoothDoubleFunction(typeof smoothness === "number" ? smoothness : Smoothness.Normal);
        this.#InternalTarget = startState;
    }

    /**
     * Clamp and shape an internal value.
     * @param {number} v - Value.
     * @returns {number} [0, 1].
     */
    #shape(v) {
        if (v <= 0)
            return 0;
        if (v >= 1)
            return 1;
        return this.#Smooth(v);
    }

    /**
     * The value [0, 1] at a time.
     * @param {number} [timeMs=performance.now()] - Time stamp.
     * @returns {number} 0 (false) .. 1 (true).
     */
    ValueAt(timeMs) {
        return this.#shape(this.#Spline.ValueAt(timeMs));
    }

    /**
     * Set a new target state.
     * @param {number} [timeMs=performance.now()] - Time stamp.
     * @param {boolean} state - Target.
     * @param {number} [duration] - Duration (ms), default the constructor's.
     * @returns {number} The value [0, 1] at timeMs.
     */
    Update(timeMs, state, duration) {
        const v = this.#Spline.Update(timeMs, state ? TrueValue : FalseValue, duration);
        this.#InternalTarget = state;
        return this.#shape(v);
    }

    /**
     * Set the state immediately (no transition).
     * @param {number} [timeMs=performance.now()] - Time stamp.
     * @param {boolean} state - State.
     * @returns {void}
     */
    SetTo(timeMs, state) {
        this.#Spline.SetTo(timeMs, state ? TrueValue : FalseValue);
        this.#InternalTarget = state;
    }
}

/**
 * A "springy" animation: a damped cosine `initialLength * cos(omega t) / e^(decayRate t)` of the elapsed time.
 * `SpringAnimation.Zoom` is a shared preset.
 */
class SpringAnimation {
    /** @type {number} Angular frequency (rad/s). */
    #Frequency;
    /** @type {number} Amplitude. */
    #InitialLength;
    /** @type {number} Exponential decay rate per second. */
    #Decay;
    /** @type {number} Time (s) after which the displacement is 0. */
    #EndTime;

    /** @type {SpringAnimation} The shared zoom preset (3, 1, 500, 0.001). */
    static #InternalZoom = new SpringAnimation(3, 1, 500, 0.001);

    /** @returns {SpringAnimation} A "default" springy zoom animation (shared instance). */
    static get Zoom() {
        return SpringAnimation.#InternalZoom;
    }

    /**
     * @param {number} [frequency=7] - Oscillation frequency (cycles per second).
     * @param {number} [initialLength=100] - Amplitude at start.
     * @param {number} [decay=90] - Decay in percent per second (rate decay / 100).
     * @param {number} [snap=0.01] - Stop when the envelope falls below this length (<= 0: run 1000 s).
     */
    constructor(frequency = 7, initialLength = 100, decay = 90, snap = 0.01) {
        this.#Frequency = frequency * Math.PI * 2;
        this.#InitialLength = initialLength;
        this.#Decay = decay * 0.01;
        this.#EndTime = snap > 0 ? Math.log(initialLength / snap) / this.#Decay : 1000;
    }

    /**
     * The displacement at an elapsed time (0 after the end time).
     * @param {number} [timeMs=performance.now()] - Elapsed time (ms) since the animation started.
     * @returns {number} Displacement.
     */
    Displacement(timeMs) {
        if (typeof timeMs !== "number")
            timeMs = performance.now();
        let t = timeMs * 0.001;
        if (t > this.#EndTime)
            return 0;
        if (t < 0)
            t = 0;
        return this.#InitialLength * Math.cos(this.#Frequency * t) / Math.exp(this.#Decay * t);
    }

    /**
     * Alias of Displacement.
     * @param {number} [timeMs=performance.now()] - Elapsed time (ms).
     * @returns {number} Displacement.
     */
    ValueAt(timeMs) {
        return this.Displacement(timeMs);
    }
}

/**
 * Formulas of a projectile shot straight up from height 0 (never instantiated).
 */
class ProjectileMotion {
    /**
     * Time of flight (ground to ground).
     * @param {number} initialVelocity - Upwards velocity (m/s).
     * @param {number} gravity - Gravity (m/s2, downwards).
     * @returns {number} Seconds.
     */
    static FlightTime(initialVelocity, gravity) {
        return (initialVelocity * 2) / gravity;
    }

    /**
     * Maximum height.
     * @param {number} initialVelocity - Upwards velocity (m/s).
     * @param {number} gravity - Gravity (m/s2).
     * @returns {number} Meters.
     */
    static MaxHeight(initialVelocity, gravity) {
        return initialVelocity * initialVelocity * 0.5 / Math.abs(gravity);
    }

    /**
     * Height at a time (below 0 after the flight time).
     * @param {number} initialVelocity - Upwards velocity (m/s).
     * @param {number} gravity - Gravity (m/s2).
     * @param {number} time - Seconds.
     * @returns {number} Meters.
     */
    static Displacement(initialVelocity, gravity, time) {
        return (initialVelocity - gravity * 0.5 * time) * time;
    }

    /**
     * Vertical velocity at a time.
     * @param {number} initialVelocity - Upwards velocity (m/s).
     * @param {number} gravity - Gravity (m/s2).
     * @param {number} time - Seconds.
     * @returns {number} m/s.
     */
    static Velocity(initialVelocity, gravity, time) {
        return initialVelocity - gravity * time;
    }

    /**
     * Initial velocity and gravity giving a max height and flight time (v = 4h/T, g = 8h/T^2).
     * @param {number} maxHeight - Meters.
     * @param {number} flightTime - Seconds.
     * @returns {{initialVelocity:number, gravity:number}} Parameters.
     */
    static Solve(maxHeight, flightTime) {
        return { initialVelocity: maxHeight * 4 / flightTime, gravity: maxHeight * 8 / (flightTime * flightTime) };
    }
}

/**
 * Formulas of a bouncing projectile whose launch velocity is multiplied by `restitution` at each bounce: period p
 * starts with r^p * v and lasts r^p * T (T = first flight time). Never instantiated.
 */
class BounceMotion {
    /**
     * The period (bounce index) a time belongs to.
     * @param {number} initialVelocity - Unused (kept for the signature).
     * @param {number} gravity - Unused (kept for the signature).
     * @param {number} restitution - (0, 1] velocity factor per bounce.
     * @param {number} flightTime - First flight time (s).
     * @param {number} time - Seconds.
     * @returns {number} Period index (integer >= 0).
     */
    static Period(initialVelocity, gravity, restitution, flightTime, time) {
        const t = time / flightTime;
        const p = Math.log(Math.max(0.000000001, (restitution - 1) * t + 1)) / Math.log(restitution);
        return Math.floor(Math.max(0, p));
    }

    /**
     * The period after which the motion is at rest.
     * @param {number} initialVelocity - m/s.
     * @param {number} restitution - (0, 1].
     * @param {number} restVelocity - Velocity considered at rest (m/s).
     * @returns {number} Period index.
     */
    static RestAfterPeriod(initialVelocity, restitution, restVelocity) {
        const p = Math.log(restVelocity / initialVelocity) / Math.log(restitution);
        return Math.floor(Math.max(0, p));
    }

    /**
     * The time when the motion is at rest.
     * @param {number} initialVelocity - m/s.
     * @param {number} restitution - (0, 1].
     * @param {number} restVelocity - m/s.
     * @param {number} flightTime - First flight time (s).
     * @returns {number} Seconds.
     */
    static RestTime(initialVelocity, restitution, restVelocity, flightTime) {
        const p = BounceMotion.RestAfterPeriod(initialVelocity, restitution, restVelocity);
        return BounceMotion.PeriodEndTime(restitution, p) * flightTime;
    }

    /**
     * Launch velocity of a period (r^period * v).
     * @param {number} initialVelocity - m/s.
     * @param {number} restitution - (0, 1].
     * @param {number} period - Period index.
     * @returns {number} m/s.
     */
    static PeriodVelocity(initialVelocity, restitution, period) {
        return Math.pow(restitution, period) * initialVelocity;
    }

    /**
     * Duration of a period as a fraction of the first flight time (r^period).
     * @param {number} restitution - (0, 1].
     * @param {number} period - Period index.
     * @returns {number} Fraction.
     */
    static PeriodDuration(restitution, period) {
        return Math.pow(restitution, period);
    }

    /**
     * Normalized end time of a period: 1 + r(r^period - 1)/(r - 1) (multiply by the first flight time).
     * @param {number} restitution - (0, 1).
     * @param {number} period - Period index.
     * @returns {number} Normalized time.
     */
    static PeriodEndTime(restitution, period) {
        return 1.0 + (restitution * (Math.pow(restitution, period) - 1)) / (restitution - 1);
    }

    /**
     * Height at a time (computes the first flight time).
     * @param {number} initialVelocity - m/s.
     * @param {number} gravity - m/s2.
     * @param {number} restitution - (0, 1].
     * @param {number} time - Seconds.
     * @returns {number} Meters.
     */
    static Displacement(initialVelocity, gravity, restitution, time) {
        return BounceMotion.Displacement2(initialVelocity, gravity, restitution, ProjectileMotion.FlightTime(initialVelocity, gravity), time);
    }

    /**
     * Height at a time with a precomputed first flight time.
     * @param {number} initialVelocity - m/s.
     * @param {number} gravity - m/s2.
     * @param {number} restitution - (0, 1].
     * @param {number} flightTime - First flight time (s).
     * @param {number} time - Seconds.
     * @returns {number} Meters.
     */
    static Displacement2(initialVelocity, gravity, restitution, flightTime, time) {
        const s = BounceMotion.#inPeriod(initialVelocity, gravity, restitution, flightTime, time);
        return ProjectileMotion.Displacement(s.v, gravity, s.dt);
    }

    /**
     * Velocity at a time (computes the first flight time).
     * @param {number} initialVelocity - m/s.
     * @param {number} gravity - m/s2.
     * @param {number} restitution - (0, 1].
     * @param {number} time - Seconds.
     * @returns {number} m/s.
     */
    static Velocity(initialVelocity, gravity, restitution, time) {
        return BounceMotion.Velocity2(initialVelocity, gravity, restitution, ProjectileMotion.FlightTime(initialVelocity, gravity), time);
    }

    /**
     * Velocity at a time with a precomputed first flight time.
     * @param {number} initialVelocity - m/s.
     * @param {number} gravity - m/s2.
     * @param {number} restitution - (0, 1].
     * @param {number} flightTime - First flight time (s).
     * @param {number} time - Seconds.
     * @returns {number} m/s.
     */
    static Velocity2(initialVelocity, gravity, restitution, flightTime, time) {
        const s = BounceMotion.#inPeriod(initialVelocity, gravity, restitution, flightTime, time);
        return ProjectileMotion.Velocity(s.v, gravity, s.dt);
    }

    /**
     * The launch velocity of the period of a time and the time inside that period.
     * @param {number} initialVelocity - m/s.
     * @param {number} gravity - m/s2.
     * @param {number} restitution - (0, 1].
     * @param {number} flightTime - First flight time (s).
     * @param {number} time - Seconds.
     * @returns {{v:number, dt:number}} Velocity and time in the period.
     */
    static #inPeriod(initialVelocity, gravity, restitution, flightTime, time) {
        const p = BounceMotion.Period(initialVelocity, gravity, restitution, flightTime, time);
        const t1 = BounceMotion.PeriodEndTime(restitution, p) * flightTime;
        const tm = BounceMotion.PeriodDuration(restitution, p) * flightTime;
        return {
            v: BounceMotion.PeriodVelocity(initialVelocity, restitution, p),
            dt: Math.max(0, Math.min(tm, time - (t1 - tm))),
        };
    }
}

/**
 * A preconfigured bounce curve evaluated by elapsed time (ms).
 */
class BounceAnimation {
    /** @type {number} Time offset (s): 0, or half a flight time with startAtPeak. */
    #TimeAdjust = 0;
    /** @type {number} Output scale: 1, or 1 / MaxHeight when normalized. */
    #Scale = 1;
    /** @type {number} Launch velocity of the first period. */
    #InitialVelocity;
    /** @type {number} Gravity. */
    #Gravity;
    /** @type {number} Restitution. */
    #Restitution;
    /** @type {number} First flight time (s). */
    #FlightTime;
    /** @type {number} Rest time (s); 0 = never rests (restitution >= 1). */
    #RestAt = 0;

    /**
     * @param {number} initialVelocity - Upwards velocity (try 5).
     * @param {number} gravity - Gravity (try 10).
     * @param {number} restitution - (0, 1] velocity factor per bounce (try 0.7); >= 1 repeats the first bounce forever.
     * @param {number} [restVelocity=0.1] - Velocity considered at rest.
     * @param {boolean} [normalize=true] - Scale the output so the first peak is 1.
     * @param {boolean} [startAtPeak=false] - Start at the peak instead of at rest.
     */
    constructor(initialVelocity, gravity, restitution, restVelocity = 0.1, normalize = true, startAtPeak = false) {
        this.#InitialVelocity = initialVelocity;
        this.#Gravity = gravity;
        this.#Restitution = restitution;
        const flightTime = ProjectileMotion.FlightTime(initialVelocity, gravity);
        if (normalize)
            this.#Scale = 1.0 / ProjectileMotion.MaxHeight(initialVelocity, gravity);
        if (startAtPeak)
            this.#TimeAdjust = flightTime * 0.5;
        this.#FlightTime = flightTime;
        if (restitution < 1)
            this.#RestAt = BounceMotion.RestTime(initialVelocity, restitution, restVelocity, flightTime);
    }

    /**
     * The value at an elapsed time (no performance.now() fallback: pass the elapsed time).
     * @param {number} timeMs - Elapsed time (ms).
     * @returns {number} Height (0..1 when normalized), 0 once at rest.
     */
    ValueAt(timeMs) {
        let t = timeMs * 0.001 + this.#TimeAdjust;
        const r = this.#RestAt;
        if (r <= 0) {
            if (t < 0)
                t = 0;
            t %= this.#FlightTime;
            return ProjectileMotion.Displacement(this.#InitialVelocity, this.#Gravity, t) * this.#Scale;
        }
        if (t >= r)
            return 0;
        if (t < 0)
            t = 0;
        return BounceMotion.Displacement2(this.#InitialVelocity, this.#Gravity, this.#Restitution, this.#FlightTime, t) * this.#Scale;
    }
}

/**
 * `Sw.Anim`: the same API under the Sw namespace (created when missing; the core keeps an existing `window.Sw`).
 */
(window.Sw = window.Sw || {}).Anim = Object.freeze({
    Smoothness, FalseValue, TrueValue, MathTools, CubicHermite, CubicHermiteInterpolator, SmoothValue, SmoothBool,
    SpringAnimation, ProjectileMotion, BounceMotion, BounceAnimation,
});

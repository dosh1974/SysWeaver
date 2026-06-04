


/** Represents an interpolation method with various smoothness */
const Smoothness = Object.freeze(
	{
		/** Linear interpolation. C0 continuity */
		Normal: 0,

		/** "Smooth step". C0 and C1 continuity */
		Smooth: 1,

		/** Smoother interpoaltion. C0, C1 and C2 continuity */
		Smoother: 2,

		/** Hard step interpolation. No continuity */
		Step: 3,
	});


class MathTools
{
	/**
	 * Smooths a value going from zero to one. The smoothed value could be used for smooth interpolation (ease in/out). C0 and C1 continuity
	 * @param {number} t The input value [0, 1]
	 * @returns {number} The smoothed value [0, 1]
	 */
	static Smooth(t)
	{
		if (t <= 0)
			return 0;
		if (t >= 1)
			return 1;
		return t * t * (3 - t - t);
	}

	/**
	 * Smooths a value going from zero to one. The smoothed value could be used for smooth interpolation (ease in/out). C0, C1 and C2 continuity
	 * @param {number} t The input value [0, 1]
	 * @returns {number} The smoothed value [0, 1]
	 */
	static Smoother(t)
	{
		if (t <= 0)
			return 0;
		if (t >= 1)
			return 1;
		return t * t * t * (t * (t * 6 - 15) + 10);
	}


	/**
	 * Function that steps a value, if input is less than a half the output is 0 else 1. No continuity
	 * @param {number} t The input value [0, 1]
	 * @returns {number} The stepped value 0 or 1
	 */
	static Step(t)
	{
		return t < 0.5 ? 0 : 1;
	}

	/**
	 * Creates a smooth ease in / out for a time interval
	 * @param {number} time The normalized time in the interval [0, 1]
	 * @param {number} easeDuration The duration as a fraction of the normalized time for the ease in and out, [0, 1]
	 * @returns {number} The factor 0 = out, 1 = in [0, 1]
	 */
	static EaseSmooth(time, easeDuration)
	{
		if (typeof easeDuration !== "number")
			easeDuration = 0.1;
		if (easeDuration <= 0)
			return 1;
		time = (time < 0) ? 0 : (time > 1 ? 1 : time);
		easeDuration = (easeDuration > 1 ? 1 : easeDuration);
		const t0 = time / easeDuration;
		const t1 = 1 / easeDuration - t0;
		return MathTools.Smooth(t0) * MathTools.Smooth(t1);
	}

	/**
	 * Creates a smooth ease in / out for a time interval
	 * @param {number} time The normalized time in the interval [0, 1]
	 * @param {number} easeInDuration The duration as a fraction of the normalized time for the ease in [0, 1]
	 * @param {number} easeOutDuration The duration as a fraction of the normalized time for the ease in [0, 1]
	 * @returns {number} The factor 0 = out, 1 = in [0, 1]
	 */
	static EaseSmooth2(time, easeInDuration, easeOutDuration)
	{
		time = (time < 0) ? 0 : (time > 1 ? 1 : time);
		easeInDuration = (easeInDuration > 1 ? 1 : easeInDuration);
		easeOutDuration = (easeOutDuration > 1 ? 1 : easeOutDuration);
		const t0 = easeInDuration > 0 ? (time / easeInDuration) : 1;
		const t1 = easeOutDuration > 0 ? ((1 - time) / easeOutDuration) : 1;
		return MathTools.Smooth(t0) * MathTools.Smooth(t1);
	}

	/**
	 * Creates a smoother ease in / out for a time interval
	 * @param {number} time The normalized time in the interval [0, 1]
	 * @param {number} easeDuration The duration as a fraction of the normalized time for the easy in and out, [0, 1]
	 * @returns {number} The factor 0 = out, 1 = in [0, 1]
	 */
	static EaseSmoother(time, easeDuration)
	{
		if (typeof easeDuration !== "number")
			easeDuration = 0.1;
		if (easeDuration <= 0)
			return 1;
		time = (time < 0) ? 0 : (time > 1 ? 1 : time);
		easeDuration = (easeDuration > 1 ? 1 : easeDuration);
		const t0 = time / easeDuration;
		const t1 = 1 / easeDuration - t0;
		return MathTools.Smoother(t0) * MathTools.Smoother(t1);
	}

	/**
	 * Creates a smoother ease in / out for a time interval
	 * @param {number} time The normalized time in the interval [0, 1]
	 * @param {number} easeInDuration The duration as a fraction of the normalized time for the ease in [0, 1]
	 * @param {number} easeOutDuration The duration as a fraction of the normalized time for the ease in [0, 1]
	 * @returns {number} The factor 0 = out, 1 = in [0, 1]
	 */
	static EaseSmoother2(time, easeInDuration, easeOutDuration)
	{
		time = (time < 0) ? 0 : (time > 1 ? 1 : time);
		easeInDuration = (easeInDuration > 1 ? 1 : easeInDuration);
		easeOutDuration = (easeOutDuration > 1 ? 1 : easeOutDuration);
		const t0 = easeInDuration > 0 ? (time / easeInDuration) : 1;
		const t1 = easeOutDuration > 0 ? ((1 - time) / easeOutDuration) : 1;
		return MathTools.Smoother(t0) * MathTools.Smoother(t1);
	}

	/**
	 * Returns a function that can be used for interpolation for the given smoothness
	 * @param {Smoothness} s The smootness to give a function for
	 * @returns {function(number):number} Smoothness function, input is a value from [0, 1] and the output is a "smoothed" values in the same range [0, 1]
	 */
	static GetSmoothDoubleFunction(s) 
	{
		return MathTools.#DoubleSmoothers[s];
	}


	/** List of smoothers (mapping Smoothness enum to function) */
	static #DoubleSmoothers = 
	[
		x => x,
		MathTools.Smooth,
		MathTools.Smoother,
		MathTools.Step,
	];
}


/** Tools for computing hermite weights */
class CubicHermite
{
	/**
	 * Compute hermite basis weights
	 * @param {number} t The time [0, 1]
	 * @returns {object} Hermite weights, fromW, initW, toW, exitW
	 */
	static CalcBasis(t)
	{
		const t2 = t * t;
		const it = 1 - t;
		const td = t + t;
		const it2 = it * it;
		const fromW = (1 + td) * it2;
		const initW = t * it2;
		const toW = t2 * (3 - td);
		const exitW = t2 * (t - 1);
		return { fromW, initW, toW, exitW };
	}

	/**
	 * Compute hermite derivative basis weights
	 * @param {number} t The time [0, 1]
	 * @returns {object} Hermite weights, fromW, initW, toW, exitW
	 */
	static CalcDerivativeBasis(t)
	{
		const t2 = t * t;
		const t2_3 = t2 * 3;
		const fromW = 6 * (t2 - t);
		const initW = t2_3 - 4 * t + 1;
		const toW = 6 * (t - t2);
		const exitW = t2_3 - 2 * t;
		return { fromW, initW, toW, exitW };
	}

}

/** Class used to interpolate within a hermite curve segment*/
class CubicHermiteInterpolator
{
/*
#if DEBUG

	public override string ToString()
	{
		return String.Join("", From, " => ", To, ", ", Init, " -> ", Exit);
	}

#endif//DEBUG
*/
	/** {number} The from value */
	#From;
	/** {number} The inital velocity (derivative for the From value) */
	#Init;
	/** {number} The target value */
	#To;
	/** {number} The final velocity (derivative for the To value) */
	#Exit;

	/**
	 * Create an interpolator for
	 * @param {number} from The value to interpolate from
	 * @param {number} to The value to interpolate to
	 * @param {number} init The start velocity (derivative at the from value)
	 * @param {number} exit The end velocity (derivative at the to value)
	 * @returns {CubicHermiteInterpolator} An interpolator
	 */
	constructor(from, to, init, exit)
	{
		if ((typeof from === "object") && from)
		{
			this.#From = from.#From;
			this.#Init = from.#Init;
			this.#To = from.#To;
			this.#Exit = from.#Exit;
			return;
		}
		if (typeof init !== "number")
			init = 0;
		if (typeof exit !== "number")
			exit = 0;
	
		this.#From = from;
		this.#To = to;
		this.#Init = init;
		this.#Exit = exit;
	}

	/** {number} The current target value */
	get Target()
	{
		return this.#To;
	}

	/**
	 * Get the current value
	 * @param {number} t The time within the cubic hermite segment [0, 1]
	 * @returns {number} The interpolated value
	 */
	ValueAt(t)
	{
		const c = CubicHermite.CalcBasis(t);
		return this.#From * c.fromW + this.#Init * c.initW + this.#To * c.toW + this.#Exit * c.exitW;
	}
	
	/**
	 * Get the current velocity (derivivative)
	 * @param {number} t The time within the cubic hermite segment [0, 1]
	 * @returns {number} The interpolated velocity
	 */
	DerivativAt(t)
	{
		const c = CubicHermite.CalcDerivativeBasis(t);
		return this.#From * c.fromW + this.#Init * c.initW + this.#To * c.toW + this.#Exit * c.exitW;
	}
}



/** Hold the state for a value that changes in a smooth fashion */
class SmoothValue
{
/*
#if DEBUG

	public override string ToString()
	{
		return String.Join(" => ", ValueAt(DateTime.UtcNow), Target);
	}

#endif//DEBUG
*/
	/** The current time */
	#Time = 0;
	/** The hermite interpolator */
	#Hermite;
	/** Current adaption time */
	#AdaptionTime = 0;
	/** The fixed adaption time (default if none is supplied when chaning the value) */
	#FixedTime = 0;

	/// <summary>
	/// Create a new smoothed value
	/// </summary>
	/// <param name="timeMs">Current time stamp, use: performance.now()</param>
	/// <param name="value">The start value</param>
	/// <param name="duration">The default duration for changes</param>
	/// <param name="init">The initial derivate (tanget, speed etc)</param>
	/// <param name="from">If supplied the start value will be this and the target value will be the supplied value</param>
	/**
	 * Create a new smoothed value
	 * @param {number} timeMs Optional time stamp (for slow-mo, stepping etc), if this isn't a value, performance.now() is used
	 * @param {number} value The start value
	 * @param {number} duration Optional default duration for any value changes
	 * @param {number} init Optional initial derivate (tangent, speed etc)
	 * @param {number} from If supplied the start value will be this and the target value will be the supplied value
	 * @returns {SmoothValue} A smooth value instance
	 */
	constructor(timeMs, value, duration, init, from)
	{
		const ut = typeof timeMs;
		if ((ut === "object") && timeMs)
		{
			this.#Hermite = new CubicHermiteInterpolator(value.#Hermite);
			this.#Time = value.#Time;
			this.#AdaptionTime = value.#AdaptionTime;
			this.#FixedTime = value.#FixedTime;
			return;
		}
		if (ut !== "number")
			timeMs = performance.now();
		if (typeof value !== "number")
			value = 0;
		if (typeof init !== "number")
			init = 0;
		if (typeof duration !== "number")
			duration = 1000;
		this.#Time = timeMs;
		this.#Hermite = new CubicHermiteInterpolator(typeof from !== "number" ? value : from, value, init);
		this.#FixedTime = duration;
	}


	/// <summary>
	/// Sets a new target for the smooth value
	/// </summary>
	/// <param name="timeMs">Current time stamp, use: performance.now()</param>
	/// <param name="value">The new target value</param>
	/// <param name="duration">The transition duration in seconds from the current state to the new value</param>
	/// <param name="exitVelocity">The exit velocity</param>
	/// <returns>The current value</returns>
	/**
	 * Sets a new target for the smooth value
	 * @param {number} timeMs Optional time stamp (for slow-mo, stepping etc), if this isn't a value, performance.now() is used
	 * @param {number} value The new value
	 * @param {number} duration Optional duration for the value change
	 * @param {number} exitVelocity Optional velocity at the end (0 is default)
	 * @returns {number} The interpolated value
	 */
	Update(timeMs, value, duration, exitVelocity)
	{
		if (typeof timeMs !== "number")
			timeMs = performance.now();
		if (typeof exitVelocity !== "number")
			exitVelocity = 0;
		if (typeof duration !== "number")
			duration = this.#FixedTime;
		let elapsed = timeMs - this.#Time;
		if (elapsed < 0)
			elapsed = 0;
		const at = this.#AdaptionTime;
		let dt = at > 0 ? (elapsed / at) : 0;
		dt = dt > 1 ? 1 : dt;
		const h = this.#Hermite;
		const tv = duration <= 0 ? value : h.ValueAt(dt);
		if ((value != h.Target) || (at != duration))
		{
			this.#Time = timeMs;
			const init = h.DerivativAt(dt);
			this.#Hermite = new CubicHermiteInterpolator(tv, value, init, exitVelocity);
			this.#AdaptionTime = duration;
		}
		return tv;
	}

	/** {number} The target value (from the last Update) where the value will eventually end up at */
	get Target()
	{
		return this.#Hermite.Target;
	}

	/**
	 * Returns the value at the given time
	 * @param {number} timeMs Optional time stamp (for slow-mo, stepping etc), if this isn't a value, performance.now() is used
	 * @returns {number} The interpolated value
	 */
	ValueAt(timeMs)
	{
		if (typeof timeMs !== "number")
			timeMs = performance.now();
		let elapsed = timeMs - this.#Time;
		if (elapsed < 0)
			elapsed = 0;
		const a = this.#AdaptionTime;
		let dt = a > 0 ? (elapsed / a) : 0.0;
		if (dt > 1)
			dt = 1;
		return this.#Hermite.ValueAt(dt);
	}

	/**
	 * Returns the derivative (speed) at the given time
	 * @param {number} timeMs Optional time stamp (for slow-mo, stepping etc), if this isn't a value, performance.now() is used
	 * @returns {number} The derivative (speed) at the given time
	 */
	DerivativeAt(timeMs)
	{
		if (typeof timeMs !== "number")
			timeMs = performance.now();
		let elapsed = timeMs - this.#Time;
		if (elapsed < 0)
			elapsed = 0;
		const a = this.#AdaptionTime;
		let dt = a > 0 ? (elapsed / a) : 0.0;
		if (dt > 1)
			dt = 1;
		return this.#Hermite.DerivativAt(dt);
	}

	/**
	 * Set the value, removing any target. This will break c0 and c1 continuity
	 * @param {number} timeMs Optional time stamp (for slow-mo, stepping etc), if this isn't a value, performance.now() is used
	 * @param {number} value The new value
	 * @param {number} init Optional initial derivate (tangent, speed etc)
	 * @returns {number} The interpolated value
	 */
	SetTo(timeMs, value, init)
	{
		if (typeof timeMs !== "number")
			timeMs = performance.now();
		if (typeof init !== "number")
			init = 0;
		this.#Hermite = new CubicHermiteInterpolator(value, value, init, init);
		this.#Time = timeMs;
		this.#AdaptionTime = 0;
		return value;
	}


}


/** {number} The value that the SmoothBoolen uses for false */
const FalseValue = -0.0001;

/** {number} The value that the SmoothBoolen uses for true */
const TrueValue = 1.0001;

/** Represents a boolean that is smoothly changed to new targets */
class SmoothBool
{
	/** {Smoothness} The smoothing method */
	#Smooth;
	/** {SmoothValue} The internal SmoothValue used for keeping track */
	#Spline;
	/** {boolean} The current target (where we're interpolating towards or stopped at) */
	#InternalTarget;
	
	/** {boolean} The target state (from the last Update) where the value will eventually end up at */
	get Target()
	{ 
		return this.#InternalTarget;
	}


	/**
	 * Create a new smoothed boolean value
	 * @param {number} timeMs Optional time stamp (for slow-mo, stepping etc), if this isn't a value, performance.now() is used
	 * @param {boolean} startState Optional start state, default is False
	 * @param {number} duration Optional default duration for any value changes
	 * @param {Smoothness} smoothness The smoothing method to use
	 * @returns {SmoothBool} The pbject instance that represents a smooth boolean
	 */
	constructor(timeMs, startState, duration, smoothness)
	{
		if (typeof smoothness !== "number")
			smoothness = Smoothness.Normal;
		this.#Spline = new SmoothValue(timeMs, startState ? TrueValue : FalseValue, duration);
		this.#Smooth = MathTools.GetSmoothDoubleFunction(smoothness);
		this.#InternalTarget = startState;
	}

	/**
	 * Returns the value [0, 1] at the given time
	 * @param {number} timeMs Optional time stamp (for slow-mo, stepping etc), if this isn't a value, performance.now() is used
	 * @returns {number} The interpolated value between [0, 1], represnting false to true
	 */
	ValueAt(timeMs)
	{
		const v = this.#Spline.ValueAt(timeMs);
		if (v <= 0)
			return 0;
		if (v >= 1)
			return 1;
		return this.#Smooth(v);
	}

	/**
	 * Sets a new target for the smooth boolean
	 * @param {number} timeMs Optional time stamp (for slow-mo, stepping etc), if this isn't a value, performance.now() is used
	 * @param {boolean} value The new value
	 * @param {number} duration Optional duration for the value change
	 * @returns {number} The interpolated value
	 */
	Update(timeMs, state, duration)
	{
		const v = this.#Spline.Update(timeMs, state ? TrueValue : FalseValue, duration);
		this.#InternalTarget = state;
		if (v <= 0)
			return 0;
		if (v >= 1)
			return 1;
		return this.#Smooth(v);
	}

	/**
	 * Sets a new target for the smooth boolean
	 * @param {number} timeMs Optional time stamp (for slow-mo, stepping etc), if this isn't a value, performance.now() is used
	 * @param {boolean} value The new value
	 * @param {number} duration Optional duration for the value change
	 * @param {number} exitVelocity Optional velocity at the end (0 is default)
	 * @returns {number} The interpolated value
	 */

	SetTo(timeMs, state)
	{
		this.#Spline.SetTo(timeMs, state ? TrueValue : FalseValue);
		this.#InternalTarget = state;
	}
}


/** Creates an object that can be used for "springy" animations */
class SpringAnimation {

	/** {SpringAnimation} A "default" springy zoom animation */
	static #InternalZoom = new SpringAnimation(3, 1, 500, 0.001);

	/** {SpringAnimation} A "default" springy zoom animation */
	static get Zoom() {
		return SpringAnimation.#InternalZoom;
	}

	/**
	 * Defines a "springy" animation
	 * @param {number} frequency The oscillation frequency (typically in cycles per second)
	 * @param {number} initialLength The initial spring length (how tense it is at start)
	 * @param {number} decay Determines how much energy is maintained over time as a percentage [0, 100)
	 * @param {number} snap Can force stop the spring earlier if it reaches this length
	 */
	constructor(frequency = 7, initialLength = 100, decay = 90, snap = 0.01) {
		this.#Frequency = frequency * Math.PI * 2;
		this.#InitialLength = initialLength;
		this.#Decay = decay * 0.01;
		this.#EndTime = snap > 0 ? Math.log(initialLength / snap) / this.#Decay : 1000;
	}

	/** {number} The oscillation frequency (typically in cycles per second) */
	#Frequency;
	/** {number} The initial spring length (how tense it is at start) */
	#InitialLength;
	/** {number} Determines how much energy is maintained over time as a percentage [0, 100) */
	#Decay;
	/** {number} Can force stop the spring earlier if it reaches this length */
	#EndTime;

	/**
	 * Evaluate the value at the given time
	 * @param {number} timeMs Optional time stamp (for slow-mo, stepping etc), if this isn't a value, performance.now() is used
	 * @returns {number} The current displacement
	 */
	Displacement(timeMs) {
		if (typeof timeMs !== "number")
			timeMs = performance.now();
		timeMs *= 0.001;
		if (timeMs > this.#EndTime)
			return 0;
		if (timeMs < 0)
			timeMs = 0;
		return this.#InitialLength * Math.cos(this.#Frequency * timeMs) / Math.exp(this.#Decay * timeMs);
	}

	/**
	 * Evaluate the value at the given time
	 * @param {number} timeMs Optional time stamp (for slow-mo, stepping etc), if this isn't a value, performance.now() is used
	 * @returns {number} The current displacement
	 */
	ValueAt(timeMs) {
		return this.Displacement(timeMs);
	}
}



/** Equations related to a projectile motion where the projectile is shot upwards */
class ProjectileMotion {

	/**
	 * Compute the time of flight for a projectile starting at ground and ending at ground
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s
	 * @param {number} gravity The gravity (downwards), typically m/s2
	 * @returns {number} The time of flight, typically in seconds 
	 */
	static FlightTime(initialVelocity, gravity) {
		return (initialVelocity * 2) / gravity;
	}

	/**
	 * Compute the maximum height of projectile starting at ground
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s
	 * @param {number} gravity The gravity (downwards), typically m/s2
	 * @returns {number} The maximum height, typically in meter
	 */
	static MaxHeight(initialVelocity, gravity) {
		return initialVelocity * initialVelocity * 0.5 / Math.abs(gravity);
	}

	/**
	 * The position at a given time for a projectile starting at the ground
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s
	 * @param {number} gravity The gravity (downwards), typically m/s2
	 * @param {number} time The time, typically in seconds, if greater than the computed flight time it will be below ground
	 * @returns {number} The vertical position, typically in meters as the height above ground
	 */
	static Displacement(initialVelocity, gravity, time) {
		return (initialVelocity - gravity * 0.5 * time) * time;
	}

	/**
	 * The velocity at a given time for a projectile starting at the ground
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s
	 * @param {number} gravity The gravity (downwards), typically m/s2
	 * @param {number} time The time, typically in seconds, if greater than the computed flight time it will be below ground
	 * @returns {number} The vertical velocity, typically in m/s
	 */
	static Velocity(initialVelocity, gravity, time) {
		return initialVelocity - gravity * time;
	}

	/**
	 * Compute an inital velocity and gravity that gives the desired max height and flight time
	 * @param {number} maxHeight The maximum height, typically in meters
	 * @param {number} flightTime The time of flight, typically in seconds
	 * @returns {object} initialVelocity = The initial velocity (upwards), typically m/s
	 * gravity = The gravity (downwards), typically m/s2
	 */
	static Solve(maxHeight, flightTime) {
		const initialVelocity = maxHeight * 4 / flightTime;
		const gravity = maxHeight * 8 / (flightTime * flightTime);
		return { initialVelocity, gravity };
	}
}


/** Represent a bounce motion */
class BounceMotion {

	/**
	 * Computes what period the specified time is in given the intital conditions
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s
	 * @param {number} gravity The gravity (downwards), typically m/s2
	 * @param {number} restitution (0, 1] The "friction" or dampening at each bounce, try 0.7
	 * @param {number} flightTime The time of flight for the initial period, use the projectile motion to compute
	 * @param {number} time The time, typically in seconds
	 * @returns {integer} The period (number of bounces) that the time belong to
	 */
	static Period(initialVelocity, gravity, restitution, flightTime, time) {
		const t = time / flightTime;
		const p = Math.log(Math.max(0.000000001, (restitution - 1) * t + 1)) / Math.log(restitution);
		return Math.floor(Math.max(0, p));
	}

	/**
	 * Compute the number of periods (bounces) that is required before we consider the motion to be at rest
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s
	 * @param {number} restitution (0, 1] The "friction" or dampening at each bounce, try 0.7
	 * @param {number} restVelocity The velocity where we consider the motion to be at rest at, typically in m/s
	 * @returns {integer} he period where we reach the rest velocity (or lower)
	 */
	static RestAfterPeriod(initialVelocity, restitution, restVelocity) {
		const p = Math.log(restVelocity / initialVelocity) / Math.log(restitution);
		return Math.floor(Math.max(0, p));
	}

	/**
	 * Compute the time where the motion becomes at rest
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s
	 * @param {number} restitution (0, 1] The "friction" or dampening at each bounce, try 0.7
	 * @param {number} restVelocity The velocity where we consider the motion to be at rest at, typically in m/s
	 * @param {number} flightTime The time of flight for the initial period, use the projectile motion to compute
	 * @returns {number} The time when the motion reaches the rest velocity (or lower)
	 */
	static RestTime(initialVelocity, restitution, restVelocity, flightTime) {
		const p = BounceMotion.RestAfterPeriod(initialVelocity, restitution, restVelocity);
		return BounceMotion.PeriodEndTime(restitution, p) * flightTime;
	}

	/**
	 * Computes the (intital) velocity for a given period (bounce)
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s
	 * @param {number} restitution (0, 1] The "friction" or dampening at each bounce, try 0.7
	 * @param {integer} period The period index, 0 = initial, 1 is outgoing velocity after 1 bounce and so on
	 * @returns {number} The velocity for the given period, typically in m/s
	 */
	static PeriodVelocity(initialVelocity, restitution, period) {
		return Math.pow(restitution, period) * initialVelocity;
	}

	/**
	 * Computes the duration of a given period
	 * @param {number} restitution (0, 1] The "friction" or dampening at each bounce, try 0.7
	 * @param {integer} period The period index, 0 = initial, 1 is outgoing velocity after 1 bounce and so on
	 * @returns {number} The duration for the given period, typically in seconds
	 */
	static PeriodDuration(restitution, period) {
		return Math.pow(restitution, period);
	}

	/**
	 * Computes the end time of a given period
	 * @param {number} restitution The "friction" or dampening at each bounc
	 * @param {integer} period The period index, 0 = initial, 1 is outgoing velocity after 1 bounce and so on
	 * @returns {number} The time when the given period ends, typically in seconds
	 */
	static PeriodEndTime(restitution, period) {
		return (1.0 + (restitution * (Math.pow(restitution, period) - 1)) / (restitution - 1));
	}

	/**
	 * Computes the position at the given time and parameters, computes the inital flight time (can be precomputed)
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s
	 * @param {number} gravity The gravity (downwards), typically m/s2
	 * @param {number} restitution The "friction" or dampening at each bounce
	 * @param {number} time The time, typically in seconds
	 * @returns {number} he position at the given time and parameters, typically in meters
	 */
	static Displacement(initialVelocity, gravity, restitution, time) {
		return BounceMotion.Displacement2(initialVelocity, gravity, restitution, ProjectileMotion.FlightTime(initialVelocity, gravity), time);
	}

	/**
	 * Computes the position at the given time and parameters
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s
	 * @param {number} gravity The gravity (downwards), typically m/s2
	 * @param {number} restitution The "friction" or dampening at each bounce
	 * @param {number} flightTime The time of flight for the initial period, use the projectile motion to compute
	 * @param {number} time The time, typically in seconds
	 * @returns {number} The position at the given time and parameters, typically in meters
	 */
	static Displacement2(initialVelocity, gravity, restitution, flightTime, time) {
		const p = BounceMotion.Period(initialVelocity, gravity, restitution, flightTime, time);
		const t1 = BounceMotion.PeriodEndTime(restitution, p) * flightTime;
		const tm = BounceMotion.PeriodDuration(restitution, p) * flightTime;
		const dt = Math.max(0, Math.min(tm, time - (t1 - tm)));
		const v = BounceMotion.PeriodVelocity(initialVelocity, restitution, p);
		return ProjectileMotion.Displacement(v, gravity, dt);
	}

	/**
	 * Computes the velocity at the given time and parameters, computes the inital flight time (can be precomputed)
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s
	 * @param {number} gravity The gravity (downwards), typically m/s2
	 * @param {number} restitution (0, 1] The "friction" or dampening at each bounce, try 0.7
	 * @param {number} time The time, typically in seconds
	 * @returns {number} The velocity at the given time and parameters, typically in m/s
	 */
	static Velocity(initialVelocity, gravity, restitution, time) {
		return Velocity2(initialVelocity, gravity, restitution, ProjectileMotion.FlightTime(initialVelocity, gravity), time);
	}

	/**
	 * Computes the velocity at the given time and parameters
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s
	 * @param {number} gravity The gravity (downwards), typically m/s2
	 * @param {number} restitution (0, 1] The "friction" or dampening at each bounce, try 0.7
	 * @param {number} flightTime The time of flight for the initial period, use the projectile motion to comput
	 * @param {number} time The time, typically in seconds
	 * @returns {number} The velocity at the given time and parameters, typically in m/s
	 */
	static Velocity2(initialVelocity, gravity, restitution, flightTime, time) {
		const p = ProjectileMotion.Period(initialVelocity, gravity, restitution, flightTime, time);
		const t1 = ProjectileMotion.PeriodEndTime(restitution, p) * flightTime;
		const tm = ProjectileMotion.PeriodDuration(restitution, p) * flightTime;
		const dt = Math.max(0, Math.min(tm, time - (t1 - tm)));
		const v = ProjectileMotion.PeriodVelocity(initialVelocity, restitution, p);
		return ProjectileMotion.Velocity(v, gravity, dt);
	}
}



/** Represent a bounce motion */
class BounceAnimation {

	/**
	 * Create an object that represents a bounce motion
	 * @param {number} initialVelocity The initial velocity (upwards), typically m/s, try 5
	 * @param {number} gravity The gravity (downwards), typically m/s2, try 10
	 * @param {number} restitution (0, 1] The "friction" or dampening at each bounce, try 0.7
	 * @param {number} restVelocity The velocity where we consider the motion to be at rest at, typically in m/s
	 * @param {boolean} normalize If true, the output values will be between 0 and 1, initial velocity and gravity magnitude will loose it's meanig and the ratio between the two will describe the motion
	 * @param {boolean} startAtPeak If true the motion will start at the peak value instead of the at rest value
	 */
	constructor(initialVelocity, gravity, restitution, restVelocity = 0.1, normalize = true, startAtPeak = false) {
		this.#InitialVelocity = initialVelocity;
		this.#Gravity = gravity;
		this.#Restitution = restitution;
		var flightTime = ProjectileMotion.FlightTime(initialVelocity, gravity);
		if (normalize)
			this.#Scale = 1.0 / ProjectileMotion.MaxHeight(initialVelocity, gravity);
		if (startAtPeak)
			this.#TimeAdjust = flightTime * 0.5;
		this.#FlightTime = flightTime;
		if (restitution < 1)
			this.#RestAt = BounceMotion.RestTime(initialVelocity, restitution, restVelocity, flightTime);
	}

	#TimeAdjust = 0;
	#Scale = 1;
	#InitialVelocity;
	#Gravity;
	#Restitution;
	#FlightTime;
	#RestAt = 0;

	/**
	 * Evaluate the value at the given time
	 * @param {number} timeMs Optional time stamp (for slow-mo, stepping etc), if this isn't a value, performance.now() is used
	 * @returns {number} The value at the specified time, typically in meters
	 */
	ValueAt(timeMs) {
		timeMs *= 0.001;
		timeMs += this.#TimeAdjust;
		var r = this.#RestAt;
		if (r <= 0) {
			if (timeMs < 0)
				timeMs = 0;
			timeMs %= this.#FlightTime;
			return ProjectileMotion.Displacement(this.#InitialVelocity, this.#Gravity, timeMs) * this.#Scale;
		}
		if (timeMs >= this.#RestAt)
			return 0;
		if (timeMs < 0)
			timeMs = 0;
		return BounceMotion.Displacement2(this.#InitialVelocity, this.#Gravity, this.#Restitution, this.#FlightTime, timeMs) * this.#Scale;
	}

}

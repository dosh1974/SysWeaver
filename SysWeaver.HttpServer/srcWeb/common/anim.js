


/// <summary>
/// Represents an interpolation method with various smoothness
/// </summary>
const Smoothness = Object.freeze(
{
	/// <summary>
	/// Linear interpolation. C0 continuity
	/// </summary>
	Normal: 0,
	/// <summary>
	/// "Smooth step". C0 and C1 continuity
	/// </summary>
	Smooth: 1,
	/// <summary>
	/// Smoother interpoaltion. C0, C1 and C2 continuity
	/// </summary>
	Smoother: 2,
	/// <summary>
	/// Hard step interpolation. No continuity
	/// </summary>
	Step: 3,
});


class MathTools
{
	/// <summary>
	/// Smooths a value going from zero to one. The smoothed value could be used for smooth interpolation (ease in/out). C0 and C1 continuity
	/// </summary>
	/// <param name="t">The input value [0, 1]</param>
	/// <returns>The smoothed value [0, 1]</returns>
	static Smooth(t)
	{
		if (t <= 0)
			return 0;
		if (t >= 1)
			return 1;
		return t * t * (3 - t - t);
	}


	/// <summary>
	/// Smooths a value going from zero to one. The smoothed value could be used for smooth interpolation (ease in/out). C0, C1 and C2 continuity
	/// </summary>
	/// <param name="t">The input value [0, 1]</param>
	/// <returns>The smoothed value [0, 1]</returns>
	static Smoother(t)
	{
		if (t <= 0)
			return 0;
		if (t >= 1)
			return 1;
		return t * t * t * (t * (t * 6 - 15) + 10);
	}

	/// <summary>
	/// Function that steps a value, if input is less than a half the output is 0 else 1. No continuity
	/// </summary>
	/// <param name="t">The input value [0, 1]</param>
	/// <returns>The stepped value [0, 1]</returns>
	static Step(t)
	{
		return t < 0.5 ? 0 : 1;
	}

	/// <summary>
	/// Creates a smooth ease in / out for a time interval
	/// </summary>
	/// <param name="time">The normalized time in the interval [0, 1]</param>
	/// <param name="easeDuration">The duration as a fraction of the normalized time for the ease in and out, [0, 1]</param>
	/// <returns>The factor 0 = out, 1 = in [0, 1]</returns>
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


	/// <summary>
	/// Creates a smooth ease in / out for a time interval
	/// </summary>
	/// <param name="time">The normalized time in the interval [0, 1]</param>
	/// <param name="easeInDuration">The duration as a fraction of the normalized time for the ease in [0, 1]</param>
	/// <param name="easeOutDuration">The duration as a fraction of the normalized time for the ease in [0, 1]</param>
	/// <returns>The factor 0 = out, 1 = in [0, 1]</returns>
	static EaseSmooth2(time, easeInDuration, easeOutDuration)
	{
		time = (time < 0) ? 0 : (time > 1 ? 1 : time);
		easeInDuration = (easeInDuration > 1 ? 1 : easeInDuration);
		easeOutDuration = (easeOutDuration > 1 ? 1 : easeOutDuration);
		const t0 = easeInDuration > 0 ? (time / easeInDuration) : 1;
		const t1 = easeOutDuration > 0 ? ((1 - time) / easeOutDuration) : 1;
		return MathTools.Smooth(t0) * MathTools.Smooth(t1);
	}



	/// <summary>
	/// Creates a smoother ease in / out for a time interval
	/// </summary>
	/// <param name="time">The normalized time in the interval [0, 1]</param>
	/// <param name="easeDuration">The duration as a fraction of the normalized time for the easy in and out, [0, 1]</param>
	/// <returns>The factor 0 = out, 1 = in [0, 1]</returns>
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

	/// <summary>
	/// Creates a smoother ease in / out for a time interval
	/// </summary>
	/// <param name="time">The normalized time in the interval [0, 1]</param>
	/// <param name="easeInDuration">The duration as a fraction of the normalized time for the ease in [0, 1]</param>
	/// <param name="easeOutDuration">The duration as a fraction of the normalized time for the ease in [0, 1]</param>
	/// <returns>The factor 0 = out, 1 = in [0, 1]</returns>
	static EaseSmoother2(time, easeInDuration, easeOutDuration)
	{
		time = (time < 0) ? 0 : (time > 1 ? 1 : time);
		easeInDuration = (easeInDuration > 1 ? 1 : easeInDuration);
		easeOutDuration = (easeOutDuration > 1 ? 1 : easeOutDuration);
		const t0 = easeInDuration > 0 ? (time / easeInDuration) : 1;
		const t1 = easeOutDuration > 0 ? ((1 - time) / easeOutDuration) : 1;
		return MathTools.Smoother(t0) * MathTools.Smoother(t1);
	}

	/// <summary>
	/// Returns a function that can be used for interpolation for the given smoothness
	/// </summary>
	/// <param name="s">The smootness to give a function for</param>
	/// <returns>Smoothness function, input is a value from [0, 1] and the output is a "smoothed" values in the same range [0, 1]</returns>
	static GetSmoothDoubleFunction(s) 
	{
		return MathTools.#DoubleSmoothers[s];
	}


	static #DoubleSmoothers = 
	[
		x => x,
		MathTools.Smooth,
		MathTools.Smoother,
		MathTools.Step,
	];
}

class CubicHermite
{

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
	#From;
	#Init;
	#To;
	#Exit;

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

	get Target()
	{
		return this.#To;
	}


	ValueAt(t)
	{
		const c = CubicHermite.CalcBasis(t);
		return this.#From * c.fromW + this.#Init * c.initW + this.#To * c.toW + this.#Exit * c.exitW;
	}
	
	DerivativAt(t)
	{
		const c = CubicHermite.CalcDerivativeBasis(t);
		return this.#From * c.fromW + this.#Init * c.initW + this.#To * c.toW + this.#Exit * c.exitW;
	}
}



/// <summary>
/// Represents a value that is smoothly changed to new targets
/// </summary>
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
	#Time = 0;
	#Hermite;
	#AdaptionTime = 0;

	/// <summary>
	/// Create a new smoothed value
	/// </summary>
	/// <param name="timeMs">Current time stamp, use: performance.now()</param>
	/// <param name="value">The start value</param>
	/// <param name="init">The initial derivate (tanget, speed etc)</param>
	/// <param name="from">If supplied the start value will be this and the target value will be the supplied value</param>
	constructor(timeMs, value, init, from)
	{
		const ut = typeof timeMs;
		if ((ut === "object") && timeMs)
		{
			this.#Hermite = new CubicHermiteInterpolator(value.#Hermite);
			this.#Time = value.#Time;
			this.#AdaptionTime = value.#AdaptionTime;
			return;
		}
		if (ut !== "number")
			timeMs = performance.now();
		if (typeof value !== "number")
			value = 0;
		if (typeof init !== "number")
			init = 0;
		this.#Time = timeMs;
		this.#Hermite = new CubicHermiteInterpolator(typeof from !== "number" ? value : from, value, init);
	}


	/// <summary>
	/// Sets a new target for the smooth value
	/// </summary>
	/// <param name="timeMs">Current time stamp, use: performance.now()</param>
	/// <param name="value">The new target value</param>
	/// <param name="duration">The transition duration in seconds from the current state to the new value</param>
	/// <param name="exitVelocity">The exit velocity</param>
	/// <returns>The current value</returns>
	Update(timeMs, value, duration, exitVelocity)
	{
		if (typeof timeMs !== "number")
			timeMs = performance.now();
		if (typeof exitVelocity !== "number")
			exitVelocity = 0;
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

	/// <summary>
	/// The target value (from the last Update) where the value will eventually end up at
	/// </summary>
	get Target()
	{
		return this.#Hermite.Target;
	}

	/// <summary>
	/// Returns the value at the given time
	/// </summary>
	/// <param name="timeMs">Current time stamp, use: performance.now()</param>
	/// <returns>The value at the given time</returns>
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

	/// <summary>
	/// Returns the derivative (speed) at the given time
	/// </summary>
	/// <param name="timeMs">Current time stamp, use: performance.now()</param>
	/// <returns>The derivative (speed) at the given time</returns>
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

	/// <summary>
	/// Set the value, removing any target. This will break c0 and c1 continuity
	/// </summary>
	/// <param name="timeMs">Current time stamp, use: performance.now()</param>
	/// <param name="value">The new value</param>
	/// <param name="init">The initial velocity</param>
	/// <returns>The current value</returns>
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


const FalseValue = -0.0001;
const TrueValue = 1.0001;

/// <summary>
/// Represents a boolean that is smoothly changed to new targets
/// </summary>
class SmoothBool
{
	#Smooth;
	#FixedDuration;
	#Spline;
	#InternalTarget;
	
	/// <summary>
	/// The target state (from the last Update) where the value will eventually end up at
	/// </summary>
	get Target()
	{ 
		return this.#InternalTarget;
	}


	/// <summary>
	/// Create a new smoothed boolean value
	/// </summary>
	/// <param name="timeMs">Current time stamp, use: performance.now()</param>
	/// <param name="startState">The initial state of the boolean</param>
	/// <param name="duration">The default duration for changes</param>
	/// <param name="smoothness">An optional extra smoothness step</param>
	constructor(timeMs, startState, duration, smoothness)
	{
		if (typeof smoothness !== "number")
			smoothness = Smoothness.Normal;
		this.#Spline = new SmoothValue(timeMs, startState ? TrueValue : FalseValue);
		this.#Smooth = MathTools.GetSmoothDoubleFunction(smoothness);
		this.#FixedDuration = duration;
		this.#InternalTarget = startState;
	}

	/// <summary>
	/// Returns the value [0, 1] at the given time
	/// </summary>
	/// <param name="timeMs">Current time stamp, use: performance.now()</param>
	/// <returns>The value at the given time</returns>
	ValueAt(timeMs)
	{
		const v = this.#Spline.ValueAt(timeMs);
		if (v <= 0)
			return 0;
		if (v >= 1)
			return 1;
		return this.#Smooth(v);
	}

	/// <summary>
	/// Set a new state, where false will end up at value 0 and true will end up at value 1, the default durations as specified when constructing will be used
	/// </summary>
	/// <param name="timeMs">Current time stamp, use: performance.now()</param>
	/// <param name="state">The new target state</param>
	/// <param name="duration">The transition duration in seconds from the current state to the new value</param>
	/// <returns>The current value [0, 1]</returns>
	Update(timeMs, state, duration)
	{
		if (typeof duration !== "number")
			duration = this.#FixedDuration;
		const v = this.#Spline.Update(timeMs, state ? TrueValue : FalseValue, duration);
		this.#InternalTarget = state;
		if (v <= 0)
			return 0;
		if (v >= 1)
			return 1;
		return this.#Smooth(v);
	}

	/// <summary>
	/// Set the state, removing any target. This will break c0 and c1 continuity
	/// </summary>
	/// <param name="timeMs">Current time stamp</param>
	/// <param name="state">The state</param>
	SetTo(timeMs, state)
	{
		this.#Spline.SetTo(timeMs, state ? TrueValue : FalseValue);
		this.#InternalTarget = state;
	}
}



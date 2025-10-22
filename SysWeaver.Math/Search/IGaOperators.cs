using System;

namespace SysWeaver.Search
{
    /// <summary>
    /// These are the operations that a user must implement on the state
    /// </summary>
    /// <typeparam name="T">The state/data of the problem</typeparam>
    public interface IGaOperators<T> where T : class
    {
        /// <summary>
        /// Create a new state that is a depp clone of the source
        /// </summary>
        /// <param name="data">The data/state to clone</param>
        /// <param name="into">If non-null this object may be used as the target of the clone (save some allocations)</param>
        /// <returns>A deep clone of the state (may be the same as into)</returns>
        T Clone(T data, T into = null);

        /// <summary>
        /// Called one or more times per generation to randomly mutate the state (minimum amount)
        /// </summary>
        /// <param name="data">The state to mutate</param>
        /// <param name="rng">The rng to use (if some other rng is used, it must be thread safe)</param>
        void Mutate(T data, Random rng);

        /// <summary>
        /// Called once per generation to mutate the state (minimum amount)
        /// </summary>
        /// <param name="data">The state to mutate</param>
        /// <param name="rng">The rng to use (if some other rng is used, it must be thread safe)</param>
        void MutateLast(T data, Random rng);

        /// <summary>
        /// Computes the error of the given state, smaller = better, this is what the optimizer will try to lower
        /// </summary>
        /// <param name="data">The state to compute the error for</param>
        /// <returns>An error value</returns>
        double Error(T data);

        /// <summary>
        /// Called periodically, may return true to abort computations and/or to display progress
        /// </summary>
        /// <param name="error">The lowest error found so far</param>
        /// <param name="generation">The number of generations processed so far</param>
        /// <param name="unchangedGenerations">The number of generations that no improvements have been made</param>
        /// <param name="mutationRate">The current mutation rate</param>
        /// <returns>True to abort processing or false to continue</returns>
        bool Abort(double error, int generation, int unchangedGenerations, int mutationRate);






    }
}

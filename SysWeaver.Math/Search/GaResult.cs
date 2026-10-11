namespace SysWeaver.Search
{



    /// <summary>
    /// Represents the result of a solver/optimizer run
    /// </summary>
    public sealed class GaResult
    {
        /// <summary>
        /// The error of the final solution
        /// </summary>
        public readonly double Error;
        
        /// <summary>
        /// The number of generations ran
        /// </summary>
        public readonly int Generations;
        
        /// <summary>
        /// The number of generations that no improvement was made
        /// </summary>
        public readonly int UnchangedGenerations;

        /// <summary>
        /// Create a result.
        /// </summary>
        /// <param name="error">The error of the final solution.</param>
        /// <param name="generations">The number of generations ran.</param>
        /// <param name="uncgangedGen">The number of generations that no improvement was made.</param>
        public GaResult(double error, int generations, int uncgangedGen)
        {
            Error = error;
            Generations = generations;
            UnchangedGenerations = uncgangedGen;
        }
    }
}

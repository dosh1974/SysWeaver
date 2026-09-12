namespace SysWeaver.AI
{
    /// <summary>
    /// What function call debugging to output
    /// </summary>
    public enum OpenAiDebugInfo
    {
        /// <summary>
        /// Output some basic stats about the AI completion.
        /// </summary>
        Stats,
        /// <summary>
        /// Output the function names and if they was successful.
        /// </summary>
        Overview,
        /// <summary>
        /// Output function calls, the parameters supplied by OpenAI (as a json blob).
        /// </summary>
        Parameters,
        /// <summary>
        /// Output function calls, the parameters supplied by OpenAI (as a json blob) and the results supplied back to OpenAI.
        /// </summary>
        Details,
    }


}

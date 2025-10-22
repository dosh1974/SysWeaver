namespace SysWeaver.Translation
{
    /// <summary>
    /// How much time / cost to spend on a translation
    /// </summary>
    public enum TranslationEffort
    {
        /// <summary>
        /// Translate as quick as possible, prefer low cost over quality.
        /// Typically use a cheaper LLM or some other cheap translation API.
        /// Use this for high volume translations such as chat messages and "uncontrollable" inputs.
        /// </summary>
        Low,
        /// <summary>
        /// Balanced approach, typically using a mid range LLM.
        /// Perfect for push messages and more controlled texts.
        /// </summary>
        Medium,
        /// <summary>
        /// High quality translation, typically using a more expensive LLM.
        /// For static resources, typically combined with long retention.
        /// </summary>
        High
    }



}

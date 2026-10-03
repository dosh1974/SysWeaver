using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Information about a large language model (chat / text generation) available from an AI service
    /// </summary>
    public sealed class AiLlmModel
    {
        public override string ToString() => String.IsNullOrEmpty(Name) ? Id : String.Concat(Id, " (", Name, ')');

        /// <summary>
        /// The model id, this is the value to use as the model in a session, ex: "gpt-4.1"
        /// </summary>
        public String Id;

        /// <summary>
        /// A human readable name of the model (if available)
        /// </summary>
        public String Name;

        /// <summary>
        /// A description of the model (if available)
        /// </summary>
        public String Description;

        /// <summary>
        /// The organization that owns the model (if available)
        /// </summary>
        public String Owner;

        /// <summary>
        /// When the model was created (if available)
        /// </summary>
        public DateTime? Created;

        /// <summary>
        /// The maximum number of input tokens (if available)
        /// </summary>
        public int? InputTokenLimit;

        /// <summary>
        /// The maximum number of output tokens (if available)
        /// </summary>
        public int? OutputTokenLimit;

        /// <summary>
        /// True if the model supports thinking (reasoning), null if unknown
        /// </summary>
        public bool? CanReason;
    }

}

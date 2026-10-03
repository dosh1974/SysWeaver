using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Information about an image generation model available from an AI service
    /// </summary>
    public sealed class AiImageModel
    {
        public override string ToString() => String.IsNullOrEmpty(Name) ? Id : String.Concat(Id, " (", Name, ')');

        /// <summary>
        /// The model id, this is the value to use as the image model, ex: "dall-e-3"
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
    }

}

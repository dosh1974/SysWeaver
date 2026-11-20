using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Information about an image edit model (image to image) available from an AI service, see <see cref="IAiImageEditService.GetImageEditModels"/>
    /// </summary>
    [SysWeaver.Data.TableDataCard("+image@ai/cards/models")]
    public sealed class AiImageEditModel
    {
        /// <inheritdoc />
        public override string ToString() => String.IsNullOrEmpty(Name) ? Id : String.Concat(Id, " (", Name, ')');

        /// <summary>
        /// The model id, this is the value to use as the image edit model, ex: "gpt-image-1"
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
        /// The maximum number of source images the model accepts, 0 if unknown
        /// </summary>
        public int MaxSourceImages;

        /// <summary>
        /// Create an edit model from an image model (same id, name etc)
        /// </summary>
        /// <param name="m">The image model</param>
        /// <param name="maxSourceImages">The maximum number of source images, 0 if unknown</param>
        /// <returns>The edit model</returns>
        public static AiImageEditModel From(AiImageModel m, int maxSourceImages = 0)
        {
            ArgumentNullException.ThrowIfNull(m);
            return new AiImageEditModel
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                Owner = m.Owner,
                Created = m.Created,
                MaxSourceImages = maxSourceImages,
            };
        }
    }

}

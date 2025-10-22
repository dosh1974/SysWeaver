using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format valus as:
    /// A clickable image link.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataImgAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// A clickable image.
        /// </summary>
        /// <param name="imgUrlFormat">
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// Image alignment can (optionally) be controlled by prefixing (the evaluated) url with:
        /// '-' for left alignment.
        /// '*' for center alignment (default).
        /// '+' for right alignment.
        /// </param>
        /// <param name="urlFormat">
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// {2} = The image url (after formatting). 
        /// The target for the link can (optionally) be controlled by prefixing (the evaluated) url with:
        /// '+' open in a new tab: "_blank" (default).
        /// '*' open in same frame: "_self".
        /// '^' open in same window: "_top".
        /// '-' open in parent frame: "_parent".
        /// </param>
        /// <param name="titleFormat">
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// {2} = The image url (after formatting). 
        /// {3} = The url (after formatting). 
        /// </param>
        /// <param name="maxWidth">If greater than 0, set the max-width property</param>
        /// <param name="maxHeight">If greater than 0, set the max-height property</param>
        /// <param name="relativeUrlPrefix">If this is non-empty AND the url value is a non-absolute url, the prefix is added to the relative url</param>
        public TableDataImgAttribute(String imgUrlFormat = "{0}", String urlFormat = "{2}", String titleFormat = "Click to open \"{3}\".", int maxWidth = TableDataConsts.ImgMaxWidth, int maxHeight = TableDataConsts.ImgMaxHeight, String relativeUrlPrefix = null) 
            : base(TableDataFormats.Img, imgUrlFormat ?? "{0}", urlFormat ?? "{2}", titleFormat ?? "Click to open \"{3}\".", maxWidth, maxHeight, relativeUrlPrefix)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataUserIconAttribute : TableDataImgAttribute
    {
        /// <summary>
        /// A clickable image.
        /// </summary>
        /// <param name="root">Path used to get back to site root</param>
        /// <param name="imageSize">
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// Image alignment can (optionally) be controlled by prefixing (the evaluated) url with:
        /// '-' for left alignment.
        /// '*' for center alignment (default).
        /// '+' for right alignment.
        /// </param>
        /// <param name="urlFormat">
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// {2} = The image url (after formatting). 
        /// The target for the link can (optionally) be controlled by prefixing (the evaluated) url with:
        /// '+' open in a new tab: "_blank" (default).
        /// '*' open in same frame: "_self".
        /// '^' open in same window: "_top".
        /// '-' open in parent frame: "_parent".
        /// </param>
        /// <param name="titleFormat">
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// {2} = The image url (after formatting). 
        /// {3} = The url (after formatting). 
        /// </param>
        /// <param name="maxWidth">If greater than 0, set the max-width property</param>
        /// <param name="maxHeight">If greater than 0, set the max-height property</param>
        public TableDataUserIconAttribute(String root = "../", String imageSize = "small", String urlFormat = "{2}/../large", String titleFormat = "Click to open \"{3}\".", int maxWidth = TableDataConsts.ImgMaxWidth, int maxHeight = TableDataConsts.ImgMaxHeight) 
            : base(String.Concat(root, "auth/UserImages/{0}/", imageSize ?? "small"), urlFormat, titleFormat, maxWidth, maxHeight)
        {
        }
    }



}




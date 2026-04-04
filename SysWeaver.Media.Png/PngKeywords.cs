namespace SysWeaver.Media.Png
{
    public enum PngKeywords
    {
        /// <summary>
        /// Short (one line) title or caption for image
        /// </summary>
        Title,
        /// <summary>
        /// Name of image's creator
        /// </summary>
        Author,
        /// <summary>
        /// Description of image (possibly long)
        /// </summary>
        Description,
        /// <summary>
        /// Copyright notice
        /// </summary>
        Copyright,
        /// <summary>
        /// Time of original image creation
        /// </summary>
        CreationTime,
        /// <summary>
        /// Software used to create the image
        /// </summary>
        Software,
        /// <summary>
        /// Legal disclaimer
        /// </summary>
        Disclaimer,
        /// <summary>
        /// Warning of nature of content
        /// </summary>
        Warning,
        /// <summary>
        /// Device used to create the image
        /// </summary>
        Source,
        /// <summary>
        /// Miscellaneous comment
        /// XML:com.adobe.xmp   
        /// Extensible Metadata Platform (XMP) information, formatted as required by the XMP specification [XMP]. 
        /// The use of iTXt, with Compression Flag set to 0, and both Language Tag and Translated Keyword set to the null string, are recommended for XMP compliance.
        /// </summary>
        Comment,
        /// <summary>
        /// Name of a collection to which the image belongs. An image may belong to one or more collections, each named by a separate text chunk.
        /// </summary>
        Collection,
    }

}

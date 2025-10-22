namespace SysWeaver.AI
{
    public enum OpenAiTableColumnTypes
    {
        /// <summary>
        /// Column is generic text.
        /// </summary>
        Text,

        /// <summary>
        /// Column data is a floating point number
        /// </summary>
        Float,

        /// <summary>
        /// Column data is an integer number
        /// </summary>
        Integer,

        /// <summary>
        /// Column data is a boolean.
        /// </summary>
        Boolean,

        /// <summary>
        /// Column data is a time stamp (date and time)
        /// </summary>
        DateTime,

        /// <summary>
        /// Column data is an URL to an image (that is displayed)
        /// </summary>
        Image,

        /// <summary>
        /// Column data is an URL (that is displayed as a clickable link)
        /// </summary>
        Link,

        /// <summary>
        /// Column data is an amount string that MUST be a floating point number, a space and an ISO 4217 currency code, ex: "12.50 USD".
        /// </summary>
        Amount,
    }


}

using SimpleStack.Orm.Attributes;
using System;
using System.Text;
using SysWeaver.Auth;

namespace SysWeaver.Chat.MySql
{
    class DbChatMessage
    {
        /// <summary>
        /// Unqiue user id
        /// </summary>
        [PrimaryKey]
        [AutoIncrement]
        public long Id { get; set; }

        /// <summary>
        /// The user/entity who posted this message.
        /// </summary>
        [Required]
        [StringLength(AuhorizationLimits.MaxNickNameLength)]
        public String From { get; set; }

        /// <summary>
        /// Url to an image that represents the user/entity that posted this message
        /// </summary>
        [StringLength(AuhorizationLimits.MaxGuidLength + 64)]
        [Ascii]
        public String FromImage { get; set; }

        /// <summary>
        /// The time when this message was created
        /// </summary>
        [Required]
        [Index]
        public DateTime Time { get; set; }

        /// <summary>
        /// The text
        /// </summary>
        [StringLength(4096)]
        public String Text { get; set; }

        /// <summary>
        /// A link to an url with some data (typically images), can use have multiple data entries using semi colon as a separator
        /// </summary>
        [StringLength(2048)]
        public String Data { get; set; }

        /// <summary>
        /// The format of the supplied text
        /// </summary>
        [Required]
        public Byte Format { get; set; }

        /// <summary>
        /// The language used in this message.
        /// A two letter ISO 639-1 language code or a three letter ISO 639-2 language code, optionally combined with a two letter ISO 3166-A2 country code using a hyphen.
        /// </summary>
        [Required]
        [StringLength(8)]
        [Ascii]
        [Default("en")]
        public String Lang { get; set; }



    }


}

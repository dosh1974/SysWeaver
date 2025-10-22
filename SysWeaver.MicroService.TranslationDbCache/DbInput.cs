using SimpleStack.Orm.Attributes;
using System;
using System.Text;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    [Alias("Inputs")]
    [PartitionByKey(nameof(Hash))]
    public sealed class DbInput
    {
        public const int MaxTextLen = 768;
        public const int MaxContextLen = 768;
        public const int MaxTranslatedLen = 768;
        
        public static readonly long Max = new DateTime(9999, 01, 01).Ticks;


        /// <summary>
        /// Time stamp when translation was performed (not valid if done manually)
        /// </summary>
        [Required]
        [Index]
        public long Time { get; set; }

        /// <summary>
        /// True if this translation was done manually
        /// </summary>
        [Ignore]
        public bool IsManual => Expiration >= Max;

        /// <summary>
        /// The language of the input text
        /// </summary>
        [Index]
        [StringLength(8)]
        [Ascii]
        [Required]
        [TableDataIsoLanguageImage]
        public String From { get; set; }

        /// <summary>
        /// The text to translate.
        /// Text is truncated to at most 768 chars.
        /// </summary>
        [Index]
        [StringLength(MaxTextLen)]
        [Required]
        [TableDataText(60)]
        public String Text { get; set; }

        /// <summary>
        /// The language to translate to
        /// </summary>
        [Index]
        [StringLength(8)]
        [Ascii]
        [Required]
        [TableDataIsoLanguageImage]
        public String To { get; set; }

        /// <summary>
        /// The translated text.
        /// Text is truncated to at most 768 chars.
        /// </summary>
        [Index]
        [StringLength(MaxTranslatedLen)]
        [Required]
        [TableDataText(60)]
        public String Translated { get; set; }

        /// <summary>
        /// The hash key used
        /// </summary>
        [StringLength(128)]
        [Ascii]
        [PrimaryKey]
        [TableDataActions(
            "Edit", "Click to edit the translated text", "&../translator/Edit.html?id={0}", "../icons/edit.svg",
            "Remove", "Click to remove the translated text (will need a new translation)", "../Api/translator/DeleteTranslation?'{0}'", "../icons/close.svg"

            )]
        public String Hash { get; set; }

        /// <summary>
        /// The context used for the translation.
        /// Text is truncated to at most 768 chars.
        /// </summary>
        [Index]
        [StringLength(MaxContextLen)]
        [TableDataText(120)]
        public String Context{ get; set; }


        /// <summary>
        /// Time stamp when translation was performed
        /// </summary>
        [Required]
        [Index]
        [Default(638984160000000000L)]
        public long Expiration { get; set; }

    }




}

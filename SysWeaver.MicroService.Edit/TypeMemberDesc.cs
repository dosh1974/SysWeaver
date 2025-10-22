using System;

namespace SysWeaver.MicroService
{

    public class TypeInstanceDesc
    {
        /// <summary>
        /// Display name (use when displaying)
        /// </summary>
        [AutoTranslate(true)]
        [AutoTranslateContext("This is the title of an editable value")]
        [AutoTranslateContext("The description of the value is \"{0}\"", nameof(Summary))]
        [AutoTranslateContext("Additional remarks for the value is \"{0}\"", nameof(Remarks))]
        public String DisplayName;

        /// <summary>
        /// The minimum allowed value
        /// </summary>
        public String Min;

        /// <summary>
        /// The maximum allowed value
        /// </summary>
        public String Max;

        /// <summary>
        /// The default value
        /// </summary>
        public String Default;

        /// <summary>
        /// Flags
        /// </summary>
        public TypeMemberFlags Flags;

        /// <summary>
        /// Type specific editor params
        /// </summary>
        public String EditParams;

        /// <summary>
        /// Summary comment (from the code)
        /// </summary>
        [AutoTranslate(true)]
        [AutoTranslateContext("This is the description (tool tip) of an editable value")]
        [AutoTranslateContext("The title (name) of the value is \"{0}\"", nameof(DisplayName))]
        [AutoTranslateContext("Additional remarks for the value is \"{0}\"", nameof(Remarks))]
        public String Summary;

        /// <summary>
        /// Remarks comment (from the code)
        /// </summary>
        [AutoTranslate(true)]
        [AutoTranslateContext("This is additional remarks of an editable value")]
        [AutoTranslateContext("The title (name) of the value is \"{0}\"", nameof(DisplayName))]
        [AutoTranslateContext("The description of the value is \"{0}\"", nameof(Summary))]
        public String Remarks;

        /// <summary>
        /// Editor type, null or empty for default
        /// </summary>
        public String Type;

        /// <summary>
        /// For dictionaries and sets, this is the instance data (optional)
        /// </summary>
        [EditAllowNull]
        [EditDefault(null)]
        public TypeInstanceDesc KeyInst;

        /// <summary>
        /// For dictionaries, lists, arrays and objects, this is the instance data (optional)
        /// </summary>
        [EditAllowNull]
        [EditDefault(null)]
        public TypeInstanceDesc ElementInst;
    }

    public class TypeDescBase : TypeInstanceDesc
    {
        public override string ToString() =>
            DisplayName != null
            ?
            String.Concat(TypeName, " [", DisplayName, ']')
            :
            String.Concat(TypeName);


        /// <summary>
        /// Name of the type
        /// </summary>
        public String TypeName;

        /// <summary>
        /// Element member (for collections)
        /// </summary>
        public String ElementTypeName;

        /// <summary>
        /// Key
        /// </summary>
        public String KeyTypeName;

        /// <summary>
        /// External link to information about the type
        /// </summary>
        public String Ext;
    }


    /// <summary>
    /// Represents a public instance member of a type
    /// </summary>
    public sealed class TypeMemberDesc : TypeDescBase
    {
        public override string ToString() =>
            DisplayName != null
            ?
            String.Concat(TypeName, ' ', Name, " [", DisplayName, ']')
            :
            String.Concat(TypeName, ' ', Name);

        /// <summary>
        /// Name of the property / field (use for referencing)
        /// </summary>
        public String Name;



    }


}

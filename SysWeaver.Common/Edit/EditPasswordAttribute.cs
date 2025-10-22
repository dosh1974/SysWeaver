using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the value input should be masked (password)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditPasswordAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the value input should be masked (password)
        /// </summary>
        /// <param name="isPassword">True to allow for multi line</param>
        public EditPasswordAttribute(bool isPassword = true)
        {
            IsPassword = isPassword;
        }
        public readonly bool IsPassword;
    }


    /// <summary>
    /// Put this to specify the value should be hidden
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditHideAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the value should be hidden
        /// </summary>
        /// <param name="hide">True to hide this member</param>
        public EditHideAttribute(bool hide = true)
        {
            Hide = hide;
        }
        public readonly bool Hide;
    }


    /// <summary>
    /// Put this to specify the value should be read only
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditReadOnlyAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the value should be read only
        /// </summary>
        /// <param name="readOnly">True to make this member read only</param>
        public EditReadOnlyAttribute(bool readOnly = true)
        {
            ReadOnly = readOnly;
        }
        public readonly bool ReadOnly;
    }

}

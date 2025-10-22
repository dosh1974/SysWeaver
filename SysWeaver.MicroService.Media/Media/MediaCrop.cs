using System;

namespace SysWeaver.MicroService.Media
{
    /// <summary>
    /// Crop information
    /// </summary>
    public class MediaCrop : IEquatable<MediaCrop>
    {
        const string HideAbs = "this." + nameof(MarginAsPercentage);
        const string HideP = "!this." + nameof(MarginAsPercentage);


        /// <summary>
        /// Use a percentage instead of absolute pixels for margins
        /// </summary>
        public bool MarginAsPercentage;

        /// <summary>
        /// Number of pixels on the top to "remove"
        /// </summary>
        [EditMin(0)]
        [EditDefault(0)]
        [EditDisplayName("Margin top")]
        [EditHideIf(HideAbs)]
        public int MarginTop;
        /// <summary>
        /// Number of pixels on the right to "remove"
        /// </summary>
        [EditMin(0)]
        [EditDefault(0)]
        [EditDisplayName("Margin right")]
        [EditHideIf(HideAbs)]
        public int MarginRight;
        /// <summary>
        /// Number of pixels on the bottom to "remove"
        /// </summary>
        [EditMin(0)]
        [EditDefault(0)]
        [EditDisplayName("Margin bottom")]
        [EditHideIf(HideAbs)]
        public int MarginBottom;
        /// <summary>
        /// Number of pixels on the left to "remove"
        /// </summary>
        [EditMin(0)]
        [EditDefault(0)]
        [EditDisplayName("Margin left")]
        [EditHideIf(HideAbs)]
        public int MarginLeft;

        /// <summary>
        /// A percentage of the vertical pixels to "remove" on the top
        /// </summary>
        [EditRange(0, 100)]
        [EditSlider]
        [EditDefault(0)]
        [EditDisplayName("Margin top")]
        [EditHideIf(HideP)]
        public float MarginTopP;
        /// <summary>
        /// A percentage of the horizontal pixels to "remove" on the right
        /// </summary>
        [EditRange(0, 100)]
        [EditSlider]
        [EditDefault(0)]
        [EditDisplayName("Margin right")]
        [EditHideIf(HideP)]
        public float MarginRightP;
        /// <summary>
        /// A percentage of the vertical pixels to "remove" on the bottom
        /// </summary>
        [EditRange(0, 100)]
        [EditSlider]
        [EditDefault(0)]
        [EditDisplayName("Margin bottom")]
        [EditHideIf(HideP)]
        public float MarginBottomP;
        /// <summary>
        /// A percentage of the horizontal pixels to "remove" on the left
        /// </summary>
        [EditRange(0, 100)]
        [EditSlider]
        [EditDefault(0)]
        [EditDisplayName("Margin left")]
        [EditHideIf(HideP)]
        public float MarginLeftP;

        /// <summary>
        /// Enable to visualize the margins (only on the media edit page)
        /// </summary>
        public bool VisualizeMargins;

        public virtual void Validate()
        {
            var i = MarginTop;
            MarginTop = i < 0 ? 0 : i;
            i = MarginRight;
            MarginRight = i < 0 ? 0 : i;
            i = MarginBottom;
            MarginBottom = i < 0 ? 0 : i;
            i = MarginLeft;
            MarginLeft = i < 0 ? 0 : i;
            var f = MarginTopP;
            MarginTopP = f < 0 ? 0 : f > 100 ? 100 : f;
            f = MarginRightP;
            MarginRightP = f < 0 ? 0 : f > 100 ? 100 : f;
            f = MarginTopP;
            MarginTopP = f < 0 ? 0 : f > 100 ? 100 : f;
            f = MarginLeftP;
            MarginLeftP = f < 0 ? 0 : f > 100 ? 100 : f;
        }

        public override int GetHashCode() => HashCode.Combine(MarginAsPercentage, HashCode.Combine(MarginTop, MarginRight, MarginBottom, MarginLeft, MarginTopP, MarginRightP, MarginBottomP, MarginLeftP));

        public override bool Equals(object obj) => Equals(obj as MediaCrop);

        public bool Equals(MediaCrop other)
        {
            if (other == null)
                return false;
            if (MarginAsPercentage != other.MarginAsPercentage)
                return false;
            if (MarginTop != other.MarginTop)
                return false;
            if (MarginRight != other.MarginRight)
                return false;
            if (MarginBottom != other.MarginBottom)
                return false;
            if (MarginLeft != other.MarginLeft)
                return false;
            if (MarginTopP != other.MarginTopP)
                return false;
            if (MarginRightP != other.MarginRightP)
                return false;
            if (MarginBottomP != other.MarginBottomP)
                return false;
            if (MarginLeftP != other.MarginLeftP)
                return false;
            return true;
        }

    }
}

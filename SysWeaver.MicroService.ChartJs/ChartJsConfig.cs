using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    public sealed class ChartJsConfig
    {
        /// <summary>
        /// The type of chart to generate
        /// </summary>
        [OpenAiOptional]
        public string type = "bar";

        /// <summary>
        /// The data of the chart
        /// </summary>
        public ChartJsData data;

        /// <summary>
        /// Options for displaying the data
        /// </summary>
        [OpenAiOptional]
        public ChartJsOptions options;

        /// <summary>
        /// The refresh rate in ms of this chart
        /// </summary>
        [OpenAiIgnore]
        public int RefreshRate;

        /// <summary>
        /// What chart types this can be interchanged with
        /// </summary>
        [OpenAiIgnore]
        public string[] ValidTypes;


        /// <summary>
        /// Number of decimals to use
        /// </summary>
        [OpenAiOptional]
        public int Precision = -2;


        /// <summary>
        /// The title of this chart, used as filename etc.
        /// Max length is 64.
        /// </summary>
        [OpenAiOptional]
        [AutoTranslate]
        [AutoTranslateContext("This is the title of chart image, it will also be used when saving the chart as a file, do NOT user any invalid chars")]
        public String Title;


        /// <summary>
        /// The prefix string for a value label
        /// </summary>
        [OpenAiOptional]
        public String ValuePrefix = "";

        /// <summary>
        /// The suffix string for a value label
        /// </summary>
        [OpenAiOptional]
        public String ValueSuffix = "";

        /// <summary>
        /// Value label control:
        /// 0 = Value.
        /// 1 = Label and Value.
        /// 2 = Label.
        /// 3 = First line of the label.
        /// </summary>
        [OpenAiOptional]
        public int ValueLabel;


    }




}

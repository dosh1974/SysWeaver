using System;

namespace SysWeaver.AI
{

    /// <summary>
    /// The reasoning (thinking) effort for models that support reasoning
    /// </summary>
    public enum AiReasoning
    {
        /// <summary>
        /// No reasoning (if the model supports disabling it)
        /// </summary>
        None,
        /// <summary>
        /// The least reasoning the model supports
        /// </summary>
        Minimal,
        /// <summary>
        /// Low reasoning effort
        /// </summary>
        Low,
        /// <summary>
        /// Medium reasoning effort
        /// </summary>
        Medium,
        /// <summary>
        /// High reasoning effort
        /// </summary>
        High,
        /// <summary>
        /// Extra high reasoning effort
        /// </summary>
        ExtraHigh,
        /// <summary>
        /// The most reasoning the model supports
        /// </summary>
        Max,
    }


    /// <summary>
    /// Common (API agnostic) parameters for an AI service
    /// </summary>
    public abstract class AiServiceParams : ApiKeyParams
    {
        #region API setup

        /// <summary>
        /// The service endpoint that the client will send requests to. If not set, the default endpoint will be used.
        /// </summary>
        [EditType(EditTypes.Url)]
        public String EndPoint;

        /// <summary>
        /// The network time out in seconds
        /// </summary>
        public int NetworkTimeoutSeconds = 5 * 60;

        #endregion// API setup


        #region Chat

        /// <summary>
        /// The default model to use for chat
        /// </summary>
        public String DefaultChatModel;

        /// <summary>
        /// The name of chat's using this chat provider
        /// </summary>
        public String ChatName = "Ai";

        /// <summary>
        /// The reasoning effort for models that support reasoning
        /// </summary>
        public AiReasoning DefaultReasoning = AiReasoning.Low;

        /// <summary>
        /// Maximum number of concurrent chat requests at the same time
        /// </summary>
        public int MaxConcurrentChats = 32;

        #endregion//Chat


        #region Image

        /// <summary>
        /// The default model to use for image generation (text to image).
        /// If null or empty the service default is used.
        /// If the model isn't available from the API (anymore), the most suitable available model is used instead.
        /// </summary>
        public String DefaultImageModel;

        /// <summary>
        /// The default model to use for image editing (image to image).
        /// If null or empty the service default is used.
        /// If the model isn't available from the API (anymore), the most suitable available model is used instead.
        /// </summary>
        public String DefaultImageEditModel;

        /// <summary>
        /// The default width in pixels of a generated image (used when no width is specified).
        /// </summary>
        public int DefaultImageWidth = 1280;

        /// <summary>
        /// The default height in pixels of a generated image (used when no height is specified).
        /// </summary>
        public int DefaultImageHeight = 720;

        /// <summary>
        /// Maximum number of concurrent images being generated at the same time
        /// </summary>
        public int MaxConcurrentImages = 4;

        #endregion//Image

    }

    /// <summary>
    /// Common (API agnostic) parameters for an AI session
    /// </summary>
    public class AiSessionParams
    {
        /// <summary>
        /// The model to use for this session.
        /// If null or empty, the service default will be used.
        /// </summary>
        public String Model;

        /// <summary>
        /// The reasoning effort for models that support reasoning.
        /// Null to use default.
        /// </summary>
        public AiReasoning? Reasoning;
    }

}

using System;

namespace SysWeaver.AI
{

    /// <summary>
    /// The reasoning (thinking) effort for models that support reasoning
    /// </summary>
    public enum AiReasoning
    {
        None,
        Minimal,
        Low,
        Medium,
        High,
        ExtraHigh,
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
        /// The default model to use for image generation
        /// </summary>
        public String DefaultImageModel;

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

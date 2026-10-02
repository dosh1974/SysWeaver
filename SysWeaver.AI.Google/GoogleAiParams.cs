using System;

namespace SysWeaver.AI
{
    public enum GoogleServiceTier
    {
        /// <summary>
        /// Use the API default (don't send a service tier)
        /// </summary>
        Default,
        /// <summary>
        /// Lower cost, higher latency
        /// </summary>
        Flex,
        /// <summary>
        /// Standard processing
        /// </summary>
        Standard,
        /// <summary>
        /// Prioritized processing (higher cost)
        /// </summary>
        Priority,
    }

    /// <summary>
    /// Parameters for the Google (Gemini) AI service.
    /// See: https://ai.google.dev/api
    /// </summary>
    public sealed class GoogleAiParams : AiServiceParams
    {
        public GoogleAiParams()
        {
            DefaultChatModel = "gemini-3.8-flash";
            DefaultImageModel = "gemini-3.1-flash-lite-image";
        }

        #region API setup

        /// <summary>
        /// Set to true to use the Vertex AI API instead of the Gemini Developer API.
        /// When using Vertex AI, set Project and Location (or use an express mode API key).
        /// </summary>
        public bool VertexAI;

        /// <summary>
        /// The Google Cloud project (Vertex AI only)
        /// </summary>
        public String Project;

        /// <summary>
        /// The Google Cloud location, ex: "us-central1" (Vertex AI only)
        /// </summary>
        public String Location;

        /// <summary>
        /// Optional API version, ex: "v1beta" (null for the SDK default)
        /// </summary>
        public String ApiVersion;

        #endregion// API setup

        #region Chat

        /// <summary>
        /// The default service tier to use for chat
        /// </summary>
        public GoogleServiceTier DefaultTier = GoogleServiceTier.Default;

        #endregion//Chat
    }


    /// <summary>
    /// Parameters for a Google (Gemini) session.
    /// Model examples:
    /// "gemini-2.5-flash" - Fast and cost efficient, supports thinking.
    /// "gemini-2.5-pro" - Advanced reasoning model.
    /// "gemini-3-pro-preview" - The most capable model.
    /// </summary>
    public class GoogleSessionParams : AiSessionParams
    {
        /// <summary>
        /// The service tier to use, null to use the service default
        /// </summary>
        public GoogleServiceTier? Tier;

        /// <summary>
        /// Get Google session parameters from some (possibly API agnostic) session parameters
        /// </summary>
        /// <param name="p">The parameters, can be null</param>
        /// <returns>The same instance if it's already a GoogleSessionParams, else a new instance with the common values copied</returns>
        public static GoogleSessionParams From(AiSessionParams p)
        {
            if (p == null)
                return null;
            if (p is GoogleSessionParams gp)
                return gp;
            return new GoogleSessionParams
            {
                Model = p.Model,
                Reasoning = p.Reasoning,
            };
        }
    }

}

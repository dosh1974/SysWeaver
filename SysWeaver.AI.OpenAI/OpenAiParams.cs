using System;

namespace SysWeaver.AI
{
    public sealed class OpenAiParams : ApiKeyParams
    {
        #region API setup

        /// <summary>
        /// The service endpoint that the client will send requests to. If not set, the default endpoint will be used.
        /// Examples:
        /// "https://generativelanguage.googleapis.com/v1beta/openai/"
        /// "http://192.168.1.112:1234/v1"
        /// </summary>
        public String EndPoint;

        /// <summary>
        /// The value to use for the OpenAI-Organization request header. Users who belong to multiple organizations can set this value to specify which organization is used for an API request. 
        /// Usage from these API requests will count against the specified organization's quota. 
        /// If not set, the header will be omitted, and the default organization will be billed. 
        /// You can change your default organization in your user settings.
        /// </summary>
        public String OrganizationId;

        /// <summary>
        /// An optional application ID to use as part of the request User-Agent header.
        /// </summary>
        public String UserAgentApplicationId;
        
        /// <summary>
        /// The value to use for the OpenAI-Project request header. 
        /// Users who are accessing their projects through their legacy user API key can set this value to specify which project is used for an API request. 
        /// Usage from these API requests will count as usage for the specified project. 
        /// If not set, the header will be omitted, and the default project will be accessed.
        /// </summary>
        public String ProjectId;

        /// <summary>
        /// The network time out in seconds
        /// </summary>
        public int NetworkTimeoutSeconds = 5 * 60;


        #endregion// API setup


        #region Chat

        /// <summary>
        /// The default model to use for chat, ex:
        /// "gpt-4o-mini" -	Our affordable and intelligent small model for fast, lightweight tasks.
        /// "gpt-4o" - Our high-intelligence flagship model for complex, multi-step tasks.
        /// "o1-preview" - Language models trained with reinforcement learning to perform complex reasoning.
        /// </summary>
        public String DefaultChatModel = "gpt-4.1";

        /// <summary>
        /// The name of chat's using this chat provider
        /// </summary>
        public String ChatName = "OpenAI";

        /// <summary>
        /// Maximum number of concurrent chat requests at the same time
        /// </summary>
        public int MaxConcurrentChats = 32;

        #endregion//Chat


        #region Image


        /// <summary>
        /// The default model to use for image gen, ex:
        /// "dall-e-3"
        /// </summary>
        public String DefaultImageModel = "dall-e-3";

        /// <summary>
        /// Maximum number of concurrent images being generated at the same time
        /// </summary>
        public int MaxConcurrentImages = 4;


        #endregion//Image
        
        #region Token 


        /// <summary>
        /// The path of downloaded token files, see:
        /// https://github.com/aiqinxuancai/TiktokenSharp
        /// Can use path variables, ex:
        ///             $(CommonApplicationData) = The directory that serves as a common repository for application-specific data that is used by all users.
        ///             $(LocalApplicationData) = The directory that serves as a common repository for application-specific data that is used by the current, non-roaming user.
        ///             $(ApplicationData) = The directory that serves as a common repository for application-specific data for the current roaming user (typically settings that should be shared between systems).
        ///             $(MyPictures) = The My Pictures folder.
        ///             $(AppName) = Application name.
        ///             $(Executable) = Full path to the executable.
        ///             $(ExecutableDir) = ExecutableDir.
        ///             $(ExecutableBase) = Full path to the folder where the application is.
        /// </summary>
        public String TokenCacheFolder = @"$(CommonApplicationData)\SysWeaver_Tiktoken\";

        /// <summary>
        /// List of models or token encoding algorithms to download cache on load (as opposed to on use)
        /// </summary>
        public String[] CacheTokensFor;// = [ "gpt-4.1" ];

        #endregion//Token 


    }

}

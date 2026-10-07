using System;

namespace SysWeaver.AI
{

    public enum OpenAiServiceTier
    {
        Auto,
        Default,
        Flex,
        Scale,

        Fast,
    }

    /// <summary>
    /// Parameters for the OpenAI service.
    /// EndPoint examples:
    /// "https://generativelanguage.googleapis.com/v1beta/openai/"
    /// "http://192.168.1.112:1234/v1"
    /// </summary>
    public sealed class OpenAiParams : AiServiceParams
    {
        public OpenAiParams()
        {
            DefaultChatModel = "gpt-4.1";
            DefaultImageModel = "dall-e-3";
        }

        #region API setup

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

        #endregion// API setup


        #region Chat

        /// <summary>
        /// The default service tier to use for chat
        /// </summary>
        public OpenAiServiceTier DefaultTier = OpenAiServiceTier.Auto;

        /// <summary>
        /// The default API used to implement chat sessions (can be overridden per session using OpenAiSessionParams.ChatApi)
        /// </summary>
        public OpenAiChatApi DefaultChatApi = OpenAiChatApi.Responses;

        #endregion//Chat


        #region Token


        /// <summary>
        /// The path of downloaded token files, see:
        /// https://github.com/aiqinxuancai/TiktokenSharp
        /// Can use path variables, env variables, ex:
        ///             $(CommonApplicationData) = The directory that serves as a common repository for application-specific data that is used by all users.
        ///             $(LocalApplicationData) = The directory that serves as a common repository for application-specific data that is used by the current, non-roaming user.
        ///             $(ApplicationData) = The directory that serves as a common repository for application-specific data for the current roaming user (typically settings that should be shared between systems).
        ///             $(MyPictures) = The My Pictures folder.
        ///             $(Executable) = Full path to the executable, ex: "C:\MyServices\MyService.exe"
        ///             $(ExeAppName) = Name of the executable, ex: "MyService" (this can be different from AppName)
        ///             $(ExecutableDir) = ExecutableDir, ex: "C:\MyServices"
        ///             $(ExecutableBase) = Full path to the executable, excluding it's extensions, ex: "C:\MyServices\MyService"
        ///             $(AppName) = Application name (defaults to exe app name, can be changed in config), ex: "MyService".
        ///             $(AppGuid) = A "unique" id for this process
        ///             $(AppDisplayName) = Friendly application name (defaults to de-camel cased exe app name, can be changed in config), ex: "My service".
        ///             $(MachineName) = Machine name, ex: "DESKTOP-324VHA".
        ///             $(KeyFolder) = The folder where keys are stored. ex: "C:\Keys".
        /// </summary>
        public String TokenCacheFolder = @"$(CommonApplicationData)/SysWeaver_Tiktoken/";

        /// <summary>
        /// List of models or token encoding algorithms to download cache on load (as opposed to on use)
        /// </summary>
        public String[] CacheTokensFor;// = [ "gpt-4.1" ];

        #endregion//Token


    }

}

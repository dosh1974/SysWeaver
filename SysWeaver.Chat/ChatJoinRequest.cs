using System;

namespace SysWeaver.Chat
{
    /// <summary>
    /// A request to poll messages from a given chat session
    /// </summary>
    public sealed class ChatJoinRequest : ChatBaseRequest
    {
        /// <summary>
        /// The first message to retrieve (inclusive), or the last message to retrieve (exclusive) if MaxCount is less than zero.
        /// If from id is less or equal to zero, the total message count is added.
        /// </summary>
        public long FromId;

        /// <summary>
        /// The maximum number of messages to retrieve.
        /// If negative, messages up until the FromId ís retrieved.
        /// </summary>
        [EditRange(-200, 200)]
        public int MaxCount;
    }

    /// <summary>
    /// A request to translate a chat message
    /// </summary>
    public sealed class ChatTranslateRequest : ChatBaseRequest
    {
        /// <summary>
        /// Id of the message
        /// </summary>
        public long MessageId;
    }

    public sealed class ChatJoinResponse
    {
        /// <summary>
        /// The name of the currently logged in user
        /// </summary>
        public String UserName;

        /// <summary>
        /// The guid of the currently logged in user
        /// </summary>
        public String UserGuid;

        /// <summary>
        /// The path part of the storage that belongs to the logged in user
        /// </summary>
        public String UserStore;

        /// <summary>
        /// The language currently selected by the user
        /// </summary>
        public String Lang;

        /// <summary>
        /// The maximum length of the text
        /// </summary>
        public int MaxTextLength = 4096;

        /// <summary>
        /// The maximum length of the data
        /// </summary>
        public int MaxDataLength = 1024;

        /// <summary>
        /// The maximum number of data items
        /// </summary>
        public int MaxDataCount = 10;

        /// <summary>
        /// The messages
        /// </summary>
        public ChatMessage[] Messages;

        /// <summary>
        /// True if this user can clear the chat
        /// </summary>
        public bool CanClear;

        /// <summary>
        /// If true, the clear operation don't pop-up a confirmation
        /// </summary>
        public bool DoNotConfirmClear;


        /// <summary>
        /// What messages this user can remove
        /// </summary>
        public ChatRemoveMessages CanRemove;

        /// <summary>
        /// Optional menus, 
        /// </summary>
        public ChatMenuItem[] Menus;

        /// <summary>
        /// If this is non-null speech input will be enabled, listening to this name.
        /// Every string can optionally have a prefix that is added to the spoken text, by adding a | and the text, ex:
        /// "All|-" will add the "-" prefix to any spoken text.
        /// </summary>
        public String[] SpeechName = [ "All" ];

        /// <summary>
        /// Array of user to voice mappings
        /// </summary>
        public ChatVoice[] Voices;

        /// <summary>
        /// If true, speech should be enabled by default
        /// </summary>
        public bool EnableSpeechByDefault;

        /// <summary>
        /// If true, the user may input markdown text (client is allowed to send the message with the MarkDown format).
        /// </summary>
        public bool AllowMarkDown;

        /// <summary>
        /// If true, the user can store images and other data (create persistant links to it).
        /// </summary>
        public bool CanStore;

        /// <summary>
        /// If true, the server supports message translation (to the users language)
        /// </summary>
        public bool CanTranslate;

        /// <summary>
        /// If true, enable the menu option to show a user profile
        /// </summary>
        public bool CanShowProfile;

        /// <summary>
        /// If non-empty and a IUserStorage is available, files can be uploaded
        /// </summary>
        public String UploadRepo;

        /// <summary>
        /// If true, the user can store files in chat in a way that anyone can look at them
        /// </summary>
        public bool AllowPublicStore;


    }

}

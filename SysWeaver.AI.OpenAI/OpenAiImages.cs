namespace SysWeaver.AI
{
    /// <summary>
    /// A predefined set of images that can be shown and used in projects.
    /// </summary>
    enum OpenAiImages
    {
        /// <summary>
        /// The logo of the application that hosts the chat session as an SVG file (this not a company logo).
        /// Don't use unless the user expclitly asks for the application logo.
        /// </summary>
        ApplicationLogo,

        /// <summary>
        /// The icon of the application that hosts the chat session as an SVG file (this not a company logo).
        /// Don't use unless the user expclitly asks for the application icon.
        /// </summary>
        ApplicationIcon,
        
        /// <summary>
        /// The avatar image of the AI agent.
        /// This is the image that is shown as YOU, the agent.
        /// </summary>
        AgentLogo,

        /// <summary>
        /// An animated smiley emoji that is angry.
        /// </summary>
        AngrySmiley,

        /// <summary>
        /// An animated smiley emoji that is happy, laughing until crying.
        /// </summary>
        HappySmiley,

        /// <summary>
        /// An animated smiley emoji that is loveful, hearts popping out of it's eyes.
        /// </summary>
        LoveSmiley,

        /// <summary>
        /// An animated smiley emoji that is sad.
        /// </summary>
        SadSmiley,

        /// <summary>
        /// An animated smiley emoji that is teasing.
        /// </summary>
        TeasingSmiley,
    }


}

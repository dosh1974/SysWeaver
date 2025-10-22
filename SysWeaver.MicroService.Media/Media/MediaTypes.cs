namespace SysWeaver.MicroService.Media
{
    public enum MediaTypes
    {
        /// <summary>
        /// The Link is an image somewhere on internet, ex: "https://i.imgur.com/HP284IZ.jpeg"
        /// </summary>
        Image,
        /// <summary>
        /// The Link is a video somewhere on internet, ex: "https://dl6.webmfiles.org/big-buck-bunny_trailer.webm"
        /// </summary>
        Video,
        /// <summary>
        /// The Link is a audio clip somewhere on internet, ex: "https://file-examples.com/storage/fe3b38ec0965f308fab1fff/2017/11/file_example_MP3_2MG.mp3"
        /// </summary>
        Audio,
        /// <summary>
        /// The Link is the youtube video code, ex: "q6L82zI1_D0" (must be available to the public).
        /// </summary>
        YouTube,
        /// <summary>
        /// The Link is an effect file somewhere on the internet, ex: "https://www.quizzweaver.com/Data/Cloud.glsl"
        /// </summary>
        Effect,


    }
}

using System;
using System.Text;

namespace SysWeaver.AI
{



    public sealed class OpenAiToolContext : IOpenAiToolContext
    {
        public readonly OpenAiChatSession Session;


        public void SetProperty<T>(String key, T value) 
            => Session.SetProperty(key, value);

        public bool TryGetProperty<T>(String key, out T value) 
            => Session.TryGetProperty(key, out value);

        public bool TryRemoveProperty<T>(String key, out T value)
            => Session.TryRemoveProperty(key, out value);


        /// <summary>
        /// Add a link to something (displayed in chat)
        /// </summary>
        /// <param name="url">The local or absolute url to the file</param>
        public void AddLink(String url)
        {
            var link = Link;
            lock (link)
            {
                if (link.Length > 0)
                    link.Append(';');
                link.Append(url);
            }
        }

        /// <summary>
        /// Attach some data as a file to a message
        /// </summary>
        /// <param name="mime">The mimetype of the file</param>
        /// <param name="data">The data of the file</param>
        /// <param name="filename">The data of the file</param>
        /// <returns>The local url to the file</returns>
        public String AddMessageFile(String mime, String data, String filename)
            => SaveFile(mime, data, filename);


        /// <summary>
        /// Attach some data as a file to a message
        /// </summary>
        /// <param name="mime">The mimetype of the file</param>
        /// <param name="data">The data of the file</param>
        /// <param name="filename">The data of the file</param>
        /// <returns>The local url to the file</returns>
        public String AddMessageFile(String mime, ReadOnlyMemory<Byte> data, String filename)
        {
            var x = String.Concat("data:", mime, ";base64," + Convert.ToBase64String(data.Span));
            return SaveFile(mime, x, filename);
        }

        /// <summary>
        /// Atach some data to a message
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public String AddMessageData(Object data)
            => SaveData(data);

        readonly StringBuilder Link;
        readonly Func<String, String, String, String> SaveFile;
        readonly Func<Object, String> SaveData;
        internal OpenAiToolContext(OpenAiChatSession session, StringBuilder link, Func<String, String, String, String> saveFile, Func<Object, String> saveData)
        {
            Session = session;
            Link = link;
            SaveFile = saveFile;
            SaveData = saveData;
        }

    }

}

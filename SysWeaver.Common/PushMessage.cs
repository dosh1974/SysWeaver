using System;


namespace SysWeaver
{

    /// <summary>
    /// A message that the server pushes to connected clients (browser sessions), ex: "reload", "server.shutdown" or "user.logout".
    /// Derive from this class (or use one of the typed variants) to include a payload, the instance is serialized (JSON) to the client.
    /// </summary>
    /// <remarks>
    /// The HTTP server lower cases <see cref="Type"/> in place when the message is queued, and may coalesce queued messages with the same type
    /// (keeping only the latest). Instances may be shared between sessions, so don't mutate them after they've been pushed.
    /// </remarks>
    public class PushMessage
    {
#if DEBUG
        public override string ToString() => String.Concat(this.GetType().Name, " \"", Type, '"');
#endif//DEBUG

        /// <summary>
        /// The message type (name), used by the client to dispatch the message (case insensitive).
        /// </summary>
        public String Type;

        /// <summary>
        /// Create a message without a type (used by serializers and derived classes).
        /// </summary>
        public PushMessage()
        {
        }

        /// <summary>
        /// Create a message of the specified type.
        /// </summary>
        /// <param name="type">The message type (name).</param>
        public PushMessage(string type)
        {
            Type = type;
        }
    }

    /// <summary>
    /// A <see cref="PushMessage"/> with a string payload.
    /// </summary>
    public class PushMessageStringValue : PushMessage
    {
#if DEBUG
        public override string ToString() => String.Join(" = ", base.ToString(), Value);
#endif//DEBUG


        /// <summary>
        /// The payload.
        /// </summary>
        public String Value;

        /// <summary>
        /// Create a message with a string payload.
        /// </summary>
        /// <param name="type">The message type (name).</param>
        /// <param name="value">The payload.</param>
        public PushMessageStringValue(string type, string value)
        {
            Type = type;
            Value = value;
        }
    }

    /// <summary>
    /// A <see cref="PushMessage"/> with an integer payload.
    /// </summary>
    public class PushMessagIntValue : PushMessage
    {
#if DEBUG
        public override string ToString() => String.Join(" = ", base.ToString(), Value);
#endif//DEBUG


        /// <summary>
        /// The payload.
        /// </summary>
        /// <remarks>Note that JavaScript clients can only represent integers up to 2^53 exactly.</remarks>
        public long Value;

        /// <summary>
        /// Create a message with an integer payload.
        /// </summary>
        /// <param name="type">The message type (name).</param>
        /// <param name="value">The payload.</param>
        public PushMessagIntValue(string type, long value)
        {
            Type = type;
            Value = value;
        }
    }

    /// <summary>
    /// A <see cref="PushMessage"/> with a floating point payload.
    /// </summary>
    public class PushMessagNumberValue: PushMessage
    {
#if DEBUG
        public override string ToString() => String.Join(" = ", base.ToString(), Value);
#endif//DEBUG


        /// <summary>
        /// The payload.
        /// </summary>
        public double Value;

        /// <summary>
        /// Create a message with a floating point payload.
        /// </summary>
        /// <param name="type">The message type (name).</param>
        /// <param name="value">The payload.</param>
        public PushMessagNumberValue(string type, double value)
        {
            Type = type;
            Value = value;
        }
    }


    /// <summary>
    /// A <see cref="PushMessage"/> with a string array payload (ex: "FileReload" with a list of urls).
    /// </summary>
    public class PushMessageStringArrayValue : PushMessage
    {
#if DEBUG
        public override string ToString() => String.Concat(base.ToString(), " = [", String.Join(", ", Value ?? []), "]");
#endif//DEBUG


        /// <summary>
        /// The payload (the array is not copied).
        /// </summary>
        public String[] Value;

        /// <summary>
        /// Create a message with a string array payload.
        /// </summary>
        /// <param name="type">The message type (name).</param>
        /// <param name="value">The payload (the array is stored as is, not copied).</param>
        public PushMessageStringArrayValue(string type, params string[] value)
        {
            Type = type;
            Value = value;
        }
    }

}

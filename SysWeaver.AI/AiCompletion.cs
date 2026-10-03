using System;
using System.Collections.Generic;
using System.Threading;

namespace SysWeaver.AI
{
    /// <summary>
    /// The role of a message in a completion request
    /// </summary>
    public enum AiCompletionRoles
    {
        /// <summary>
        /// A message from the user
        /// </summary>
        User,
        /// <summary>
        /// A message from the model (can contain tool calls)
        /// </summary>
        Assistant,
        /// <summary>
        /// The result of a tool call
        /// </summary>
        Tool,
    }

    /// <summary>
    /// How the model should use the tools of a completion request
    /// </summary>
    public enum AiCompletionToolChoices
    {
        /// <summary>
        /// The model decides if a tool should be called
        /// </summary>
        Auto,
        /// <summary>
        /// The model may not call any tools
        /// </summary>
        None,
        /// <summary>
        /// The model must call at least one tool
        /// </summary>
        Required,
    }

    /// <summary>
    /// Why the model stopped generating
    /// </summary>
    public enum AiCompletionFinishReasons
    {
        /// <summary>
        /// The model completed the response
        /// </summary>
        Stop,
        /// <summary>
        /// The maximum number of output tokens was reached
        /// </summary>
        Length,
        /// <summary>
        /// The model wants the caller to execute some tool calls
        /// </summary>
        ToolCalls,
    }

    /// <summary>
    /// A tool call made by the model, the caller executes it and adds the result as a Tool message
    /// </summary>
    public sealed class AiCompletionToolCall
    {
        public override string ToString() => String.Concat(Name, "(", Arguments, ")");

        /// <summary>
        /// A unique id of the call (used to match the result)
        /// </summary>
        public String Id;

        /// <summary>
        /// The name of the tool (function)
        /// </summary>
        public String Name;

        /// <summary>
        /// The arguments as a json object
        /// </summary>
        public String Arguments;
    }

    /// <summary>
    /// A message in a completion request
    /// </summary>
    public sealed class AiCompletionMessage
    {
        public override string ToString() => String.Concat(Role.ToString(), ": ", Text);

        /// <summary>
        /// The role of the message
        /// </summary>
        public AiCompletionRoles Role;

        /// <summary>
        /// The text content of the message (can be null for assistant messages with tool calls)
        /// </summary>
        public String Text;

        /// <summary>
        /// Optional name of the participant (user messages) or the tool (tool messages)
        /// </summary>
        public String Name;

        /// <summary>
        /// Tool calls made by the model (assistant messages only)
        /// </summary>
        public List<AiCompletionToolCall> ToolCalls;

        /// <summary>
        /// The id of the tool call this is the result of (tool messages only)
        /// </summary>
        public String ToolCallId;
    }

    /// <summary>
    /// An API agnostic (stateless) completion request, the whole conversation is supplied.
    /// Tools are defined by the caller, tool calls are returned to the caller (not executed).
    /// </summary>
    public sealed class AiCompletionRequest
    {
        /// <summary>
        /// The model to use, null or empty for the service default
        /// </summary>
        public String Model;

        /// <summary>
        /// The system prompt (instructions), can be null
        /// </summary>
        public String SystemPrompt;

        /// <summary>
        /// The conversation
        /// </summary>
        public List<AiCompletionMessage> Messages = new List<AiCompletionMessage>();

        /// <summary>
        /// Tools that the model can call, can be null
        /// </summary>
        public List<AiToolFunction> Tools;

        /// <summary>
        /// How the model should use the tools
        /// </summary>
        public AiCompletionToolChoices ToolChoice;

        /// <summary>
        /// The temperature, null for the service default
        /// </summary>
        public float? Temperature;

        /// <summary>
        /// The maximum number of output tokens, null for the service default
        /// </summary>
        public int? MaxTokens;

        /// <summary>
        /// The reasoning effort, null for the service default
        /// </summary>
        public AiReasoning? Reasoning;

        /// <summary>
        /// Can be used to cancel the request
        /// </summary>
        public CancellationToken Cancel;
    }

    /// <summary>
    /// The result of a completion request
    /// </summary>
    public sealed class AiCompletionResult
    {
        public override string ToString() => String.Concat(Model, ": ", Text);

        /// <summary>
        /// The model that was used
        /// </summary>
        public String Model;

        /// <summary>
        /// The text response (can be empty if the model only made tool calls)
        /// </summary>
        public String Text;

        /// <summary>
        /// Tool calls made by the model, null if none
        /// </summary>
        public List<AiCompletionToolCall> ToolCalls;

        /// <summary>
        /// Why the model stopped generating
        /// </summary>
        public AiCompletionFinishReasons FinishReason;

        /// <summary>
        /// Number of input (prompt) tokens used
        /// </summary>
        public long InputTokens;

        /// <summary>
        /// Number of output tokens used
        /// </summary>
        public long OutputTokens;
    }

    /// <summary>
    /// Thrown by a completion when the request is invalid
    /// </summary>
    public sealed class AiCompletionException : Exception
    {
        /// <summary>
        /// Create a completion exception
        /// </summary>
        /// <param name="message">The message</param>
        /// <param name="code">An optional error code, ex: "context_length_exceeded"</param>
        /// <param name="statusCode">The http status code to use if the error is returned in a http response</param>
        public AiCompletionException(String message, String code = null, int statusCode = 400) : base(message)
        {
            Code = code;
            StatusCode = statusCode;
        }

        /// <summary>
        /// An optional error code, ex: "context_length_exceeded"
        /// </summary>
        public readonly String Code;

        /// <summary>
        /// The http status code to use if the error is returned in a http response
        /// </summary>
        public readonly int StatusCode;
    }

}

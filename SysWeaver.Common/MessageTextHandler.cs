using System;
using System.Runtime.CompilerServices;

namespace SysWeaver
{
    /// <summary>
    /// An interpolated string handler used by the <c>AddMessage(MessageLevels level, $"...")</c> overloads of <see cref="MessageHost"/> and <see cref="MessageHostExt"/>:
    /// the text is only formatted if the message is accepted by the host (the level is above <see cref="IMessageHost.AcceptMessageAbove"/>),
    /// so a filtered message doesn't format (or evaluate) the interpolation holes.
    /// </summary>
    /// <remarks>
    /// The text is formatted like a <c>$"..."</c> string (the current culture).
    /// The expressions of the holes are not evaluated when the message is filtered, they shouldn't have side effects.
    /// </remarks>
    [InterpolatedStringHandler]
    public ref struct MessageTextHandler
    {
        DefaultInterpolatedStringHandler H;
        readonly bool IsEnabled;

        /// <summary>
        /// Create a handler (called by the compiler)
        /// </summary>
        /// <param name="literalLength">The number of constant chars</param>
        /// <param name="formattedCount">The number of holes</param>
        /// <param name="host">The message host that will receive the message</param>
        /// <param name="level">The level of the message</param>
        /// <param name="shouldAppend">True if the message is accepted (the text must be formatted)</param>
        /// <exception cref="NullReferenceException"><paramref name="host"/> is null.</exception>
        public MessageTextHandler(int literalLength, int formattedCount, IMessageHost host, MessageLevels level, out bool shouldAppend)
        {
            shouldAppend = IsEnabled = level > host.AcceptMessageAbove;
            H = shouldAppend ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
        }

        /// <summary>
        /// True if the message is accepted (the text is formatted), else false
        /// </summary>
        public readonly bool Enabled => IsEnabled;

        /// <summary>Append a literal (called by the compiler)</summary>
        /// <param name="value">The text</param>
        public void AppendLiteral(String value) => H.AppendLiteral(value);

        /// <summary>Append a value (called by the compiler)</summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="value">The value</param>
        public void AppendFormatted<T>(T value) => H.AppendFormatted(value);

        /// <summary>Append a value (called by the compiler)</summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="value">The value</param>
        /// <param name="format">The format string</param>
        public void AppendFormatted<T>(T value, String format) => H.AppendFormatted(value, format);

        /// <summary>Append a value (called by the compiler)</summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="value">The value</param>
        /// <param name="alignment">The minimum width, negative values are left aligned</param>
        public void AppendFormatted<T>(T value, int alignment) => H.AppendFormatted(value, alignment);

        /// <summary>Append a value (called by the compiler)</summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="value">The value</param>
        /// <param name="alignment">The minimum width, negative values are left aligned</param>
        /// <param name="format">The format string</param>
        public void AppendFormatted<T>(T value, int alignment, String format) => H.AppendFormatted(value, alignment, format);

        /// <summary>Append a span of chars (called by the compiler)</summary>
        /// <param name="value">The text</param>
        public void AppendFormatted(ReadOnlySpan<Char> value) => H.AppendFormatted(value);

        /// <summary>Append a span of chars (called by the compiler)</summary>
        /// <param name="value">The text</param>
        /// <param name="alignment">The minimum width, negative values are left aligned</param>
        /// <param name="format">Ignored</param>
        public void AppendFormatted(ReadOnlySpan<Char> value, int alignment = 0, String format = null) => H.AppendFormatted(value, alignment, format);

        /// <summary>Append a string (called by the compiler)</summary>
        /// <param name="value">The text</param>
        public void AppendFormatted(String value) => H.AppendFormatted(value);

        /// <summary>Append a string (called by the compiler)</summary>
        /// <param name="value">The text</param>
        /// <param name="alignment">The minimum width, negative values are left aligned</param>
        /// <param name="format">Ignored</param>
        public void AppendFormatted(String value, int alignment = 0, String format = null) => H.AppendFormatted(value, alignment, format);

        /// <summary>Append a value (called by the compiler)</summary>
        /// <param name="value">The value</param>
        /// <param name="alignment">The minimum width, negative values are left aligned</param>
        /// <param name="format">The format string</param>
        public void AppendFormatted(Object value, int alignment = 0, String format = null) => H.AppendFormatted(value, alignment, format);

        /// <summary>
        /// Get the formatted text and release the buffer (only call it once, and only if <see cref="Enabled"/> is true)
        /// </summary>
        /// <returns>The text</returns>
        public String ToStringAndClear() => H.ToStringAndClear();
    }


    /// <summary>
    /// Extension methods for <see cref="IMessageHost"/>
    /// </summary>
    public static class MessageHostExt
    {
        /// <summary>
        /// Add a new message, the interpolated text is only formatted if the message is accepted (<paramref name="level"/> is above <see cref="IMessageHost.AcceptMessageAbove"/>).
        /// </summary>
        /// <param name="host">The message host</param>
        /// <param name="level">The message level</param>
        /// <param name="message">The interpolated text: <c>host.AddMessage(MessageLevels.Debug, $"Loaded {name}")</c>.
        /// A leading "[Prefix]" is treated as a prefix and aligned when rendered.</param>
        /// <exception cref="NullReferenceException"><paramref name="host"/> is null.</exception>
        public static void AddMessage(this IMessageHost host, MessageLevels level, [InterpolatedStringHandlerArgument(nameof(host), nameof(level))] ref MessageTextHandler message)
        {
            if (message.Enabled)
                host.AddMessage(message.ToStringAndClear(), level);
        }

        /// <summary>
        /// Add a new message with an exception, the interpolated text is only formatted if the message is accepted (<paramref name="level"/> is above <see cref="IMessageHost.AcceptMessageAbove"/>).
        /// </summary>
        /// <param name="host">The message host</param>
        /// <param name="level">The message level</param>
        /// <param name="message">The interpolated text: <c>host.AddMessage(MessageLevels.Debug, $"Request {url} failed", ex)</c>.
        /// A leading "[Prefix]" is treated as a prefix and aligned when rendered.</param>
        /// <param name="ex">An exception (may be null), inner exceptions are included when rendered</param>
        /// <exception cref="NullReferenceException"><paramref name="host"/> is null.</exception>
        public static void AddMessage(this IMessageHost host, MessageLevels level, [InterpolatedStringHandlerArgument(nameof(host), nameof(level))] ref MessageTextHandler message, Exception ex)
        {
            if (message.Enabled)
                host.AddMessage(message.ToStringAndClear(), ex, level);
        }
    }

}

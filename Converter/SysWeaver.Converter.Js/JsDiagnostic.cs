using System;
using System.Collections.Generic;
using System.Text;

namespace SysWeaver.Converter.Js
{
    /// <summary>
    /// The severity of a diagnostic message
    /// </summary>
    public enum JsDiagnosticLevel
    {
        /// <summary>Informational</summary>
        Info,
        /// <summary>The output may not behave exactly like the input (or a construct couldn't be converted)</summary>
        Warning,
        /// <summary>The conversion failed</summary>
        Error,
    }

    /// <summary>
    /// A diagnostic message from the converter
    /// </summary>
    public sealed class JsDiagnostic
    {
        /// <summary>The severity</summary>
        public JsDiagnosticLevel Level;
        /// <summary>The message</summary>
        public String Message;
        /// <summary>The (one based) line in the source, 0 if unknown</summary>
        public int Line;
        /// <summary>The (one based) column in the source, 0 if unknown</summary>
        public int Column;
        /// <summary>The file the diagnostic is about, null if it's about the converted source (only set by JsSite)</summary>
        public String File;

        public override String ToString()
        {
            var sb = new StringBuilder();
            if (File != null)
                sb.Append(File).Append(": ");
            sb.Append(Level);
            if (Line > 0)
                sb.Append(" (").Append(Line).Append(':').Append(Column).Append(')');
            sb.Append(": ").Append(Message);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Collects diagnostics, translates source positions to lines and columns
    /// </summary>
    sealed class Diagnostics
    {
        public readonly List<JsDiagnostic> Items = new List<JsDiagnostic>();
        readonly String Source;
        readonly int LineOffset;
        int[] LineStarts;

        /// <summary>
        /// Create a diagnostics collector
        /// </summary>
        /// <param name="source">The source text</param>
        /// <param name="lineOffset">Added to all line numbers (used for scripts embedded in html)</param>
        public Diagnostics(String source, int lineOffset = 0)
        {
            Source = source ?? "";
            LineOffset = lineOffset;
        }

        public bool HaveErrors
        {
            get
            {
                foreach (var x in Items)
                    if (x.Level == JsDiagnosticLevel.Error)
                        return true;
                return false;
            }
        }

        void Pos(int pos, out int line, out int col)
        {
            line = 0;
            col = 0;
            if (pos < 0)
                return;
            var ls = LineStarts;
            if (ls == null)
            {
                var l = new List<int> { 0 };
                var s = Source;
                var c = s.Length;
                for (int i = 0; i < c; ++i)
                {
                    var ch = s[i];
                    if (ch == '\n' || ch == (char)0x2028 || ch == (char)0x2029)
                        l.Add(i + 1);
                    else if (ch == '\r')
                    {
                        if (i + 1 < c && s[i + 1] == '\n')
                            ++i;
                        l.Add(i + 1);
                    }
                }
                ls = l.ToArray();
                LineStarts = ls;
            }
            var idx = Array.BinarySearch(ls, pos);
            if (idx < 0)
                idx = ~idx - 1;
            line = idx + 1 + LineOffset;
            col = pos - ls[idx] + 1;
        }

        public void Add(JsDiagnosticLevel level, int pos, String message)
        {
            Pos(pos, out var line, out var col);
            lock (Items)
            {
                foreach (var x in Items)
                    if (x.Line == line && x.Column == col && x.Message == message)
                        return;
                Items.Add(new JsDiagnostic { Level = level, Message = message, Line = line, Column = col });
            }
        }

        /// <summary>
        /// Add a diagnostic with an explicit line / column (one based, line offset is applied)
        /// </summary>
        public void AddAt(JsDiagnosticLevel level, int line, int col, String message)
        {
            lock (Items)
                Items.Add(new JsDiagnostic { Level = level, Message = message, Line = line > 0 ? line + LineOffset : 0, Column = col });
        }

        public void Error(int pos, String message) => Add(JsDiagnosticLevel.Error, pos, message);
        public void Warning(int pos, String message) => Add(JsDiagnosticLevel.Warning, pos, message);
        public void Info(int pos, String message) => Add(JsDiagnosticLevel.Info, pos, message);
    }

    /// <summary>
    /// Thrown when a conversion can't be completed (the error is also added to the diagnostics)
    /// </summary>
    public sealed class JsConvertException : Exception
    {
        public JsConvertException(String message) : base(message)
        {
        }
    }
}

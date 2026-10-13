using System;

namespace SysWeaver.EmailReader
{
    // User = the IMAP user name (often the email address), Password = the password (or an app password).

    public sealed class ImapEmailReaderParams : CredentialParams
    {
        public override string ToString() => String.Concat(Server, ':', Port, " (", Security, "), ", Folder ?? "INBOX", ", Auth: ", base.ToString());

        /// <summary>
        /// The host name of the IMAP server, ex: "imap.gmail.com", "outlook.office365.com".
        /// Required.
        /// </summary>
        public String Server;

        /// <summary>
        /// The port to connect to, 0 = the default port of the security option (993 for SslOnConnect, else 143).
        /// Default: 0.
        /// </summary>
        public int Port;

        /// <summary>
        /// The connection security.
        /// Default: SslOnConnect.
        /// </summary>
        public ImapSecurity Security = ImapSecurity.SslOnConnect;

        /// <summary>
        /// If true, any server certificate is accepted (only use for testing).
        /// Default: false.
        /// </summary>
        public bool AcceptAnyCertificate;

        /// <summary>
        /// The folder to read unread (unseen) emails from, null or empty for the inbox.
        /// Sub folders are separated using the server's directory separator, typically '/' or '.'.
        /// Default: null (the inbox).
        /// </summary>
        public String Folder;

        /// <summary>
        /// What to do with an email after it has been read.
        /// Default: MarkSeen.
        /// </summary>
        public ImapAfterRead AfterRead = ImapAfterRead.MarkSeen;

        /// <summary>
        /// The folder to move emails to after they have been read, required (and only used) if AfterRead is Move.
        /// The folder must exist.
        /// </summary>
        public String MoveTo;

        /// <summary>
        /// If true, the html body is used if an email have both a plain text and a html body, else the plain text body is used.
        /// Default: false.
        /// </summary>
        public bool PreferHtml;

        /// <summary>
        /// If true, and the server supports it, IDLE is used to get notified of new emails as soon as they arrive (else the folder is checked every PollInterval seconds).
        /// Default: true.
        /// </summary>
        public bool UseIdle = true;

        /// <summary>
        /// The number of seconds between checking for new emails (when IDLE isn't used).
        /// Default: 30.
        /// </summary>
        public int PollInterval = 30;

        /// <summary>
        /// The number of seconds to wait before connecting again after a failure.
        /// Default: 60.
        /// </summary>
        public int PollRetryDelay = 60;

        /// <summary>
        /// The time out in seconds for server operations.
        /// Default: 60.
        /// </summary>
        public int ServerTimeOut = 60;

        /// <summary>
        /// Maximum number of emails to download per check (the rest are downloaded by the following checks).
        /// Default: 100.
        /// </summary>
        public int MaxDownload = 100;

        /// <summary>
        /// Optional system that all emails received belongs to (unless a more specific address is matched in Addresses).
        /// </summary>
        public String System;

        /// <summary>
        /// Optional addresses that this mailbox receives emails for (ex: aliases), each with an optional system.
        /// Incoming emails to an address gets the system of that address.
        /// </summary>
        public ImapAddressParams[] Addresses;

        /// <summary>
        /// Maximum size of the email queues
        /// </summary>
        public int Size = 1000;

        /// <summary>
        /// Time out in seconds when waiting for new emails
        /// </summary>
        public int TimeOut = 30;

        /// <summary>
        /// Maximum number of emails to return on a poll
        /// </summary>
        public int MaxPoll = 100;
    }

    public sealed class ImapAddressParams
    {
        public override string ToString() => String.Concat(Address, String.IsNullOrEmpty(System) ? "" : " System: " + System);

        /// <summary>
        /// The email address, required.
        /// </summary>
        public String Address;

        /// <summary>
        /// Optional system that emails sent to this address belongs to.
        /// </summary>
        public String System;
    }

    public enum ImapSecurity
    {
        /// <summary>
        /// Use TLS from the start (IMAPS, default port 993)
        /// </summary>
        SslOnConnect,
        /// <summary>
        /// Connect in plain text, then require STARTTLS (default port 143)
        /// </summary>
        StartTls,
        /// <summary>
        /// Connect in plain text, then use STARTTLS if the server supports it (default port 143)
        /// </summary>
        StartTlsWhenAvailable,
        /// <summary>
        /// No encryption (only use for testing, the password is sent in plain text)
        /// </summary>
        None,
    }

    public enum ImapAfterRead
    {
        /// <summary>
        /// Mark the email as read (seen), it's left in the folder
        /// </summary>
        MarkSeen,
        /// <summary>
        /// Delete the email (some servers, ex: Gmail, archives it instead)
        /// </summary>
        Delete,
        /// <summary>
        /// Move the email to the MoveTo folder (it's marked as read too)
        /// </summary>
        Move,
        /// <summary>
        /// Leave the email as is (unread), only for testing: read emails are remembered in memory, so they are read again after a restart.
        /// </summary>
        Nothing,
    }
}

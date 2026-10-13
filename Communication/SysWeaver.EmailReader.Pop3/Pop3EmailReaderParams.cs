using System;

namespace SysWeaver.EmailReader
{
    // User = the POP3 user name (often the email address), Password = the password (or an app password).

    public sealed class Pop3EmailReaderParams : CredentialParams
    {
        public override string ToString() => String.Concat(Server, ':', Port, " (", Security, "), Auth: ", base.ToString());

        /// <summary>
        /// The host name of the POP3 server, ex: "pop.gmail.com", "outlook.office365.com".
        /// Required.
        /// </summary>
        public String Server;

        /// <summary>
        /// The port to connect to, 0 = the default port of the security option (995 for SslOnConnect, else 110).
        /// Default: 0.
        /// </summary>
        public int Port;

        /// <summary>
        /// The connection security.
        /// Default: SslOnConnect.
        /// </summary>
        public Pop3Security Security = Pop3Security.SslOnConnect;

        /// <summary>
        /// If true, any server certificate is accepted (only use for testing).
        /// Default: false.
        /// </summary>
        public bool AcceptAnyCertificate;

        /// <summary>
        /// If true, emails are deleted from the server after they have been read (recommended, POP3 have no other way to mark emails as read).
        /// If false, emails are left on the server and the unique id's (UIDL) of read emails are remembered in memory, so after a restart all emails on the server are read again.
        /// Default: true.
        /// </summary>
        public bool DeleteAfterRead = true;

        /// <summary>
        /// If true, the html body is used if an email have both a plain text and a html body, else the plain text body is used.
        /// Default: false.
        /// </summary>
        public bool PreferHtml;

        /// <summary>
        /// The number of seconds between checking for new emails.
        /// Default: 30.
        /// </summary>
        public int PollInterval = 30;

        /// <summary>
        /// The number of seconds to wait before checking for new emails again after a failure.
        /// Default: 60.
        /// </summary>
        public int PollRetryDelay = 60;

        /// <summary>
        /// The time out in seconds for server operations.
        /// Default: 60.
        /// </summary>
        public int ServerTimeOut = 60;

        /// <summary>
        /// Maximum number of emails to download per check (the rest are downloaded on the next check).
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
        public Pop3AddressParams[] Addresses;

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

    public sealed class Pop3AddressParams
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

    public enum Pop3Security
    {
        /// <summary>
        /// Use TLS from the start (POP3S, default port 995)
        /// </summary>
        SslOnConnect,
        /// <summary>
        /// Connect in plain text, then require STARTTLS (STLS, default port 110)
        /// </summary>
        StartTls,
        /// <summary>
        /// Connect in plain text, then use STARTTLS if the server supports it (default port 110)
        /// </summary>
        StartTlsWhenAvailable,
        /// <summary>
        /// No encryption (only use for testing, the password is sent in plain text)
        /// </summary>
        None,
    }
}

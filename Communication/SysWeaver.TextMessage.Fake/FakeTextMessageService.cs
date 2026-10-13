using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{

    public sealed class FakeTextMessageService : ITextMessageSender, ITextMessageReceiverService, ITextMessageStatusService, ITextMessageEvents, IDisposable
    {
        public FakeTextMessageService(FakeTextMessageParams p = null)
        {
            p = p ?? new FakeTextMessageParams();
            Size = Math.Max(p.Size, 10);
            var timeOut = Math.Max(p.TimeOut, 0) * 1000;
            var maxPoll = Math.Max(p.MaxPoll, 10);
            Channel = p.Channel == TextMessageChannels.Any ? TextMessageChannels.Sms : p.Channel;
            From = p.From;
            NotOnChannelPrefixes = (p.NotOnChannelPrefixes ?? []).Select(GetDigits).Where(x => x.Length > 0).ToArray();
            FailAsync = p.FailAsync;
            AutoDeliver = p.AutoDeliver;
            StatusDelay = Math.Max(0, p.StatusDelay);
            Incoming = new(x => x.Id, x => x.System, x => x.Clone(), Size, timeOut, maxPoll);
            Statuses = new(x => x.Id, x => x.System, x => x.Clone(), Size, timeOut, maxPoll);
        }

        public void Dispose()
        {
            Incoming.Dispose();
            Statuses.Dispose();
        }

        readonly int Size;
        readonly TextMessageChannels Channel;
        readonly String From;
        readonly String[] NotOnChannelPrefixes;
        readonly bool FailAsync;
        readonly bool AutoDeliver;
        readonly int StatusDelay;

        readonly TextMessageQueue<IncomingTextMessage> Incoming;
        readonly TextMessageQueue<TextMessageStatusUpdate> Statuses;

        public event Action<IncomingTextMessage> OnIncoming;
        public event Action<TextMessageStatusUpdate> OnStatus;

        static String GetDigits(String s)
        {
            var sb = new StringBuilder(s?.Length ?? 0);
            foreach (var c in s ?? "")
                if ((c >= '0') && (c <= '9'))
                    sb.Append(c);
            return sb.ToString();
        }

        void Add(AnyTextMessage m)
        {
            lock (Messages)
            {
                Messages.AddFirst(m);
                Messages.Drain(Size);
            }
        }

        /// <summary>
        /// Send a text message
        /// </summary>
        /// <param name="message">The message details</param>
        /// <returns>The result</returns>
        [WebApi]
        public Task<TextMessageSendResult> Send(OutgoingTextMessage message)
        {
            var text = message.Text?.Trim();
            if (String.IsNullOrEmpty(text))
                throw new Exception("No text supplied!");
            var phone = message.PhoneNumber?.Trim() ?? throw new Exception("No phone number supplied!");

            PhonePrefix.GetValidatedPhoneNumber(out var name, out var isoCountry, out var pre, out var num, phone);
            phone = String.Join(' ', pre, num);
            var ch = Channel;
            if (!message.Allows(ch))
                return Task.FromResult(message.Failed(ch, TextMessageFailures.NotOnChannel, "The message doesn't allow the channel " + ch));
            var digits = GetDigits(phone);
            var notOnChannel = NotOnChannelPrefixes.Any(x => digits.StartsWith(x, StringComparison.Ordinal));
            var m = new AnyTextMessage(false, phone, text, name, isoCountry, message.System)
            {
                Channel = ch,
                To = message.From ?? From,
                Reference = message.Reference,
                Status = TextMessageStatuses.Accepted,
            };
            if (notOnChannel && !FailAsync)
            {
                m.Status = TextMessageStatuses.Failed;
                m.Failure = TextMessageFailures.NotOnChannel;
                Add(m);
                var r = message.Failed(ch, TextMessageFailures.NotOnChannel, "Fake: the number isn't on the channel " + ch, "FAKE1");
                r.Id = m.Id;
                r.From = m.To;
                return Task.FromResult(r);
            }
            Add(m);
            if (notOnChannel)
                _ = UpdateLater(m, TextMessageStatuses.Failed, TextMessageFailures.NotOnChannel);
            else if (AutoDeliver)
                _ = DeliverLater(m);
            return Task.FromResult(new TextMessageSendResult
            {
                Id = m.Id,
                Reference = message.Reference,
                Status = TextMessageStatuses.Accepted,
                Channel = ch,
                From = m.To,
            });
        }

        async Task UpdateLater(AnyTextMessage m, TextMessageStatuses status, TextMessageFailures failure = TextMessageFailures.None)
        {
            await Task.Delay(StatusDelay).ConfigureAwait(false);
            SetStatus(m, status, failure);
        }

        async Task DeliverLater(AnyTextMessage m)
        {
            await UpdateLater(m, TextMessageStatuses.Sent).ConfigureAwait(false);
            await UpdateLater(m, TextMessageStatuses.Delivered).ConfigureAwait(false);
        }

        void SetStatus(AnyTextMessage m, TextMessageStatuses status, TextMessageFailures failure)
        {
            m.Status = status;
            m.Failure = failure;
            var u = new TextMessageStatusUpdate
            {
                Id = AnyTextMessage.NewId(),
                MessageId = m.Id,
                Reference = m.Reference,
                System = m.System,
                PhoneNumber = m.PhoneNumber,
                Channel = m.Channel,
                Status = status,
                Failure = failure,
                Error = failure == TextMessageFailures.None ? null : "Fake: " + failure,
                ErrorCode = failure == TextMessageFailures.None ? null : "FAKE" + (int)failure,
                Time = DateTime.UtcNow,
            };
            Statuses.Add(u);
            try
            {
                OnStatus?.Invoke(u);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Fake an incoming message
        /// </summary>
        /// <param name="message">Message details (PhoneNumber is the sender, From is the number that received the message)</param>
        [WebApi]
        public void FakeIncoming(OutgoingTextMessage message)
        {
            var text = message.Text?.Trim();
            if (String.IsNullOrEmpty(text))
                throw new Exception("No text supplied!");
            var phone = message.PhoneNumber?.Trim() ?? throw new Exception("No phone number supplied!");
            PhonePrefix.GetValidatedPhoneNumber(out var name, out var isoCountry, out var pre, out var num, phone);
            phone = String.Join(' ', pre, num);

            var m = new AnyTextMessage(true, phone, text, name, isoCountry, message.System)
            {
                Channel = Channel,
                To = message.From ?? From,
                SenderName = "Fake user",
            };
            m.ResponseToken = m.Id;
            Add(m);
            var inc = m.Clone();
            Incoming.Add(inc);
            try
            {
                OnIncoming?.Invoke(inc);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Fake a status update of a sent message
        /// </summary>
        /// <param name="request">The status</param>
        [WebApi]
        public void FakeStatus(FakeStatusRequest request)
        {
            AnyTextMessage m;
            lock (Messages)
                m = Messages.FirstOrDefault(x => (!x.Incoming) && (x.Id == request.MessageId));
            if (m == null)
                throw new Exception("No sent message with the id " + request.MessageId.ToQuoted() + " found!");
            SetStatus(m, request.Status, request.Status == TextMessageStatuses.Failed ? (request.Failure == TextMessageFailures.None ? TextMessageFailures.Other : request.Failure) : TextMessageFailures.None);
        }

        /// <summary>
        /// Poll for any new text messages, blocks until new messages are available
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new messages</returns>
        [WebApi]
        public Task<IncomingTextMessage[]> GetIncoming(GetIncomingTextMessageRequest request)
            => Incoming.Poll(request?.LastId, request?.System);

        /// <summary>
        /// Poll for any new status updates, blocks until new updates are available
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new updates</returns>
        [WebApi]
        public Task<TextMessageStatusUpdate[]> GetStatusUpdates(GetTextMessageStatusRequest request)
            => Statuses.Poll(request?.LastId, request?.System);

        /// <summary>
        /// A table with all messages
        /// </summary>
        /// <param name="request">The table request</param>
        /// <returns></returns>
        [WebApi]
        [WebApiRequestCache(1)]
        [WebMenuTable(null, "Messages", "Messages", "The list of messages")]
        public TableData MessageTable(TableDataRequest request)
            => TableDataTools.Get(request, 2000, Messages, "Text messages");


        readonly LinkedList<AnyTextMessage> Messages = new ();

    }


}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{

    public sealed class FakeTextMessageService : ITextMessageSender, ITextMessageReceiverService, IDisposable
    {
        public FakeTextMessageService(FakeTextMessageParams p = null)
        {
            p = p ?? new FakeTextMessageParams();
            Size = Math.Max(p.Size, 10);
            TimeOut = Math.Max(p.TimeOut, 0) * 1000;
            MaxPoll = Math.Max(p.MaxPoll, 10);
            
        }



        public void Dispose()
        {
        }

        readonly int Size;
        readonly int TimeOut;
        readonly int MaxPoll;


        /// <summary>
        /// Send a text message
        /// </summary>
        /// <param name="message">The message details</param>
        /// <returns></returns>
        [WebApi]
        public Task Send(OutgoingTextMessage message)
        {
            var text = message.Text.Trim();
            if (String.IsNullOrEmpty(text))
                throw new Exception("No text supplied!");
            var phone = message.PhoneNumber.Trim();

            PhonePrefix.GetValidatedPhoneNumber(out var name, out var isoCountry, out var pre, out var num, phone);
            phone = String.Join(' ', pre, num);

            var m = new AnyTextMessage(false, phone, text, name, isoCountry, message.System);
            lock (IncomingMessages)
            {
                Messages.AddFirst(m);
                Messages.Drain(Size);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Fake an incoming message
        /// </summary>
        /// <param name="message">Message details</param>
        [WebApi]
        public void FakeIncoming(OutgoingTextMessage message)
        {
            var text = message.Text.Trim();
            if (String.IsNullOrEmpty(text))
                throw new Exception("No text supplied!");
            var phone = message.PhoneNumber.Trim();
            PhonePrefix.GetValidatedPhoneNumber(out var name, out var isoCountry, out var pre, out var num, phone);
            phone = String.Join(' ', pre, num);

            var m = new AnyTextMessage(true, phone, text, name, isoCountry, message.System);
            lock (IncomingMessages)
            {
                Messages.AddFirst(m);
                IncomingMessages.AddFirst(m);
                Messages.Drain(Size);
                IncomingMessages.Drain(Size);
            }
            IncomingBlock.Change();
        }

        
        readonly BlockUntilChange IncomingBlock = new BlockUntilChange();


        /// <summary>
        /// Poll for any new text messages, blocks until new messages are available
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new messages</returns>
        [WebApi]
        public async Task<IncomingTextMessage[]> GetIncoming(GetIncomingTextMessageRequest request)
        {
            var inc = IncomingMessages;
            var cc = IncomingBlock.Cc;
            var t = TimeOut;
            var lastId = request?.LastId;
            var system = request?.System;
            bool anySystem = String.IsNullOrEmpty(system);
            for (; ; )
            {
                lock (inc)
                {
                    if (inc.Count > 0)
                    {
                        if (!inc.First.Value.Id.FastEquals(lastId))
                        {
                            var mp = MaxPoll;
                            var maxAge = DateTime.UtcNow.AddMinutes(-5);
                            var newLast = lastId;
                            List<IncomingTextMessage> msgs = new(mp);
                            foreach (var x in inc)
                            {
                                if (x.Id.FastEquals(lastId))
                                    break;
                                if (anySystem || x.System.FastEquals(system))
                                    msgs.Add(x.Clone());
                                newLast = lastId;
                                if (x.Time < maxAge)
                                    break;
                                if (msgs.Count >= mp)
                                    break;
                            }
                            if (msgs.Count > 0)
                                return msgs.ToArray();
                            lastId = newLast;
                        }
                    }
                }
                if (t <= 0)
                    return null;
                var oldCC = cc;
                cc = await IncomingBlock.WaitForChange(cc, t).ConfigureAwait(false);
                if (oldCC == cc)
                    return null;
            }
        }


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


        readonly LinkedList<AnyTextMessage> IncomingMessages = new ();
        readonly LinkedList<AnyTextMessage> Messages = new ();




    }


}

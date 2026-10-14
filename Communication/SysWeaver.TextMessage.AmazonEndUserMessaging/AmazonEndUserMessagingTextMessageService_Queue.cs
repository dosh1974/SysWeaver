using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{

    public sealed partial class AmazonEndUserMessagingTextMessageService
    {
        /// <summary>
        /// Ids of received messages and events (SQS and SNS delivers at least once).
        /// </summary>
        readonly ConcurrentDictionary<String, DateTime> Seen = new(StringComparer.Ordinal);

        /// <summary>
        /// Long poll a queue, process and delete the messages
        /// </summary>
        async Task PollQueue(String queueUrl, CancellationToken cancel)
        {
            //  Let the constructor finish
            await Task.Yield();
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    var res = await Sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = queueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 20,
                    }, cancel).ConfigureAwait(false);
                    var msgs = res?.Messages;
                    if ((msgs == null) || (msgs.Count <= 0))
                        continue;
                    Purge();
                    List<DeleteMessageBatchRequestEntry> delete = new(msgs.Count);
                    foreach (var m in msgs)
                    {
                        //  Messages that fails to process are not deleted (they are received again after the visibility time out, and moved to the dead letter queue if one is configured)
                        if (Process(m.Body))
                            delete.Add(new DeleteMessageBatchRequestEntry { Id = delete.Count.ToString(), ReceiptHandle = m.ReceiptHandle });
                    }
                    if (delete.Count > 0)
                        await Sqs.DeleteMessageBatchAsync(new DeleteMessageBatchRequest { QueueUrl = queueUrl, Entries = delete }, cancel).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (cancel.IsCancellationRequested)
                        break;
                    PollFails.OnException(ex);
                    Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Failed to poll the queue {queueUrl.ToQuoted()}", ex);
                    try
                    {
                        await Task.Delay(PollRetryDelay, cancel).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }
            }
        }

        static String GetString(JsonElement e, String name)
            => e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.String) ? v.GetString() : null;

        /// <summary>
        /// Process a message from a queue
        /// </summary>
        /// <param name="body">The body, an SNS notification (or the raw message if raw message delivery is enabled on the subscription)</param>
        /// <returns>True if the message should be deleted (processed, or not something this service understands)</returns>
        bool Process(String body)
        {
            using var _ = PerfMon.Track(nameof(Process));
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (Exception ex)
            {
                ProcessFails.OnException(ex);
                Msg?.AddMessage(LogPrefix + "Ignoring a queue message that isn't json", MessageLevels.Warning);
                return true;
            }
            try
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return true;
                //  Unwrap the SNS notification
                if ((GetString(root, "Type") == "Notification") && root.TryGetProperty("Message", out var _))
                {
                    var inner = GetString(root, "Message");
                    if (inner == null)
                        return true;
                    doc.Dispose();
                    doc = JsonDocument.Parse(inner);
                    root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                        return true;
                }
                if (root.TryGetProperty("eventType", out var _))
                {
                    OnEvent(root);
                    return true;
                }
                if (root.TryGetProperty("originationNumber", out var _) && root.TryGetProperty("messageBody", out var _))
                {
                    OnInboundMessage(root);
                    return true;
                }
                Msg?.AddMessage(LogPrefix + "Ignoring an unknown queue message", MessageLevels.Debug);
                return true;
            }
            catch (Exception ex)
            {
                ProcessFails.OnException(ex);
                Msg?.AddMessage(LogPrefix + "Failed to process a queue message", ex, MessageLevels.Warning);
                return false;
            }
            finally
            {
                doc.Dispose();
            }
        }

        /// <summary>
        /// An incoming (two-way) SMS
        /// </summary>
        void OnInboundMessage(JsonElement e)
        {
            var id = GetString(e, "inboundMessageId");
            var from = GetString(e, "originationNumber");
            if ((id == null) || (from == null))
                return;
            if (!Seen.TryAdd("in:" + id, DateTime.UtcNow))
                return;
            var phone = from;
            String name = null;
            String isoCountry = null;
            try
            {
                PhonePrefix.GetValidatedPhoneNumber(out name, out isoCountry, out var pre, out var num, from);
                phone = String.Join(' ', pre, num);
            }
            catch
            {
            }
            var to = GetString(e, "destinationNumber");
            var s = FindSender(to);
            var rec = new AmazonTextMessage
            {
                Incoming = true,
                Id = id,
                Time = DateTime.UtcNow,
                PhoneNumber = phone,
                Name = name,
                Country = isoCountry,
                Text = GetString(e, "messageBody"),
                System = s?.System,
                ResponseToken = String.Concat(s?.Identity ?? to, '/', id),
                Channel = Channel,
                To = s?.Display ?? to,
                Status = "received",
            };
            AddRecord(rec);
            PublishIncoming(rec.Clone());
        }

        /// <summary>
        /// A delivery event (from a configuration set event destination)
        /// </summary>
        void OnEvent(JsonElement e)
        {
            var type = GetString(e, "eventType") ?? "";
            //  Only text (SMS / RCS) events, not media or voice
            if (!type.StartsWith("TEXT_", StringComparison.Ordinal))
                return;
            var id = GetString(e, "messageId");
            var statusText = GetString(e, "messageStatus");
            if ((id == null) || (statusText == null))
                return;
            long ts = 0;
            if (e.TryGetProperty("eventTimestamp", out var tse) && (tse.ValueKind == JsonValueKind.Number))
                ts = tse.GetInt64();
            if (!Seen.TryAdd(String.Concat("ev:", id, ':', statusText, ':', ts), DateTime.UtcNow))
                return;
            var status = AmazonEndUserMessagingErrors.GetStatus(statusText, out var failure);
            if (status == TextMessageStatuses.Unknown)
                return;
            String reference = null;
            String system = null;
            if (e.TryGetProperty("context", out var ctx) && (ctx.ValueKind == JsonValueKind.Object))
            {
                reference = GetString(ctx, ContextReference);
                system = GetString(ctx, ContextSystem);
            }
            PendingOut.TryGetValue(id, out var p);
            var rec = p?.Record;
            //  Events may arrive out of order, don't go back from delivered
            if ((rec != null) && (status < TextMessageStatuses.Delivered) && (rec.Status == "DELIVERED"))
                return;
            var to = GetString(e, "destinationPhoneNumber");
            var origination = GetString(e, "originationPhoneNumber");
            var u = new TextMessageStatusUpdate
            {
                Id = NewId("Status"),
                MessageId = id,
                Reference = reference ?? p?.Message.Reference,
                System = system ?? p?.Message.System,
                PhoneNumber = rec?.PhoneNumber ?? to,
                //  An origination that isn't a phone number is an RCS agent
                Channel = (origination != null) && !origination.StartsWith('+') && !origination.All(Char.IsDigit) ? TextMessageChannels.Rcs : Channel,
                Status = status,
                Failure = failure,
                Time = ts > 0 ? DateTime.UnixEpoch.AddMilliseconds(ts) : DateTime.UtcNow,
            };
            if (status == TextMessageStatuses.Failed)
            {
                u.ErrorCode = statusText;
                u.Error = GetString(e, "messageStatusDescription") ?? statusText;
                if ((to != null) && (failure == TextMessageFailures.InvalidNumber))
                    SetCached(GetDigits(to), failure);
            }
            if (rec != null)
                rec.Status = statusText;
            var isFinal = e.TryGetProperty("isFinal", out var fe) && (fe.ValueKind == JsonValueKind.True);
            if (isFinal || status.IsFinal())
                PendingOut.TryRemove(id, out var _);
            PublishStatus(u);
        }
    }
}

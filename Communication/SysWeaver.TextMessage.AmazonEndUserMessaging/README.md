# SysWeaver.TextMessage.AmazonEndUserMessaging

[⬆ SysWeaver overview](../../README.md)

> Send and receive SMS using AWS End User Messaging SMS (formerly Amazon Pinpoint SMS), with delivery events and two-way SMS read from SQS.

| | |
|---|---|
| **Layer** | Users |
| **Kind** | Micro service |

## Purpose

Implement `ITextMessageSender`, `ITextMessageReceiverService`, `ITextMessageStatusService` and `ITextMessageEvents` using AWS, so that phone verification, notifications and conversations can use SMS through AWS (alone, or as the SMS fallback of WhatsApp using the [router](../SysWeaver.TextMessage.Router/README.md)).

## How it fits into SysWeaver

```mermaid
flowchart LR
  App["UserManager / TextMessageRouterService"] -->|"Send"| S["AmazonEndUserMessagingTextMessageService"]
  S -->|"SendTextMessage, CarrierLookup"| EUM["AWS End User Messaging SMS"]
  EUM -->|"two-way SMS, delivery events (configuration set)"| SNS["SNS topic(s)"]
  SNS --> SQS["SQS queue"]
  S -->|"long poll, delete"| SQS
```

## Key features

- Uses the official AWS SDK (`AWSSDK.PinpointSMSVoiceV2` = the End User Messaging SMS API, the package name is historical, and `AWSSDK.SQS`). Credentials from config / credentials file, or the default AWS credential chain (instance / task roles etc).
- Senders (origination identities): phone numbers, phone number ids / ARNs, pools and sender ids, each with an optional `System` and `Countries`. Sender selection: the number that received the message being replied to (`ResponseToken`), the requested `From`, the `System`, the recipient's country, else the first sender (or AWS's default when no senders are configured).
- `Purpose` maps to the message type (Marketing → PROMOTIONAL, else TRANSACTIONAL), `TimeToLive` to the TTL, and for India `Template.Name` to `IN_TEMPLATE_ID` (+ `IndiaEntityId`). `Reference` and `System` are sent in the message context, which AWS includes in the delivery events, so status updates have them even after a restart.
- Optional carrier lookup (`CarrierLookup`, a paid API call, cached): landlines fail with NotOnChannel (so a router can fall back), invalid numbers with InvalidNumber.
- Errors are mapped to `TextMessageFailures` (opted out → Blocked, sandbox not verified / blocked country / unregistered sender → Rejected, invalid destination → InvalidNumber); other errors (access denied, configuration) throw. Delivery statuses are mapped too (DELIVERED, UNREACHABLE → Unreachable, BLOCKED → Blocked, SPAM / CARRIER_BLOCKED → Rejected, TTL_EXPIRED → Expired, ...).
- Incoming messages and delivery events are read from SQS queues subscribed to the SNS topics: no public endpoint needed, and messages wait in the queue while the service is down (they are deleted when processed, failures are retried by SQS). Both SNS envelopes and raw message delivery are supported, duplicates are ignored.
- Message table, stats and perf monitoring.

## Limitations and considerations

- SMS only: MMS (`SendMediaMessage`), RCS (`SendRcsMessage`) and voice are not implemented. WhatsApp through AWS is a separate service (AWS End User Messaging Social).
- Delivery events can arrive up to 72 hours after sending; in countries without delivery receipts the final status is SUCCESSFUL (accepted by the carrier), reported as `Sent`.
- Two-way SMS isn't available in all countries, and not for sender ids.
- `Send`, `GetIncoming`, `GetStatusUpdates` and `MessageTable` require the `Admin` or `Ops` role when called over http.

## Using it

```json
{ "Type": "SysWeaver.TextMessage.AmazonEndUserMessagingTextMessageService, SysWeaver.TextMessage.AmazonEndUserMessaging",
  "Name": "AwsSms",
  "Params": {
    "CredFile": "$(KeyFolder)/AwsSms.txt",
    "Region": "eu-north-1",
    "ConfigurationSetName": "sms-events",
    "QueueUrls": [ "https://sqs.eu-north-1.amazonaws.com/123456789012/sms-events" ],
    "Senders": [
      { "OriginationIdentity": "+46 8 123 456 78" },
      { "OriginationIdentity": "pool-0123456789abcdef", "PhoneNumber": "+1 206 555 0152", "Countries": ["US", "CA"] }
    ] } }
```

`AwsSms.txt` contains a single line: `AKIAXXXXXXXXXXXXXXXX:the-secret-access-key` (omit `CredFile` to use the default AWS credential chain).

## Setting up an AWS account for testing

1. **AWS account and region** - sign up at <https://aws.amazon.com/>, open the *AWS End User Messaging SMS* console and pick a region. Phone numbers, configuration sets, the sandbox and limits are per region.
2. **Sandbox** - new accounts are in the SMS sandbox: you can only send to *verified destination numbers* (*Sandbox → Verified destination numbers → Add*, confirm with the code sent to the phone) and the monthly spend limit is low. For production use *Request production access* (a support case with the use case, countries and expected volume) and request a higher spend limit.
3. **Origination identity** - *Phone numbers → Request originator*: pick the country, number type (long code, toll-free, 10DLC, short code) and capabilities (SMS, two-way). Many countries require registration before sending (US toll-free verification or 10DLC brand / campaign registration, sender id registration in some countries). For US testing without registration, request a *simulator* number (sends to simulator destination numbers only, nothing is delivered to real phones). Alternatively use a sender id where supported (send only).
4. **IAM** - create a user (or role) with a policy allowing `sms-voice:SendTextMessage` (+ `sms-voice:CarrierLookup` if `CarrierLookup` is used) and `sqs:ReceiveMessage`, `sqs:DeleteMessage` on the queue. Create an access key for the user and put `AccessKeyId:SecretAccessKey` in the credentials file.
5. **SNS topic and SQS queue** - create a standard SNS topic (ex: `sms-events`) and a standard SQS queue, then *SQS → queue → Subscribe to Amazon SNS topic* (this also sets the queue policy). Optionally add a dead-letter queue. Raw message delivery on the subscription is optional (both formats work).
6. **Delivery events** - *Configuration sets → Create* (ex: `sms-events`), *Add event destination*: Amazon SNS, the topic from step 5, events: the TEXT_* events (or all). Set `ConfigurationSetName`.
7. **Two-way SMS** - *Phone numbers → number → Two-way SMS → Enable*, destination: Amazon SNS, the same topic (or another topic subscribed by the same queue). Allow the service to publish to the topic (the console offers to create the topic policy).
8. **Test** - send to a verified number (or a simulator destination), watch the status updates (`GetStatusUpdates` or the message table), reply from the phone and check `GetIncoming`.

## Relationships

- **Project:** [`SysWeaver.TextMessage.AmazonEndUserMessaging.csproj`](SysWeaver.TextMessage.AmazonEndUserMessaging.csproj)
- **Builds on:** [SysWeaver.Common](../../SysWeaver.Common/README.md), [SysWeaver.IsoData](../../SysWeaver.IsoData/README.md), [SysWeaver.MicroServices.ServiceManager](../../SysWeaver.MicroServices.ServiceManager/README.md), [SysWeaver.TableData](../../SysWeaver.TableData/README.md)
- **Used with:** [SysWeaver.TextMessage.Router](../SysWeaver.TextMessage.Router/README.md)
- **Alternative to:** [SysWeaver.TextMessage.Twilio](../SysWeaver.TextMessage.Twilio/README.md), [SysWeaver.TextMessage.Fake](../SysWeaver.TextMessage.Fake/README.md)

# SysWeaver.Email.AmazonSes

[⬆ SysWeaver overview](../../README.md)

> Amazon SES (Simple Email Service) e-mail sending service implementing the framework's e-mail contract.

| | |
|---|---|
| **Layer** | Networking |
| **Kind** | Micro service |

## Purpose

Provide `IEmailService` backed by [Amazon SES](https://aws.amazon.com/ses/), as an alternative to [SysWeaver.Email.Smtp](../SysWeaver.Email.Smtp/README.md)'s SMTP service, so that features such as account verification, password reset and invitations can send e-mail through AWS.

## How it fits into SysWeaver

```mermaid
flowchart LR
  UM["UserManager and other services"] -->|IEmailService| SES["AmazonSesEmailService"]
  SES -->|"SES v2 API (AWSSDK.SimpleEmailV2)"| AWS["Amazon SES"]
```

## Key features

- Uses the official AWS SDK (`AWSSDK.SimpleEmailV2`, SES API v2 `SendEmail`), including SigV4 request signing.
- Credentials from config / credentials file (`AccessKeyId:SecretAccessKey`), or the default AWS credential chain (environment variables, `~/.aws/credentials`, instance / task roles) when none are given.
- Optional sender display name (RFC 2047 encoded when non-ascii) and SES configuration set.
- Retries with tracking; requests rejected by SES (unverified identity, invalid address etc) are not retried, throttling is.

## Limitations and considerations

- One recipient per call (as defined by `IEmailService`); no templates, attachments or bulk sending.
- While the account is in the SES *sandbox* it can only send to verified identities (see below).
- Bounces and complaints are not handled by this service; set up a configuration set with event publishing (SNS etc) for that in production.

## Using it

```json
{ "Type": "SysWeaver.Email.AmazonSesEmailService, SysWeaver.Email.AmazonSes",
  "Params": {
    "Region": "eu-north-1",
    "From": "noreply@example.com",
    "FromName": "My Service",
    "CredFile": "$(KeyFolder)/AmazonSes.txt" } }
```

`AmazonSes.txt` contains a single line: `AKIAXXXXXXXXXXXXXXXX:the-secret-access-key`.

## Setting up an AWS account for testing

1. **AWS account** - sign up at <https://aws.amazon.com/> (requires a credit card). Sign in to the console and pick the region you want to send from (ex: `eu-north-1`, Stockholm). SES identities, sandbox status and quotas are *per region*, the `Region` param must match.
2. **Verify a sender identity** - console: *Amazon SES → Configuration → Identities → Create identity*.
   - *Email address*: quickest for testing, AWS sends a confirmation link to it.
   - *Domain*: recommended for real use. Add the 3 Easy DKIM CNAME records SES shows to your DNS; verification usually completes within minutes to hours. Also add SPF/DMARC records (ex: `_dmarc.example.com TXT "v=DMARC1; p=none;"`), optionally configure a custom MAIL FROM domain.
3. **Sandbox** - new accounts are in the sandbox: you can only send *to* verified addresses/domains, max 200 messages per 24 hours, 1 message per second. For testing either:
   - Verify each test recipient address as an identity too (step 2), or
   - Send to the SES mailbox simulator, which works in the sandbox and doesn't hurt your reputation: `success@simulator.amazonses.com`, `bounce@simulator.amazonses.com`, `complaint@simulator.amazonses.com`, `ooto@simulator.amazonses.com`, `suppressionlist@simulator.amazonses.com`.
4. **Create IAM credentials** - console: *IAM → Users → Create user* (ex: `ses-sender`, no console access), attach an inline policy like:

   ```json
   {
     "Version": "2012-10-17",
     "Statement": [{
       "Effect": "Allow",
       "Action": "ses:SendEmail",
       "Resource": "*",
       "Condition": { "StringEquals": { "ses:FromAddress": "noreply@example.com" } }
     }]
   }
   ```

   Then *Security credentials → Create access key* (use case "Application running outside AWS") and store `AccessKeyId:SecretAccessKey` in the credentials file. Note: these are normal IAM access keys, *not* the "SMTP credentials" the SES console can generate.
   When running on AWS (EC2, ECS, Lambda) prefer an IAM role and leave the credentials empty.
5. **Test** - configure the service with `From` = your verified address and send to a verified address or the mailbox simulator. Check *SES → Account dashboard* for send statistics.
6. **Production access** (when done testing) - *SES → Account dashboard → Request production access*: describe the use case (transactional mail, how recipients opt in, how bounces/complaints are handled). AWS usually answers within 24 hours; afterwards you can send to any address with a higher quota.
7. **Production hygiene** - keep the bounce rate below 5% and the complaint rate below 0.1% (or the account gets reviewed/paused). Create a configuration set with SNS (or EventBridge) event destinations for bounces and complaints and set `ConfigurationSet`; the account-level suppression list is on by default.

## Pricing

List prices (pay-as-you-go, no subscription or minimum fee) as of October 2026, see [Amazon SES pricing](https://aws.amazon.com/ses/pricing/) for current prices.

| | |
|---|---|
| Per email | $0.10 / 1,000 |
| Per data | $0.12 / GB |
| **1M emails @ 1 KB** | 1,000,000 × $0.0001 = $100.00<br/>+ 0.95 GB × $0.12 = $0.11<br/>**≈ $100.11** |

- 1M × 1 KB = 1,000,000 KB ≈ 0.95 GB.
- The pricing page lists the data charge as for *attachments*; with no attachments the total would be exactly $100.00.
- Not included: dedicated IPs ($24.95 / IP / month, optional), SES plans (optional), SNS usage for bounce handling, and outbound network transfer from your own servers.

## Relationships

- **Project:** [`SysWeaver.Email.AmazonSes.csproj`](SysWeaver.Email.AmazonSes.csproj)
- **Builds on:** [SysWeaver.Common](../../SysWeaver.Common/README.md), [SysWeaver.MicroServices](../../SysWeaver.MicroServices/README.md)
- **Alternative to:** [SysWeaver.Email.Smtp](../SysWeaver.Email.Smtp/README.md) (SMTP), [SysWeaver.Email.MicrosoftAcs](../SysWeaver.Email.MicrosoftAcs/README.md) (Azure Communication Services)

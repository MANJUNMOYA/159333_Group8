# Notifications / 业务通知

The existing SMTP service now sends English, Campus Coffee themed transactional emails.

| Trigger | Recipient | Content |
| --- | --- | --- |
| Order committed | Email entered at checkout | Order number, items, quantities, totals in NZD, pickup/delivery and current status |
| Merchant application submitted | Applicant | Receipt acknowledgement and pending review status |
| Merchant application submitted | Review mailbox | Application ID, business/contact name and an administrator portal link |
| Application approved | Applicant | Approval, welcome message and merchant sign-in link |
| Application rejected | Applicant | Review result and contact guidance |

客户确认邮件表示订单已收到，不表示已经付款或可以取餐。商家获批后仍需完成原有邮箱验证。邮件采用网站原有咖啡色样式，动态文本作 HTML 编码，链接来自 `PublicBaseUrl`，不使用请求 Host。

## Configuration

Reuse the existing `Smtp` settings / `SMTP_*` variables in the server `.env`.

- `NOTIFICATIONS_ENABLED=true` (default): enable business notifications when SMTP settings are present.
- `MERCHANT_REVIEW_EMAIL`: optional administrator review mailbox; blank uses `SMTP_FROM_EMAIL`.
- `PUBLIC_BASE_URL`: public website URL for links, for example `https://campuscoffee.duckdns.org`.

Store credentials only in server environment configuration or User Secrets. Never commit them. SMTP values may be absent: business actions continue and report `unavailable`. Registration still requires confirmation; changing notification settings does not bypass account verification.

## Delivery behaviour

`NotificationService` is called after order commit, application save, or approval/rejection and role commit. APIs return `notificationStatus` (`sent`, `failed`, `unavailable`, `skipped`); merchant forms show the delivery outcome in their existing status message. `sent` means the SMTP server accepted the message, not guaranteed inbox delivery.

SMTP calls are awaited with a 15-second timeout per message. Submission sends two messages sequentially, so a slow provider can add up to approximately 30 seconds. SMTP failure does not undo an order, application or review. Failures are logged with event type and reference, without credentials, full mail bodies or recipient addresses. Missing configuration or disabled notifications are skipped. An acknowledgement failure does not prevent trying the administrator alert.

Updating an already-pending application does not resend submission mail; submitting a draft reuses the draft record. Repeating the same review decision does not resend decision mail. A later change from Approved to Rejected, or vice versa, generates a new decision message.

This is best-effort delivery: there is no durable mail queue or automatic retry. A process failure after saving data but before sending can lose a notification. Concurrent duplicate requests are not guaranteed exactly-once delivery. Failed notifications can be diagnosed through logs; repeating an unchanged decision does not retry delivery. Existing orders and past decisions are not emailed when this version is deployed.

## Verification

Run `dotnet run --project CampusCoffeeSystem.Tests/CampusCoffeeSystem.Tests.csproj` on Windows with the `CampusCoffeeIntegration` LocalDB instance. Tests use a unique disposable database and a capturing email sender, never real Gmail recipients.

Checks cover registration verification, commit-before-notify on an independent database connection, receipt content/encoding, stock handling, unavailable or disabled SMTP, delivery failure and timeout, application/admin recipients, draft reuse, repeated-submission/decision suppression, approval roles, rejection and custom reviewer configuration. No schema migration is required.

# Email verification only / 仅邮箱验证

## Scope / 合入范围

Based on `main` commit `00204e1`, selectively ported from `codex/integrate-main-email-ai` (`134a8d9`).

本分支仅迁入邮箱验证及其测试，不合入 AI、Ollama、推荐栏目、聊天窗口、Docker/Apache 配置或数据库迁移。保留 main 的个人账户导航、商品、订单、商家审核与原有页面样式。

## Flow / 实现流程

1. 原有注册表单调用 `POST /api/auth/customers` 或 `/api/auth/merchants`。Identity 保存密码哈希，新账号的 `EmailConfirmed` 为 false。
2. `EmailConfirmationService` 调用 `GenerateEmailConfirmationTokenAsync`，将令牌作 Base64URL 编码，生成带 userId 和 code 的确认链接。有效期为 24 小时。
3. `SmtpEmailSender` 使用 SMTP 发送网站咖啡配色的英文欢迎邮件。发送失败时保留未验证账号，并告知用户重发，不自动激活账号。
4. 用户访问 `/Identity/Account/ConfirmEmail`；页面解码令牌并调用 `ConfirmEmailAsync`。Identity 验证成功后更新 `EmailConfirmed`。
5. 原有登录接口使用 `PasswordSignInAsync`，配合 `RequireConfirmedAccount` 和 `RequireConfirmedEmail` 阻止未确认账号登录。角色检查仍然有效，商家仍须通过审核。
6. 登录页与注册结果页提供重发入口。`POST /api/auth/resend-confirmation` 返回统一提示，避免透露账户是否存在。

Registration and resend endpoints require an anti-forgery token and share a limit of five requests per remote IP per ten minutes. Behind a reverse proxy, configure trusted forwarded headers in the deployment; otherwise users may share the proxy's rate-limit bucket. This branch does not change proxy trust settings.

已有 `EmailConfirmed=true` 的账号不强制重新验证。没有新增数据库字段或迁移；令牌由 Identity/Data Protection 保护，并非数据库中的六位验证码，也不是严格的单次消费令牌。

## Configuration / 运行配置

通过环境变量或本地开发的 User Secrets 配置，不要把实际邮箱密码提交到 Git。Docker 不是发送邮件的必要条件。

| Environment variable | Purpose |
| --- | --- |
| `Smtp__Host` | SMTP server; for Gmail, `smtp.gmail.com` |
| `Smtp__Port` | `587` |
| `Smtp__EnableSsl` | `true` |
| `Smtp__Username` | Sending email address |
| `Smtp__Password` | SMTP credential; for Gmail, an app password rather than the account password |
| `Smtp__FromEmail` | Authorized sender email address |
| `Smtp__FromName` | `Campus Coffee` |
| `PublicBaseUrl` | Canonical public HTTPS origin, e.g. `https://campuscoffee.duckdns.org` |
| `DataProtection__KeysPath` | Optional absolute path to a protected, persistent key directory |

生产环境应明确配置 `PublicBaseUrl`，避免从请求 Host 构建邮件地址。Data Protection 密钥目录应只允许应用账户访问；容器部署时应挂载持久卷。不要提交密钥到 Git。原整合分支的 `PersistDataProtectionKeys` 开关在本分支不使用，改用上述明确的路径配置。

## Verification / 验证

```text
dotnet build CampusCoffeeSystem.csproj
dotnet run --project CampusCoffeeSystem.Tests/CampusCoffeeSystem.Tests.csproj
```

集成测试需要 Windows SQL Server LocalDB 中名为 `CampusCoffeeIntegration` 的实例。测试创建唯一命名的临时数据库，执行 main 的迁移，并在 finally 中核对数据库名称后清理。邮件发送使用替身，不连接 Gmail，不触碰网站数据库。

覆盖注册、密码哈希、HTML 编码、未验证登录拦截、无效/跨账户/过期令牌、真实确认页面处理、验证后登录 Cookie、角色限制、商家审核限制、SMTP 失败以及邮件重发。未验证真实 SMTP 投递或生产反向代理。

## English explanation

The registration controller creates an unconfirmed Identity account and requests an email confirmation token. Our email service encodes that token into a confirmation link and sends it through SMTP. When the user opens the link, ASP.NET Core Identity validates the token and marks the email as confirmed. The login flow checks email confirmation before issuing an authentication cookie. Email confirmation does not grant extra roles or bypass merchant approval.

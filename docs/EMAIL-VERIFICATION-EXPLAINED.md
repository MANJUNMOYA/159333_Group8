# 邮箱验证：中英文答辩说明

本文件对应 main 与 codex/email-verification 合并后的实现。
当前客户与商家注册都需要邮件确认；商家另需管理员审批。
这是确认链接验证，不是六位数字验证码。

## 1. 创建账户 / Account creation

中文：原来的注册页面由 portal-auth.js 提交 JSON 到
POST /api/auth/customers 或 POST /api/auth/merchants。
AuthController 使用 ASP.NET Core Identity 的 UserManager.CreateAsync 创建账户，
由 Identity 哈希保存密码，并将 EmailConfirmed 保持为 false。
已有的邮箱唯一性检查、客户角色、姓名 Claims 和商家审批流程继续保留。

English: “The registration form sends a request to our ASP.NET Core API.
We use ASP.NET Core Identity to create the account and store a salted password
hash. New accounts start with EmailConfirmed set to false.”

## 2. 生成确认链接 / Generating the confirmation link

中文：EmailConfirmationService 调用 GenerateEmailConfirmationTokenAsync(user)。
该 Token 由 Identity 的 Data Protection 提供保护，并验证用户、用途、
SecurityStamp 和有效期。程序明确设置 Token 有效期为 24 小时。
Base64UrlEncode 只是把 Token 编码成适合 URL 的形式，本身不是加密。
链接中含 userId 和 code，不含用户密码。
PublicBaseUrl 可固定外部网站地址，防止使用容器内部地址生成链接。

English: “We generate an email confirmation token using UserManager.
ASP.NET Identity protects and validates the token. We URL-safe encode it and
include it in a confirmation link with the user ID. The configured lifetime
is 24 hours. Base64 encoding is for URL compatibility, not encryption.”

## 3. Gmail 发信 / Sending through Gmail

中文：服务通过 IEmailSender 调用 SmtpEmailSender，使用 HTML 邮件模板、
smtp.gmail.com、587 端口和 STARTTLS。认证使用 Gmail 应用专用密码。
凭据由服务器 .env 经 Docker 环境变量注入，不写进源代码。
姓名和链接进行 HTML 编码，邮件使用原网站的咖啡色品牌样式与英文欢迎语。
SMTP 接受邮件不等于已投递到收件箱，用户仍需检查垃圾邮件。

English: “Our SMTP sender sends a branded HTML email through Gmail using
STARTTLS. Credentials are supplied through environment variables.
The email contains a confirmation button, but never the user's password.”

## 4. 验证链接 / Validating the link

中文：点击链接会进入 /Identity/Account/ConfirmEmail。
页面解码 code，查找用户，然后调用 ConfirmEmailAsync。
有效 Token 让数据库中的 EmailConfirmed 变为 true；损坏、过期或属于
其他用户的 Token 会被拒绝。损坏的 Base64 不会导致错误页面。
确认邮箱不代表授予管理员/商家角色，也不自动登录。

English: “When the user clicks the link, our confirmation page decodes the
token and calls ConfirmEmailAsync. If validation succeeds, Identity updates
EmailConfirmed to true. Invalid, expired, or mismatched tokens are rejected.”

## 5. 登录控制 / Enforcing confirmation

中文：Program 同时要求 RequireConfirmedAccount 和 RequireConfirmedEmail。
新版登录接口原来的 SignInAsync 直接签发 Cookie，不能依赖它自动执行确认检查。
合并后使用 PasswordSignInAsync 执行 Identity 登录检查，再返回对应角色入口。
角色验证仍保留，确认邮箱的客户不能进入管理员入口，
商家验证邮箱后仍需审批得到 Merchant 角色。

English: “We enforce confirmation during sign-in using PasswordSignInAsync.
An unconfirmed user cannot receive a login cookie. Role checks are separate:
confirming an email does not grant merchant or administrator access.”

## 6. 失败恢复 / Recovery

中文：若 SMTP 发信失败，账户保留未验证状态，页面明确提示失败，
提供重新发送入口。重新发送接口返回统一提示，避免暴露某邮箱是否有账户；
注册与重发共用按 IP 限流（每十分钟最多五次）。
Data Protection 密钥用 Docker volume 持久化，以便重建容器后仍可验证旧链接。
旧 main 版本已经标为 EmailConfirmed=true 的历史账户不会被自动撤销；
本次规则对新注册账户生效，不能宣称所有历史账户都真实收过验证邮件。

English: “If email delivery fails, we keep the account unconfirmed and allow
the user to request another email. We persist the Data Protection keys so
container recreation does not invalidate otherwise valid confirmation links.”

## 老师可能追问

- **你们自己实现密码加密了吗？** 没有，Identity 负责带盐密码哈希。
  “Identity handles password hashing; we do not store plaintext passwords.”
- **确认 Token 是绝对一次性的吗？** 不宣称消费后立即销毁。
  Identity 校验有效期、用户和安全标记；重复有效链接一般只重复确认同一状态。
  “Confirmation tokens are time-limited and bound to the account. We do not
  maintain a separate single-use token table.”
- **验证能证明什么？** 表示当时能够访问这个邮箱，不验证现实身份。
  “It demonstrates access to the mailbox, not the person's legal identity.”
- **测试真的发 Gmail 邮件了吗？** 自动测试用模拟发信器，测试真实 Identity
  Token 与 SQL Server；服务器的 Gmail 实际投递需另做部署验收。
  “We test the Identity flow against SQL Server with a fake email sender.
  Live SMTP delivery is a separate deployment check.”

## AI 合并说明 / AI integration

Ollama 和 Qwen 本地 AI 功能已与新版商品/订单数据连接。
菜单来自当前上架且有库存的数据库商品；最近购买记录来自当前用户的数据库订单，
不再接受浏览器上报 purchase。聊天历史只取当前用户最近 12 条，
浏览和订单项目各最多 30 条。前端推荐卡片带商品 ID 与库存限制，
调用现有购物车；商品价格与库存最终仍由下单后端核验。

“The AI service runs locally through Ollama. We supply a limited set of public
products and the current user's activity. Purchase history comes from stored
orders. Recommendations reuse real product IDs and the existing cart.”

提示词只是行为引导，不能保证模型永不编造。用户自己输入聊天的个人信息
仍会进入本地模型上下文；不要把“未主动附加邮箱字段”说成“自动匿名化所有文本”。

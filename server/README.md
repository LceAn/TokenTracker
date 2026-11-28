# 服务端：部署自己的云端

**简体中文** · [English](README.en.md)

这个目录放的是 TokenTracker 的**服务端源码**。想用自己的服务器代替官方云，需要的东西都在这里：自己用，或者给一个团队、一个社区共用。

先说清楚三件事：

- **官方云是付费的托管服务，自部署是免费的**，但服务器、域名、备份和升级都要自己负责。自部署用的是仓库里这套 MIT 开源代码；官方线上运行的脚本不一定和仓库完全一致。
- **自部署目前是技术预览，不是一键安装。** 上游在隔离的 Linux 环境里验证过安装器和函数；“公网服务器 + 打包好的桌面客户端”这条完整链路，上游没有验证过，本分支也还没有。
- **客户端这一侧**，本分支已经可以在「设置 → 云服务」里直接填写自部署实例的地址，不需要改环境变量。

## 目录里有什么

| 路径 | 内容 |
| --- | --- |
| `self-host/` | 私有后端安装器：生成建库 SQL（`install.cjs`）、构建后端函数（`build-functions.cjs`）、安装清单（`manifest.cjs`）、建库脚本（`baseline.sql`、`aggregation.sql`、`private-rpc-access.sql`），以及给开发者用的验收工具（`acceptance.cjs`） |
| `migrations/` | 数据库迁移（PostgreSQL），共 59 个。它们是给官方已有数据库做增量改动的历史记录，**不要在新数据库上从头执行**。安装器会从这里挑出需要的部分 |
| `cloudflare/` | Cloudflare Worker 后端：官方托管云的新后端，负责账号、订阅、支付和公开排行榜。**目前不支持自托管**，见文末 |
| `docs/` | 上游写的自托管文档原文，背景、证据和验收清单比本文更详细 |

还有一部分服务端代码不在本目录：**后端函数的源码**在 [`desktop/dashboard/edge-patches/`](../desktop/dashboard/edge-patches/)，共 46 个 `.ts` 文件（设备授权、用量写入、账号读取、计费、支付回调、排行榜等）。它们和仪表盘共用类型定义，上游放在 `dashboard/` 下。本分支为了每周同步上游时不产生大量冲突，没有移动它们；`self-host/build-functions.cjs` 会从那里读取自托管需要的 14 个。

## 一套自部署实例由三部分组成

1. **InsForge 平台。** 一个开源的后端即服务（PostgreSQL、认证、API、函数运行时），用 Docker Compose 部署。
2. **TokenTracker 的应用部分。** 数据表、RPC、触发器、权限，加上 14 个后端函数。这部分由本目录的安装器负责。
3. **一份指向这个实例的仪表盘**，或者直接用本分支的桌面客户端。

## 部署步骤

### 0. 准备

**一台服务器。** Linux，装好 Docker Engine 和 Compose v2；一个域名；HTTPS 反向代理。数据库、PostgREST 和函数运行时的端口不要直接对外开放，只通过 HTTPS 暴露 API 和管理入口。

**一台构建机。** 用来生成安装文件。安装器和函数构建要借用桌面端那个 npm 包的依赖，所以先在 `desktop/` 里装依赖：

```bash
git clone https://github.com/LceAn/TokenTracker.git
cd TokenTracker/desktop
npm ci
```

（只有运行开发者验收工具 `acceptance.cjs`，或者要自己构建网页仪表盘时，才还需要 `npm --prefix dashboard ci`。）

### 1. 安装 InsForge 平台

按 InsForge 官方的 [Docker Compose 自部署说明](https://github.com/InsForge/InsForge#self-hosted-docker-compose) 操作。几个要点：

- 选一个你审阅过的版本，固定镜像版本或 digest。
- 官方的 setup 脚本会生成密钥，但不会启动服务。把 `API_BASE_URL`、`VITE_API_BASE_URL` 设成你对外的 API 域名后再启动。
- 管理员密码、数据库密码、JWT 签名密钥、加密密钥和管理 API key 只留在服务器上，前端只使用公开的 anon key。

上游验证过的组合是 InsForge v2.3.3（PostgreSQL 15.18、Deno 2.0.6）。安装器要求平台提供 `auth.users(id)`、`auth.uid()`，以及 `anon`、`authenticated`、`project_admin` 三个角色；缺了它会报错。

### 2. 生成并执行应用结构

在构建机的仓库根目录：

```bash
node server/self-host/install.cjs --sql /absolute/private/path/tokentracker-install.sql
```

这个命令只生成文件，不连接任何数据库。检查生成的 SQL，然后在服务器上用一个事务执行完：

```bash
psql --set ON_ERROR_STOP=1 --file /absolute/private/path/tokentracker-install.sql
```

- 用数据库所有者的身份，在一个新的应用数据库里执行。连接信息用服务器上受保护的配置，不要把密码写在命令行里。文件自带事务和安装锁。
- 不要执行 `migrations/` 里的全部历史迁移：其中有针对官方特定用户的运营处理和数据修复。
- 安装过程会记在 `tokentracker_self_host_installations` 表里（每一步和它的 SHA-256）。原样重跑不会动已有的用户、token 和用量；某一步的内容变了则会明确报错，不会悄悄覆盖。
- 清单共 16 步，上游在 v1.5.34 新增了 `14-usage-cost-provenance`（用量成本的溯源信息）。对用上一版清单装好的实例重新运行安装器：已有步骤的内容没变就跳过，只补上新增的步骤。
- 清单执行完之后，安装器会把实例设成 `hosting_mode='self_hosted'`：私有同步免费，不限设备数和历史时长，不产生付费订单。

如果你的 InsForge 实例已经用 CLI 连上了，也可以走平台的迁移接口（`--migration`），做法见 [docs/self-hosting-backend.md](docs/self-hosting-backend.md)。

### 3. 构建并部署后端函数

```bash
node server/self-host/build-functions.cjs /absolute/private/path/tokentracker-functions
```

输出 14 个打包好的函数（每个一个 `.js` 文件）和一份 `functions.json` 清单。然后通过你实例的管理接口或管理界面，把清单里的函数逐个部署上去。

这 14 个函数是：设备授权的三个接口、设备 token 签发、设备改名、用量写入、七个账号用量读取接口，以及账号和设备的计费接口。**不包含**公开排行榜和支付回调。

### 4. 给函数配置环境变量

```dotenv
INSFORGE_BASE_URL=https://api.your-domain.example
INSFORGE_SERVICE_ROLE_KEY=your-server-only-service-credential
ANON_KEY=your-public-anonymous-key
JWT_SECRET=your-platform-signing-secret
TOKENTRACKER_DASHBOARD_URL=https://dashboard.your-domain.example
TOKENTRACKER_BILLING_ENVIRONMENT=live
```

- 平台如果用 RS256 签发会话，用 `JWT_PUBLIC_KEY` 代替 `JWT_SECRET`。
- `TOKENTRACKER_DASHBOARD_URL` 必须是 HTTPS（本机回环地址可以用 HTTP）。设备授权链接会指向它。
- `TOKENTRACKER_BILLING_ENVIRONMENT` 只接受 `live` 或 `sandbox`，不填也可以。原因见下文“实战经验”。
- 不要给个人实例配置 Waffo、Paddle、微信支付或支付宝的密钥。

### 5. 仪表盘（可选）

想在浏览器里用，就在 `desktop/` 里用你自己的地址和 anon key 构建网页仪表盘，产物在 `dashboard/dist/`；服务器需要把未知路径回退到 `index.html`：

```bash
cd desktop
VITE_INSFORGE_BASE_URL=https://api.your-domain.example \
VITE_INSFORGE_ANON_KEY=your-public-anonymous-key \
npm --prefix dashboard run build
```

构建会拒绝管理员 key、service-role key 和用户 JWT，只接受公开的 anon key。InsForge v2.3.3 生成的 anon key 是 `anon_` 加 40 或 64 位十六进制；旧实例可以是用 `role=anon` 的 JWT。

如果启用 InsForge 的 OAuth 登录，要在实例上允许 `tokentracker://auth/callback` 这个回调地址（桌面客户端用），以及你自己仪表盘的来源和 `/dashboard` 回调（网页登录用）。

### 6. 连接客户端

**本分支的桌面客户端。** 打开「设置 → 云服务」，在「服务器」卡片里点「使用自部署服务器」，填入「服务器地址」和「Anon Key」，先点「测试连接」，通过后点「保存并重新连接」。切换会退出当前账号，在新服务器重新登录即可。想换回来，点「恢复官方云」。

**命令行或其它版本。** 使用环境变量 `TOKENTRACKER_INSFORGE_BASE_URL` 和 `TOKENTRACKER_INSFORGE_ANON_KEY`。

anon key 是公开的，可以放在客户端；管理员 key、service-role key 和签名密钥绝不能放进前端或客户端。

### 7. 怎样才算部署完成

下面这些要在服务器的真实 HTTPS 地址上逐条验证，单元测试代替不了：

- 新账号能登录、退出和刷新会话；一个账号读不到另一个账号的数据。
- 设备授权链接指向你自己的仪表盘；两台客户端能把用量写到同一个账号下，不覆盖彼此的设备身份。
- 同一批数据重传不会让总量增加；修正数据、设备筛选、时区和夏令时、模型成本与本地口径一致。
- 会话过期、设备暂停、后端离线时界面可以恢复，不丢本地队列。
- 浏览器和桌面端的请求都访问你选的实例，不把数据写到官方后端或公开排行榜。
- 备份能恢复到另一个实例，恢复后总量一致。

## 实战经验

下面来自一次真实的自部署，记录在上游 issue [#1119](https://github.com/xiufengsun/TokenTracker/issues/1119) 的评论里，当时基于上游 `0397a2c`。标了“已核对”的四条，我们在当前代码上实际验证过；其余是部署环境上的问题，我们没有在现在的环境里复现。

| 现象 | 原因和做法 |
| --- | --- |
| 换一台机器重新运行安装器，报 `Installed step has changed`，但清单并没有改 | **已核对。** 安装器按读到的原始字节计算每一步的 SHA-256，换行符不同，校验和就不同。仓库用 `.gitattributes` 规定用 LF 换行，干净的克隆就是 LF；如果检出时换行被改成了 CRLF（例如 Windows 上旧检出里的文件），算出来的校验和会和别人不一样。始终用干净的克隆生成安装文件。 |
| 所有云函数返回 503，调用方只看到失败 | **已核对。** `TOKENTRACKER_BILLING_ENVIRONMENT` 只接受 `live` 和 `sandbox`。自部署的“免费”是安装器最后一步写进数据库的策略决定的，和这个变量无关。留空或填 `live` 即可。 |
| 从官方云迁移数据，迁移量算出来高了约 3 倍 | **已核对。** 同一个 `(hour_start, source, model)` 只保留 `total_tokens` 最大的一条，而客户端的 `queue.jsonl` 是不断累加的快照。迁移时每个键取最大值再上传，不要把队列逐行求和。 |
| 多用户实例想要排行榜 | **已核对。** 自托管的范围不含排行榜：14 个函数里没有，建库脚本里也没有排行榜的表。目前只能自己实现。 |
| 新建的 RPC 调用报 404 | PostgREST 有 schema 缓存。新建或修改函数后执行 `NOTIFY pgrst, 'reload schema';` |
| 改了函数的返回类型但不生效 | `CREATE OR REPLACE FUNCTION` 不能改返回类型，要先 `DROP FUNCTION`。 |
| plpgsql 函数报 `column reference is ambiguous` | `RETURNS TABLE` 的输出列名不能和查询里的列同名，用别名区分。 |
| 部署计费函数时命令报错 | 打包后的函数约 270 KB，把源码内联在命令行里会超过 shell 的参数长度上限。改成从文件读取请求体，例如 curl 的 `-d @文件`。 |
| 在 linux/arm64 上 `npm ci` 失败 | 一个原生依赖（zstd 相关）没有 arm64 的预编译包。在 x64 机器或 macOS 上构建函数，再把产物传到服务器。 |
| 桌面客户端登录弹窗失败 | 当时的桌面版（1.5.15）登录弹窗直接请求远端实例，在中国大陆经 Cloudflare 代理的网络下经常被中断。我们当时修改了内嵌的仪表盘，让登录请求走本地服务代理。当前版本在同样的网络下是否还有这个问题，我们没有验证。 |

## 备份、恢复与升级

- **备份。** 把数据库导出、服务端配置和文件存储放在一起备份，和服务器分开保存，并且实际演练一次恢复。
- **工具版本。** 镜像里的 PostgreSQL 和自带的工具版本可能不一致（上游验证的镜像是 PostgreSQL 15.18，默认工具是 18.4），要用匹配版本的 `pg_dump` 和 `pg_restore`。
- **升级前。** 记下应用 commit、InsForge 版本和镜像 digest、已安装的步骤，先备份。
- **升级应用。** 安装器会校验已安装的步骤：内容没变的原样跳过，变了的会报错。所以升级需要一份经过审阅的新清单；不要删掉安装记录表，也不要改一个已经执行过的步骤来强行重试。
- **升级 InsForge。** 同时检查认证、存储和函数运行时有没有变化。

## 还没有完成的

- 在真实的公网服务器上做一次完整的全新安装、双设备同步、账号隔离、备份恢复和升级测试。
- 用打包好的桌面客户端（Windows、macOS、Linux）对着真实实例验收登录和同步。
- 要提供 OAuth 或邮件登录的话，配置实例自己的服务商并验证。
- 上文实战经验里的几项问题，本分支目前只在文档里做了说明，代码层面还没有改。

## 官方云和自部署实例现在各走哪里

上游从 v1.5.31 起把官方账号的后端换成了 Cloudflare Worker，本分支已同步到 v1.5.34。客户端按所选的地址决定去向：

- **官方地址**：已有的登录会话迁移到 Cloudflare，读取和上传都走 `https://api.tokentracker.cc`。
- **自部署实例的地址**：不做迁移，读取和上传都走实例自己的地址和 anon key，界面不会去读 Cloudflare。

所以在设置里选了自部署服务器之后，数据不会流向官方后端。`cloudflare/` 是官方云那套后端的源码，和上游的账号、支付、公开排行榜以及官方域名绑在一起，**不支持自托管**。本分支的「切换服务器」功能对接的是 InsForge 实例。

## 相关文档

- [docs/self-hosting.zh-CN.md](docs/self-hosting.zh-CN.md)、[docs/self-hosting.md](docs/self-hosting.md)：上游的自托管总览
- [docs/self-hosting-backend.md](docs/self-hosting-backend.md)：后端安装细节与验证证据
- [docs/self-hosting-status.md](docs/self-hosting-status.md)：实现状态与剩余门槛
- [InsForge 自部署说明](https://github.com/InsForge/InsForge#self-hosted-docker-compose)

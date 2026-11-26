# About this fork / 关于本分支

[中文](#中文) · [English](#english)

## 中文

### 这是什么

**本仓库是 [xiufengsun/TokenTracker](https://github.com/xiufengsun/TokenTracker) 的社区维护分支**，由 [@LceAn](https://github.com/LceAn) 维护，遵循 MIT 协议。

Token Tracker 的全部基础能力——本地解析、用量统计、桌面客户端、云服务——都来自原作者。本分支不做另一款产品，只做一件事：**在上游最新代码的基础上，合入社区已经做好、但还没被上游合并的改进**。

### 为什么有这个分支

起点是上游 issue [#1119](https://github.com/xiufengsun/TokenTracker/issues/1119)：桌面客户端一直围绕官方云服务打造，想改用自建云服务的用户只能靠环境变量或命令行参数，设置界面里没有入口。

我们为这件事提交了一组 PR。它们都通过了测试，也按评审意见做过返工，但到本分支建立时（2026-11-26）还没有被合并，issue 也没有得到回应。于是建立这个分支：需要的人可以先用上，同时 PR 继续留在上游，不放弃合并的可能。

### 和上游相比多了什么

| 改动 | 说明 | 上游 PR |
| --- | --- | --- |
| 在设置里切换云端后端 | 「设置 → 云服务」新增服务器卡片，可在官方云和自部署实例之间切换（填写地址和 anon key），也可随时恢复官方云 | [#1082](https://github.com/xiufengsun/TokenTracker/pull/1082) |
| 保存前测试连接 | 由本地服务探测目标地址是否可达（浏览器直接访问会被跨域限制拦住），并区分证书错误和地址错误 | [#1084](https://github.com/xiufengsun/TokenTracker/pull/1084) |
| 切换前确认 | 已登录时，切换服务器或更换 anon key 都会退出登录，所以先弹窗确认；表单里附了自部署指南的链接 | [#1085](https://github.com/xiufengsun/TokenTracker/pull/1085) |
| 当前服务器状态 | 打开设置时自动检查当前后端，显示「已连接」或「服务器不可达」；地址没写协议时自动补上 `https://` | [#1086](https://github.com/xiufengsun/TokenTracker/pull/1086) |
| 「关于」页面 | 「设置 → 应用 → 关于」：项目介绍、版本、许可证和贡献者列表 | [#1081](https://github.com/xiufengsun/TokenTracker/pull/1081) |
| 标题字号修复 | 云服务子页面左上角的「设置」标题比其他子页面小，已修正 | [#1079](https://github.com/xiufengsun/TokenTracker/pull/1079) |

这些 PR 现在是否已被上游合并，请以上游页面为准。

另外，[#1087](https://github.com/xiufengsun/TokenTracker/pull/1087)（在会话页面里浏览 pi 的会话）已经由上游作者自己实现并合入（[#1071](https://github.com/xiufengsun/TokenTracker/pull/1071)），所以本分支不需要再保留它。

### 怎么维护

- **以上游为基线。** `main` 分支 = 上游 `main` 的代码 + 上表的改动。本分支于 2026-11-26 建立，基于上游 v1.5.28。
- **每周同步一次。** 把上游 `main` 上的新功能和修复合并进来，完整测试通过后推送，和上游版本的差距不超过一周。
- **同步记录可查。** 每次同步是一个合并提交，标题形如 `chore(fork): sync upstream vX.Y.Z`。想知道本分支包含到上游的哪个版本，看最近一次同步提交即可。
- **改动尽量少、互相独立。** 功能改动保持「可以直接作为 PR 提交给上游」的形态；只有本文件和各 README 顶部的说明是本分支专属的内容。
- **上游合并了，就交还给上游。** 表里的 PR 一旦被上游合并，对应改动会在下一次同步时成为上游代码的一部分，本分支不再单独维护。

每周同步的步骤（假设 `upstream` 指向原仓库，`origin` 指向本分支）：

```bash
git fetch upstream main
git switch main
git merge --no-ff upstream/main -m "chore(fork): sync upstream vX.Y.Z"  # 有冲突就解决后 git merge --continue
npm test && npm run validate:copy && npm run validate:locale && npm run validate:ui-hardcode
npm --prefix dashboard run test && npm --prefix dashboard run build
git push origin main
```

### 怎么获取

**目前只有源码（`main` 分支）。** 没有发布安装包，也没有发布到 npm 或 Homebrew——README 里的安装命令装的都是官方版本，不包含上表的改动。构建方式与上游相同，见 [CONTRIBUTING.md](./CONTRIBUTING.md)。

**已知事项：** 客户端的自动更新目前写死指向官方发布渠道（Windows 和 macOS 都是）。用本分支的源码自行构建时，一旦上游发布新版本，更新提示会引导安装官方版本。

### 反馈

关于本分支的问题和讨论，目前集中在上游 issue [#1119](https://github.com/xiufengsun/TokenTracker/issues/1119)。

### 致谢与许可

感谢原作者 [@xiufengsun](https://github.com/xiufengsun) 以及所有上游贡献者——本分支的一切都建立在他们的工作之上。

MIT License。上游的版权声明完整保留在 [LICENSE](./LICENSE)；本分支新增的内容同样以 MIT 协议发布。

---

## English

### What this is

**This repository is a community-maintained fork of [xiufengsun/TokenTracker](https://github.com/xiufengsun/TokenTracker)**, maintained by [@LceAn](https://github.com/LceAn) under the MIT license.

Everything Token Tracker can do — local parsing, usage tracking, the desktop apps, the cloud service — comes from the original author. This fork is not a different product. It does one thing: **keeps the latest upstream code and adds community improvements that upstream has not merged yet.**

### Why it exists

It started with upstream issue [#1119](https://github.com/xiufengsun/TokenTracker/issues/1119): the desktop client is built around the official cloud, so anyone who wants to use their own self-hosted backend has to set environment variables or CLI flags — there is nothing in Settings.

We submitted a set of PRs for that. They pass tests and were reworked after review, but as of this fork's creation (2026-11-26) none had been merged and the issue had no reply. This fork lets people use the work now, while the PRs stay open upstream.

### What it adds on top of upstream

| Change | What it does | Upstream PR |
| --- | --- | --- |
| Switch the cloud backend in Settings | A Server card under Settings → Cloud switches between the official cloud and a self-hosted instance (address + anon key), and can restore the official cloud at any time | [#1082](https://github.com/xiufengsun/TokenTracker/pull/1082) |
| Test the connection before saving | The local server probes the target for reachability (the browser is blocked by CORS) and tells certificate errors apart from address errors | [#1084](https://github.com/xiufengsun/TokenTracker/pull/1084) |
| Confirm before switching | While signed in, switching servers or changing the anon key signs you out, so the card asks first; the form links the self-hosting guide | [#1085](https://github.com/xiufengsun/TokenTracker/pull/1085) |
| Current server status | Opening Settings checks the active backend and shows Connected or Server unreachable; an address without a scheme gets `https://` added | [#1086](https://github.com/xiufengsun/TokenTracker/pull/1086) |
| About page | Settings → App → About: project introduction, version, license and the contributor list | [#1081](https://github.com/xiufengsun/TokenTracker/pull/1081) |
| Title size fix | The Settings title on the Cloud sub-page was smaller than on every other sub-page | [#1079](https://github.com/xiufengsun/TokenTracker/pull/1079) |

Check the PR pages for whether upstream has merged them since.

[#1087](https://github.com/xiufengsun/TokenTracker/pull/1087) (browsing pi sessions in the Sessions page) is not carried here: the upstream author implemented and merged it himself ([#1071](https://github.com/xiufengsun/TokenTracker/pull/1071)).

### How it is maintained

- **Upstream is the baseline.** `main` = upstream `main` + the changes above. The fork was created on 2026-11-26 from upstream v1.5.28.
- **Synced once a week.** New upstream features and fixes are merged in, the full test suite is run, and the result is pushed. The fork stays within a week of upstream.
- **Every sync is visible.** Each sync is one merge commit titled `chore(fork): sync upstream vX.Y.Z`; the most recent one tells you which upstream version the fork includes.
- **Changes stay small and independent.** Feature changes are kept in a form that can be submitted upstream as a PR. Only this file and the notice at the top of each README are fork-only content.
- **Merged upstream means handed back.** Once one of the PRs above merges upstream, the next sync brings it in as upstream code and the fork stops carrying it separately.

Weekly sync procedure (assuming `upstream` is the original repository and `origin` is this fork):

```bash
git fetch upstream main
git switch main
git merge --no-ff upstream/main -m "chore(fork): sync upstream vX.Y.Z"  # on conflicts: resolve, then git merge --continue
npm test && npm run validate:copy && npm run validate:locale && npm run validate:ui-hardcode
npm --prefix dashboard run test && npm --prefix dashboard run build
git push origin main
```

### How to get it

**Source only, for now (`main` branch).** There are no prebuilt installers, and nothing is published to npm or Homebrew — the install commands in the README install the official release, without the changes above. Building works the same way as upstream; see [CONTRIBUTING.md](./CONTRIBUTING.md).

**Known issue:** the apps' auto-update check is hardcoded to the official release channel (Windows and macOS). If you build from this fork yourself, update prompts will offer the official version as soon as upstream ships a newer release.

### Feedback

Questions and discussion about this fork currently happen in upstream issue [#1119](https://github.com/xiufengsun/TokenTracker/issues/1119).

### Thanks and license

Thanks to the original author [@xiufengsun](https://github.com/xiufengsun) and every upstream contributor — this fork is built entirely on their work.

MIT License. The upstream copyright notice is preserved in full in [LICENSE](./LICENSE); additions in this fork are released under the same license.

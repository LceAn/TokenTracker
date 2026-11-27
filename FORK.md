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
| 更新渠道可选（仅 Windows） | 在「设置 → App 与更新」里选择跟随本分支还是官方版本的发布，见[下一节](#更新渠道仅-windows) | 仅本分支 |

这些 PR 现在是否已被上游合并，请以上游页面为准。

另外，[#1087](https://github.com/xiufengsun/TokenTracker/pull/1087)（在会话页面里浏览 pi 的会话）已经由上游作者自己实现并合入（[#1071](https://github.com/xiufengsun/TokenTracker/pull/1071)），所以本分支不需要再保留它。

### 更新渠道（仅 Windows）

Windows 客户端可以在「设置 → App 与更新 → 更新渠道」里选择：

- **社区分支**（默认）：跟随本分支的发布。
- **官方版本**：跟随上游的官方发布。选中后，下一次更新会把社区分支版本替换成官方版本，社区分支独有的功能会被移除。安装前一定会让你确认，不会在后台自己换掉。

从社区分支换到官方版本之后，官方客户端不认识本分支，想换回来需要重新下载本分支的安装包安装。换过去的那一刻，「官方版本」这个选择会自动清除，所以重新安装社区分支后，它又会默认跟随社区分支。

macOS 和 Linux 客户端没有这个选项，它们的应用仍然只跟随官方发布。

### 怎么维护

- **以上游为基线。** `main` 分支 = 上游 `main` 的代码 + 上表的改动。本分支于 2026-11-26 建立，基于上游 v1.5.28。
- **每周同步一次。** 把上游 `main` 上的新功能和修复合并进来，测试通过后推送，和上游版本的差距不超过一周。
- **同步记录可查。** 每次同步是一个合并提交，标题形如 `chore(fork): sync upstream vX.Y.Z`。想知道本分支包含到上游的哪个版本，看最近一次同步提交即可。
- **改动尽量少、互相独立。** 功能改动保持「可以直接作为 PR 提交给上游」的形态；本分支专属的是这个文件、各 README 顶部的说明、更新渠道和发布脚本。
- **上游合并了，就交还给上游。** 表里的 PR 一旦被上游合并，对应改动会在下一次同步时成为上游代码的一部分，本分支不再单独维护。

每周同步的步骤（假设 `upstream` 指向原仓库，`origin` 指向本分支）：

```bash
git fetch upstream main
git switch main
git merge --no-ff upstream/main -m "chore(fork): sync upstream vX.Y.Z"  # 有冲突就解决后 git merge --continue
npm run validate:copy && npm run validate:locale && npm run validate:ui-hardcode && npm run validate:versions
npm --prefix dashboard run test && npm --prefix dashboard run build
git push origin main
```

`npm test` 是上游的完整服务端测试，需要数据库等环境；在 Windows 上有一部分上游测试本身就跑不过。

**发布 Windows 版。** 版本号是「上游版本 + `-fork.<修订号>`」，例如 `1.5.29-fork.1`。修订号记在仓库根目录的 `FORK_REVISION`，每次发布加一；上游的版本号文件保持和上游一致，所以同步时不会在版本号上冲突。先提交所有改动，再在仓库根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File TokenTrackerWin\scripts\build-fork-release.ps1
```

脚本会跑测试和校验、构建前端、打包 Node 运行时和 CLI、发布、生成压缩包、安装包和校验文件，放在 `TokenTrackerWin\installer\Output\<版本号>\`。它只负责构建，不打标签也不发布；需要本机装有 .NET 8 SDK 和 Inno Setup 6。

### 怎么获取

**Windows：** 到 [Releases](https://github.com/LceAn/TokenTracker/releases) 下载 `TokenTracker-Setup.exe`，不需要管理员权限。它和官方版用同一个安装位置和产品标识，会原地替换对方，不会动你的数据目录。已安装的社区分支版本可以在设置里检查更新。

**macOS 和 Linux：** 没有安装包，只能从源码自行构建，方式与上游相同，见 [CONTRIBUTING.md](./CONTRIBUTING.md)。

README 里的安装命令（npm、Homebrew 等）装的都是官方版本，不包含上表的改动。

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
| Choice of update channel (Windows only) | Settings → App & Updates picks whether the app follows this fork's releases or the official ones; see [below](#update-channel-windows-only) | This fork only |

Check the PR pages for whether upstream has merged them since.

[#1087](https://github.com/xiufengsun/TokenTracker/pull/1087) (browsing pi sessions in the Sessions page) is not carried here: the upstream author implemented and merged it himself ([#1071](https://github.com/xiufengsun/TokenTracker/pull/1071)).

### Update channel (Windows only)

The Windows app has an **Update channel** row under Settings → App & Updates:

- **Community fork** (the default): follows this fork's releases.
- **Official release**: follows the upstream releases. The next update then replaces the fork build with the official version, which removes the features that exist only in the fork. You are always asked to confirm first; it never switches on its own in the background.

Once you are on the official release, the official app does not know about this fork. To come back, download this fork's installer and run it again. The "Official release" choice clears itself at the moment of the hand-over, so after reinstalling the fork it follows the fork again by default.

The macOS and Linux apps do not have this option and keep following the official releases.

### How it is maintained

- **Upstream is the baseline.** `main` = upstream `main` + the changes above. The fork was created on 2026-11-26 from upstream v1.5.28.
- **Synced once a week.** New upstream features and fixes are merged in, tested, and pushed. The fork stays within a week of upstream.
- **Every sync is visible.** Each sync is one merge commit titled `chore(fork): sync upstream vX.Y.Z`; the most recent one tells you which upstream version the fork includes.
- **Changes stay small and independent.** Feature changes are kept in a form that can be submitted upstream as a PR. Fork-only content is this file, the notice at the top of each README, the update channel and the release script.
- **Merged upstream means handed back.** Once one of the PRs above merges upstream, the next sync brings it in as upstream code and the fork stops carrying it separately.

Weekly sync procedure (assuming `upstream` is the original repository and `origin` is this fork):

```bash
git fetch upstream main
git switch main
git merge --no-ff upstream/main -m "chore(fork): sync upstream vX.Y.Z"  # on conflicts: resolve, then git merge --continue
npm run validate:copy && npm run validate:locale && npm run validate:ui-hardcode && npm run validate:versions
npm --prefix dashboard run test && npm --prefix dashboard run build
git push origin main
```

`npm test` is upstream's full server-side suite and needs a database and other services; on Windows some upstream tests cannot pass in the first place.

**Releasing the Windows build.** The version is "upstream version + `-fork.<revision>`", for example `1.5.29-fork.1`. The revision lives in `FORK_REVISION` at the repository root and is bumped for each release. Upstream's version files stay identical to upstream, so a sync never conflicts on a version number. Commit everything first, then run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File TokenTrackerWin\scripts\build-fork-release.ps1
```

The script runs the tests and validators, builds the dashboard, bundles the Node runtime and CLI, publishes, and writes the zip, the installer and the checksums to `TokenTrackerWin\installer\Output\<version>\`. It only builds: it does not tag or publish. It needs the .NET 8 SDK and Inno Setup 6.

### How to get it

**Windows:** download `TokenTracker-Setup.exe` from [Releases](https://github.com/LceAn/TokenTracker/releases). It does not need administrator rights. It shares the official app's install location and product identity, so the two replace each other in place, and it leaves your data directory alone. An installed fork build can check for updates from Settings.

**macOS and Linux:** there are no installers; build from source the same way as upstream (see [CONTRIBUTING.md](./CONTRIBUTING.md)).

The install commands in the README (npm, Homebrew and so on) install the official release, without the changes above.

### Feedback

Questions and discussion about this fork currently happen in upstream issue [#1119](https://github.com/xiufengsun/TokenTracker/issues/1119).

### Thanks and license

Thanks to the original author [@xiufengsun](https://github.com/xiufengsun) and every upstream contributor — this fork is built entirely on their work.

MIT License. The upstream copyright notice is preserved in full in [LICENSE](./LICENSE); additions in this fork are released under the same license.

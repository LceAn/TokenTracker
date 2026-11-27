# TokenTracker · 社区分支

**简体中文** · [English](README.en.md)

本仓库是 [xiufengsun/TokenTracker](https://github.com/xiufengsun/TokenTracker)（作者 [@xiufengsun](https://github.com/xiufengsun)）的社区维护分支，由 [@LceAn](https://github.com/LceAn) 维护，遵循 MIT 协议。

Token Tracker 的全部基础能力都来自原作者。**完整的功能介绍、截图和使用说明请看上游仓库**：[中文](https://github.com/xiufengsun/TokenTracker/blob/main/README.zh-CN.md) · [English](https://github.com/xiufengsun/TokenTracker) · [日本語](https://github.com/xiufengsun/TokenTracker/blob/main/README.ja.md) · [한국어](https://github.com/xiufengsun/TokenTracker/blob/main/README.ko.md) · [Deutsch](https://github.com/xiufengsun/TokenTracker/blob/main/README.de.md)。这里只写两件事：这个工具是什么，以及本分支多了什么。

## 这个工具是什么

Token Tracker 是一个本地优先的 AI 用量统计工具。它读取你电脑上各个 AI 编程工具（Claude Code、Codex、Cursor、Gemini CLI 等）留下的记录，统计用了多少 token、花了多少钱，并在一个仪表盘里展示趋势和成本。

- **本地运行。** 解析和统计都在你自己的电脑上完成，不需要云账号和 API Key。
- **只读用量。** 只记录 token 数量、时间和模型名，不读取对话内容。
- **多种形态。** 命令行工具（npm 包）、网页仪表盘，以及 Windows、macOS、Linux 的桌面程序。

## 本分支多了什么

官方云服务推出付费订阅之后，想用云同步但不付费的用户会被引向“自己部署”；但客户端一直只认官方云，想换后端只能靠环境变量，设置里没有入口。本分支就是为了补上这条路，缘由见上游 issue [#1119](https://github.com/xiufengsun/TokenTracker/issues/1119)。

| 新增 | 说明 |
| --- | --- |
| **在设置里切换云端后端** | 「设置 → 云服务」里新增服务器卡片，可以在官方云和自部署实例之间切换（填地址和 anon key），也可以随时恢复官方云。 |
| **保存前测试连接** | 由本地服务探测目标地址是否可达，并区分证书错误和地址错误。 |
| **切换前确认** | 切换服务器会退出登录，所以先弹窗说明；表单里附了部署指南的链接。 |
| **显示当前服务器状态** | 打开设置时自动检查，显示「已连接」或「服务器不可达」；地址没写协议时自动补上 `https://`。 |
| **更新渠道可选**（仅 Windows） | 在设置里选择跟随本分支还是官方版本的发布，见下文。 |
| **「关于」页面** | 「设置 → 应用 → 关于」：项目介绍、版本、许可证和贡献者。 |
| **服务端部署指南** | 新增 [`server/README.md`](server/README.md)：怎样部署自己的云端，客户端怎样连上去。 |

前四项和「关于」页面已经作为 PR 提交给上游（[#1082](https://github.com/xiufengsun/TokenTracker/pull/1082)、[#1084](https://github.com/xiufengsun/TokenTracker/pull/1084)、[#1085](https://github.com/xiufengsun/TokenTracker/pull/1085)、[#1086](https://github.com/xiufengsun/TokenTracker/pull/1086)、[#1081](https://github.com/xiufengsun/TokenTracker/pull/1081)），通过了测试并按评审意见返工过；截至 2026-11-27 还没有合并，是否已合并请以上游页面为准。

## 下载

| 平台 | 怎么获取 |
| --- | --- |
| Windows | 到 [Releases](https://github.com/LceAn/TokenTracker/releases) 下载 `TokenTracker-Setup.exe`，不需要管理员权限。 |
| macOS、Linux | 暂无安装包。从源码构建的方法与上游相同，见 [CONTRIBUTING.md](CONTRIBUTING.md)。 |

通过 npm、Homebrew 安装的是官方版本，不包含上面的改动。

### 更新渠道（仅 Windows）

Windows 版可以在「设置 → App 与更新 → 更新渠道」里选择：

- **社区分支**（默认）：跟随本分支的发布。
- **官方版本**：跟随上游的发布。下一次更新会把本分支替换成官方版本，上表的功能会被移除。安装前一定会让你确认，不会在后台自己换掉。

换成官方版本之后，官方客户端里没有这个选项；想换回来需要重新安装本分支的安装包。macOS 和 Linux 版没有这个选项，只跟随官方发布。

## 仓库结构

```
desktop/   桌面程序：本地采集、本地服务、界面，以及 Windows / macOS / Linux 的宿主程序
server/    服务端：可以自己部署的云端（安装器、数据库结构、Cloudflare Worker）
```

想在设置里选「自部署实例」，需要先有一个自己的服务端。部署方法见 [server/README.md](server/README.md)。

## 怎么维护

- **以上游为基线。** `main` 分支 = 上游 `main` + 上表的改动。
- **每周同步一次上游。** 每次同步是一条合并提交，标题形如 `chore(fork): sync upstream vX.Y.Z`，看提交记录就知道包含到上游的哪个版本。
- **改动保持能提回上游的形态。** 上游合并了对应的 PR 之后，下一次同步时它就成为上游代码的一部分，本分支不再单独维护。

具体步骤见 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 反馈

本分支为什么存在、现在能做什么、接下来做什么，见仓库 Issues 里的置顶公告。

使用问题、部署问题和改进建议，欢迎在本仓库提 [Issue](https://github.com/LceAn/TokenTracker/issues)。请不要在 issue 里贴 token、密钥或 anon key。

## 许可

MIT License。上游的版权声明完整保留在 [LICENSE](LICENSE)，本分支新增的内容同样以 MIT 协议发布。

感谢原作者 @xiufengsun 和所有上游贡献者，本分支的一切都建立在他们的工作之上。

# desktop/ · 桌面程序 / The desktop app

**简体中文** · [English](#english)

回到仓库说明 / Back to the [repository README](../README.md) · [README.en.md](../README.en.md)

## 中文

这里是在用户电脑上运行的那一部分：本地采集、本地服务、界面，以及三个平台的宿主程序。服务端在 [`server/`](../server/README.md)。

| 路径 | 内容 |
| --- | --- |
| `package.json`、`src/`、`bin/` | npm 包 `tokentracker-cli`：读取各个 AI 编程工具的用量记录，在本地统计，并提供本地服务。三个平台的宿主程序都内嵌它 |
| `dashboard/` | 界面（React + Vite），构建后内嵌进各个宿主程序 |
| `TokenTrackerWin/` | Windows 托盘程序（.NET 8，WebView2），自带安装包脚本 |
| `TokenTrackerBar/` | macOS 菜单栏程序（Swift） |
| `TokenTrackerLinux/` | Linux 托盘程序（Tauri） |
| `test/`、`scripts/` | 测试、校验器和构建脚本 |
| `docs/` | 上游的内部文档 |
| `FORK_REVISION` | 本分支的修订号，发布 Windows 版时使用 |

这些目录在仓库里的位置变了，但每个目录内部的布局和上游完全相同，所以上游的构建说明、各目录下的 README 和 `CLAUDE.md` 里的命令都照常适用，只是要先进入 `desktop/`：

```bash
cd desktop
npm ci
npm --prefix dashboard ci
npm test                                   # 根测试套件
npm --prefix dashboard run test            # 界面测试
npm run dashboard:build                    # 构建界面
```

完整的命令清单、每周同步上游和发布 Windows 版的步骤见 [CONTRIBUTING.md](../CONTRIBUTING.md)。

---

## English

This is everything that runs on the user's machine: local collection, the local server, the dashboard, and the hosts for three platforms. The server side is in [`server/`](../server/README.en.md).

| Path | Contents |
| --- | --- |
| `package.json`, `src/`, `bin/` | the npm package `tokentracker-cli`: it reads the usage records of AI coding tools, aggregates them locally, and serves the local API. Every desktop host embeds it |
| `dashboard/` | the dashboard UI (React + Vite), built into each host |
| `TokenTrackerWin/` | the Windows tray app (.NET 8, WebView2), with its installer script |
| `TokenTrackerBar/` | the macOS menu bar app (Swift) |
| `TokenTrackerLinux/` | the Linux tray app (Tauri) |
| `test/`, `scripts/` | tests, validators and build scripts |
| `docs/` | upstream's internal documents |
| `FORK_REVISION` | this fork's revision number, used when releasing the Windows build |

These directories moved, but the layout inside each one is exactly upstream's. Upstream's build instructions, the README in each directory, and the commands in `CLAUDE.md` all still apply; you just start from `desktop/`:

```bash
cd desktop
npm ci
npm --prefix dashboard ci
npm test                                   # root test suite
npm --prefix dashboard run test            # dashboard tests
npm run dashboard:build                    # build the dashboard
```

For the full command list, the weekly upstream sync, and releasing the Windows build, see [CONTRIBUTING.md](../CONTRIBUTING.md).

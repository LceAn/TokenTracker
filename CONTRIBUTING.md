# 参与与维护 / Contributing and maintaining

[中文](#中文) · [English](#english)

## 中文

### 仓库结构

```
desktop/   桌面程序：在用户电脑上运行的那一部分
server/    服务端：可以自己部署的云端，见 server/README.md
```

`desktop/` 里是一个 npm 包加三个平台的宿主程序：

| 路径 | 内容 |
| --- | --- |
| `package.json`、`src/`、`bin/` | npm 包 `tokentracker-cli`：本地采集、本地服务、命令行 |
| `dashboard/` | 界面（React + Vite） |
| `TokenTrackerWin/`、`TokenTrackerWin.Tests/` | Windows 托盘程序（.NET 8）及其测试 |
| `TokenTrackerBar/` | macOS 菜单栏程序（Swift） |
| `TokenTrackerLinux/` | Linux 托盘程序（Tauri） |
| `test/`、`scripts/` | 测试、校验器和构建脚本 |
| `docs/` | 上游的内部文档 |
| `CLAUDE.md` | 给 AI 协作工具看的项目说明，人读也有用 |

`.github/workflows/` 是从上游继承的发布和运维流程。它们面向原作者的账号和基础设施，在本分支里不会运行；里面的路径仍按上游的目录结构书写，没有跟着本次重组修改。

### 开发环境

需要 Node.js 20 或更高版本。

```bash
cd desktop
npm ci
npm --prefix dashboard ci
```

构建 Windows 桌面程序另需 .NET 8 SDK；macOS 需要 Xcode 和 xcodegen；Linux 需要 Rust 和 Tauri 的依赖，见各目录下的 README。

### 常用命令

都在 `desktop/` 下执行：

```bash
npm test                                   # 根测试套件
node --test test/<name>.test.js            # 单个测试文件
npm run validate:copy                      # 文案登记是否完整
npm run validate:locale                    # 多语言覆盖率
npm run validate:ui-hardcode               # 界面里没有写死的文案
npm run validate:guardrails                # 架构约束
npm run validate:versions                  # 各处版本号是否一致
npm --prefix dashboard run test            # 界面测试
npm --prefix dashboard run build           # 构建界面到 dashboard/dist/
npm run dashboard:dev                      # 界面开发服务器
node bin/tracker.js serve --no-sync        # 本地仪表盘服务
dotnet test TokenTrackerWin.Tests/TokenTrackerWin.Tests.csproj -c Release   # Windows 测试
```

关于测试：

- `npm test` 是上游的完整测试套件。其中一部分依赖数据库、符号链接或 macOS 工具链，在 Windows 上本来就通不过。判断有没有改坏东西，请对比改动前后的**失败清单**，不要只看失败数量。
- 上游的两条规则：用户可见的文案一律登记在 `dashboard/src/content/copy.csv`，不写在组件里；`zh` 和 `zh-TW` 的覆盖率必须达到 100%。

### 提交约定

- 提交信息用英文，使用 `feat:`、`fix:`、`refactor:`、`docs:`、`test:`、`chore:`、`ci:` 这些前缀。
- 本分支独有的提交带 `(fork)` 作用域，例如 `feat(fork): …`，方便和上游的提交区分。
- 功能改动尽量保持能直接作为 PR 提交给上游的形态。

### 每周同步上游

假设 `upstream` 指向原仓库，`origin` 指向本分支：

```bash
git fetch upstream main
git switch main
git -c merge.renameLimit=30000 -c merge.directoryRenames=true merge --no-ff upstream/main -m "chore(fork): sync upstream vX.Y.Z"
```

目录整体搬进了 `desktop/` 和 `server/`，但内部布局没有变，所以 git 能跟上改名，把上游的改动和新文件自动落到新位置。`merge.directoryRenames=true` 的意思是直接接受新文件的新位置；不加的话，git 会把每个新文件都标成冲突让你确认，内容其实没有冲突。

合并后先看一眼仓库根目录：上游新增在根目录的文件不会被自动移动。属于 npm 包的，用 `git mv` 移进 `desktop/`；属于服务端的，移进 `server/`。然后验证：

```bash
cd desktop
npm run validate:copy && npm run validate:locale && npm run validate:ui-hardcode && npm run validate:versions
npm --prefix dashboard run test && npm --prefix dashboard run build
dotnet test TokenTrackerWin.Tests/TokenTrackerWin.Tests.csproj -c Release
```

遇到冲突时：

1. `README.md`、`README.en.md`、`CONTRIBUTING.md`、`FORK.md`、`server/README*.md`、`desktop/README.md` 是本分支自己的文档，保留本分支的版本。上游对 README 的修改不需要带过来。
2. 下面这些文件里，本分支只改过路径。保留本分支的路径写法，同时带上游的其它改动：
   - `desktop/test/` 下读取 `.github/workflows/` 的 8 个测试；
   - `desktop/test/`、`desktop/scripts/` 下读取 `server/migrations/` 的约 28 个文件；
   - `desktop/test/` 下调用 `server/self-host/` 的 3 个文件；
   - `server/self-host/` 下的 `manifest.cjs`、`build-functions.cjs`、`acceptance.cjs`；
   - `server/cloudflare/worker/` 下指向 `desktop/` 的十来个文件；
   - `server/docs/`、`desktop/docs/` 里跨目录的链接；
   - `.gitignore`、`.github/dependabot.yml`。
3. 上游从 v1.5.31 起更换了后端。同步到这些版本之前，要先适配「切换服务器」这个功能。

重组之后做过一次试合并（上游 v1.5.29 到 v1.5.33）：重组前没有冲突；重组后只有一个真正的冲突，是 `server/self-host/manifest.cjs`，上游和本分支都改了它。

### 发布 Windows 版

版本号是「上游版本 + `-fork.<修订号>`」，例如 `1.5.29-fork.1`。修订号记在 `desktop/FORK_REVISION`，每次发布加一。上游的版本号文件保持和上游一致，所以同步时不会在版本号上冲突。

先提交所有改动，把修订号加一，然后在 `desktop/` 下运行：

```powershell
powershell -ExecutionPolicy Bypass -File TokenTrackerWin\scripts\build-fork-release.ps1
```

脚本会跑测试和校验、构建界面、打包 Node 运行时和 CLI、发布、生成压缩包、安装包和校验文件，放在 `TokenTrackerWin\installer\Output\<版本号>\`。它只负责构建，不打标签也不发布。需要本机装有 .NET 8 SDK 和 Inno Setup 6。

之后的步骤：推送 `main` 和标签 `v<版本号>`，在 GitHub 上建 Release，上传三个文件，核对远端的摘要和本地一致后再发布。

---

## English

### Repository layout

```
desktop/   the desktop app: everything that runs on the user's machine
server/    the server: the cloud you can deploy yourself; see server/README.en.md
```

`desktop/` holds one npm package and the hosts for three platforms:

| Path | Contents |
| --- | --- |
| `package.json`, `src/`, `bin/` | the npm package `tokentracker-cli`: local collection, the local server, the CLI |
| `dashboard/` | the dashboard UI (React + Vite) |
| `TokenTrackerWin/`, `TokenTrackerWin.Tests/` | the Windows tray app (.NET 8) and its tests |
| `TokenTrackerBar/` | the macOS menu bar app (Swift) |
| `TokenTrackerLinux/` | the Linux tray app (Tauri) |
| `test/`, `scripts/` | tests, validators and build scripts |
| `docs/` | upstream's internal documents |
| `CLAUDE.md` | project notes written for AI coding tools; useful for people too |

`.github/workflows/` holds the release and operations workflows inherited from upstream. They target the original author's accounts and infrastructure and do not run in this fork. Their paths still follow upstream's layout and were not changed by the restructure.

### Development setup

Node.js 20 or newer.

```bash
cd desktop
npm ci
npm --prefix dashboard ci
```

Building the Windows app also needs the .NET 8 SDK; macOS needs Xcode and xcodegen; Linux needs Rust and Tauri's dependencies. See the README in each directory.

### Everyday commands

Run these from `desktop/`:

```bash
npm test                                   # root test suite
node --test test/<name>.test.js            # one test file
npm run validate:copy                      # copy registry completeness
npm run validate:locale                    # locale coverage
npm run validate:ui-hardcode               # no hardcoded UI strings
npm run validate:guardrails                # architecture guardrails
npm run validate:versions                  # version files agree
npm --prefix dashboard run test            # dashboard tests
npm --prefix dashboard run build           # build the dashboard to dashboard/dist/
npm run dashboard:dev                      # dashboard dev server
node bin/tracker.js serve --no-sync        # local dashboard server
dotnet test TokenTrackerWin.Tests/TokenTrackerWin.Tests.csproj -c Release   # Windows tests
```

About the tests:

- `npm test` is upstream's full suite. Part of it needs a database, symbolic links or the macOS toolchain, and cannot pass on Windows in the first place. To tell whether you broke something, compare the **list of failing tests** before and after your change, not the number.
- Two upstream rules: user-visible text lives in `dashboard/src/content/copy.csv`, never in a component; `zh` and `zh-TW` coverage must be 100%.

### Commit conventions

- Commit messages are in English, with the prefixes `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`, `ci:`.
- Commits that exist only in this fork carry a `(fork)` scope, for example `feat(fork): …`, so they are easy to tell from upstream's commits.
- Keep feature changes in a form that can be submitted upstream as a PR.

### Weekly sync from upstream

Assuming `upstream` is the original repository and `origin` is this fork:

```bash
git fetch upstream main
git switch main
git -c merge.renameLimit=30000 -c merge.directoryRenames=true merge --no-ff upstream/main -m "chore(fork): sync upstream vX.Y.Z"
```

The directories moved into `desktop/` and `server/` as whole units and kept their internal layout, so git follows the renames and lands upstream's changes, and its new files, in the new places by itself. `merge.directoryRenames=true` accepts those new locations outright; without it git marks every new file as a conflict for you to confirm, although nothing in the content conflicts.

After the merge, look at the repository root: additions upstream made there are not moved automatically. Move the ones that belong to the npm package into `desktop/` with `git mv`, and the ones that belong to the server into `server/`. Then verify:

```bash
cd desktop
npm run validate:copy && npm run validate:locale && npm run validate:ui-hardcode && npm run validate:versions
npm --prefix dashboard run test && npm --prefix dashboard run build
dotnet test TokenTrackerWin.Tests/TokenTrackerWin.Tests.csproj -c Release
```

When there are conflicts:

1. `README.md`, `README.en.md`, `CONTRIBUTING.md`, `FORK.md`, `server/README*.md` and `desktop/README.md` are this fork's own documents. Keep the fork's version; upstream's README changes do not need to come across.
2. In the files below, this fork changed only paths. Keep the fork's path spelling and take upstream's other changes:
   - the 8 tests under `desktop/test/` that read `.github/workflows/`;
   - the 28 or so files under `desktop/test/` and `desktop/scripts/` that read `server/migrations/`;
   - the 3 files under `desktop/test/` that call into `server/self-host/`;
   - `manifest.cjs`, `build-functions.cjs` and `acceptance.cjs` under `server/self-host/`;
   - the dozen or so files under `server/cloudflare/worker/` that import from `desktop/`;
   - the cross-folder links in `server/docs/` and `desktop/docs/`;
   - `.gitignore` and `.github/dependabot.yml`.
3. Upstream switched its backend from v1.5.31. Before syncing to those versions, the server switch needs adapting.

A trial merge was done right after the restructure (upstream v1.5.29 to v1.5.33). Before the restructure it had no conflicts. After it, the only real conflict was `server/self-host/manifest.cjs`, which both upstream and this fork had changed.

### Releasing the Windows build

The version is "upstream version + `-fork.<revision>`", for example `1.5.29-fork.1`. The revision lives in `desktop/FORK_REVISION` and is bumped for each release. Upstream's version files stay identical to upstream, so a sync never conflicts on a version number.

Commit everything, bump the revision, then run from `desktop/`:

```powershell
powershell -ExecutionPolicy Bypass -File TokenTrackerWin\scripts\build-fork-release.ps1
```

The script runs the tests and validators, builds the dashboard, bundles the Node runtime and the CLI, publishes, and writes the zip, the installer and the checksums to `TokenTrackerWin\installer\Output\<version>\`. It only builds: it does not tag or publish. It needs the .NET 8 SDK and Inno Setup 6.

Then: push `main` and the tag `v<version>`, create the GitHub Release, upload the three files, and publish only after the remote digests match the local ones.

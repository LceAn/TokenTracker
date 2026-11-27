# TokenTracker · community fork

[简体中文](README.md) · **English**

This repository is a community-maintained fork of [xiufengsun/TokenTracker](https://github.com/xiufengsun/TokenTracker) (by [@xiufengsun](https://github.com/xiufengsun)), maintained by [@LceAn](https://github.com/LceAn) under the MIT license.

Everything Token Tracker can do comes from the original author. **For the full feature tour, screenshots and usage guides, see the upstream repository**: [English](https://github.com/xiufengsun/TokenTracker) · [中文](https://github.com/xiufengsun/TokenTracker/blob/main/README.zh-CN.md) · [日本語](https://github.com/xiufengsun/TokenTracker/blob/main/README.ja.md) · [한국어](https://github.com/xiufengsun/TokenTracker/blob/main/README.ko.md) · [Deutsch](https://github.com/xiufengsun/TokenTracker/blob/main/README.de.md). This page covers only two things: what the tool is, and what this fork adds.

## What the tool is

Token Tracker is a local-first usage tracker for AI coding tools. It reads the records that tools such as Claude Code, Codex, Cursor and Gemini CLI leave on your machine, counts the tokens and the cost, and shows trends and spending in one dashboard.

- **Runs locally.** Parsing and aggregation happen on your own computer. No cloud account, no API key.
- **Usage only.** It records token counts, timestamps and model names. It never reads the conversation.
- **Several forms.** A command-line tool (npm package), a web dashboard, and desktop apps for Windows, macOS and Linux.

## What this fork adds

When the official cloud became a paid subscription, users who wanted cloud sync without paying were pointed at self-hosting. The client, however, only ever recognised the official cloud: switching the backend meant environment variables, and Settings had no entry for it. This fork exists to close that gap. Background: upstream issue [#1119](https://github.com/xiufengsun/TokenTracker/issues/1119).

| Addition | What it does |
| --- | --- |
| **Switch the cloud backend in Settings** | A Server card under Settings → Cloud switches between the official cloud and a self-hosted instance (address + anon key), and restores the official cloud at any time. |
| **Test the connection before saving** | The local server probes the target for reachability, and tells certificate errors apart from address errors. |
| **Confirm before switching** | Switching servers signs you out, so the card asks first; the form links to the deployment guide. |
| **Show the active server's status** | Opening Settings checks the backend and shows Connected or Server unreachable; an address without a scheme gets `https://` added. |
| **Choice of update channel** (Windows only) | Settings picks whether the app follows this fork's releases or the official ones. See below. |
| **About page** | Settings → App → About: project introduction, version, license and contributors. |
| **Server deployment guide** | A new [`server/README.md`](server/README.md) (English: [`server/README.en.md`](server/README.en.md)): how to deploy your own cloud, and how to point a client at it. |

The first four and the About page were submitted upstream as PRs ([#1082](https://github.com/xiufengsun/TokenTracker/pull/1082), [#1084](https://github.com/xiufengsun/TokenTracker/pull/1084), [#1085](https://github.com/xiufengsun/TokenTracker/pull/1085), [#1086](https://github.com/xiufengsun/TokenTracker/pull/1086), [#1081](https://github.com/xiufengsun/TokenTracker/pull/1081)). They pass their tests and were reworked after review; as of 2026-11-27 none had been merged. Check the PR pages for the current state.

## Download

| Platform | How to get it |
| --- | --- |
| Windows | Download `TokenTracker-Setup.exe` from [Releases](https://github.com/LceAn/TokenTracker/releases). It does not need administrator rights. |
| macOS, Linux | No installers yet. Build from source the same way as upstream; see [CONTRIBUTING.md](CONTRIBUTING.md). |

The npm and Homebrew packages are the official release and do not include the additions above.

### Update channel (Windows only)

The Windows app has an **Update channel** row under Settings → App & Updates:

- **Community fork** (the default): follows this fork's releases.
- **Official release**: follows the upstream releases. The next update then replaces the fork with the official version, which removes the additions above. You are always asked to confirm first; it never switches on its own in the background.

Once you are on the official release, the official app has no such option. To come back, run this fork's installer again. The macOS and Linux apps do not have the option and keep following the official releases.

## Repository layout

```
desktop/   the desktop app: local collection, the local server, the dashboard, and the Windows / macOS / Linux hosts
server/    the server: the cloud you can deploy yourself (installer, database schema, Cloudflare Worker)
```

To pick a self-hosted instance in Settings you first need a server of your own. See [server/README.en.md](server/README.en.md).

## How it is maintained

- **Upstream is the baseline.** `main` = upstream `main` + the additions above.
- **Synced from upstream once a week.** Each sync is one merge commit titled `chore(fork): sync upstream vX.Y.Z`, so the commit log shows which upstream version the fork includes.
- **Changes stay in a form that can go back upstream.** Once upstream merges the matching PR, the next sync brings it in as upstream code and the fork stops carrying it separately.

Step by step: [CONTRIBUTING.md](CONTRIBUTING.md).

## Feedback

Why this fork exists, what it can do now and what comes next: see the pinned announcement among the repository's issues.

Usage questions, deployment problems and suggestions are welcome as [issues](https://github.com/LceAn/TokenTracker/issues) in this repository. Please never paste tokens, secrets or anon keys into an issue.

## License

MIT License. The upstream copyright notice is preserved in full in [LICENSE](LICENSE); additions in this fork are released under the same license.

Thanks to the original author @xiufengsun and every upstream contributor. This fork is built entirely on their work.

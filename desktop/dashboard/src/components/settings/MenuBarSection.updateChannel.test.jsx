import React from "react";
import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MenuBarSection, NativeAppFooter } from "./MenuBarSection.jsx";

const nativeSettingsMock = vi.hoisted(() => ({
  available: true,
  settings: {},
  setSetting: vi.fn(),
  runAction: vi.fn(),
}));

vi.mock("../../hooks/use-native-settings.js", () => ({
  useNativeSettings: () => ({
    available: nativeSettingsMock.available,
    settings: nativeSettingsMock.settings,
    setSetting: nativeSettingsMock.setSetting,
    runAction: nativeSettingsMock.runAction,
  }),
}));

vi.mock("../../hooks/useLocale.js", () => ({
  useLocale: () => ({ resolvedLocale: "en" }),
}));

vi.mock("../../lib/copy", () => ({
  copy: (key, params) => (params ? `${key}:${JSON.stringify(params)}` : key),
}));

const showToast = vi.hoisted(() => vi.fn());
vi.mock("../../ui/components/Toast.jsx", () => ({
  showToast: (...args) => showToast(...args),
}));

// What the Windows app pushes for a community-fork build.
const FORK_SETTINGS = {
  platform: "windows",
  autoUpdateEnabled: true,
  launchAtLogin: false,
  launchAtLoginSupported: false,
  updateChannelSupported: true,
  updateChannel: "fork",
  updateBusy: false,
  isSyncing: false,
  version: "2.0.0",
};

const CHANNEL_LABEL = "settings.menubar.updateChannel";

function renderSection() {
  return render(
    <MemoryRouter>
      <MenuBarSection />
    </MemoryRouter>,
  );
}

function renderFooter(Footer = NativeAppFooter) {
  return render(
    <MemoryRouter>
      <Footer />
    </MemoryRouter>,
  );
}

async function choose(label, option) {
  const user = userEvent.setup();
  await act(async () => {
    await user.click(screen.getByRole("combobox", { name: label }));
  });
  const listbox = await screen.findByRole("listbox", { name: label });
  await act(async () => {
    await user.click(within(listbox).getByRole("option", { name: option }));
  });
}

describe("MenuBarSection update channel", () => {
  beforeEach(() => {
    nativeSettingsMock.available = true;
    nativeSettingsMock.settings = { ...FORK_SETTINGS };
    nativeSettingsMock.setSetting.mockReset();
    nativeSettingsMock.runAction.mockReset();
  });

  it("offers the channel choice on a fork build and follows the fork by default", () => {
    renderSection();

    expect(screen.getByRole("combobox", { name: CHANNEL_LABEL })).toHaveTextContent(
      "settings.menubar.updateChannel.fork",
    );
    expect(screen.getByText("settings.menubar.updateChannelHint.fork")).toBeInTheDocument();
  });

  it("hides the channel choice where the build cannot pick one", () => {
    nativeSettingsMock.settings = { ...FORK_SETTINGS, updateChannelSupported: false, updateChannel: undefined };

    renderSection();

    expect(screen.queryByRole("combobox", { name: CHANNEL_LABEL })).toBeNull();
    expect(screen.queryByText(/settings\.menubar\.updateChannelHint/)).toBeNull();
  });

  it("sends the chosen channel to the native app", async () => {
    renderSection();

    await choose(CHANNEL_LABEL, "settings.menubar.updateChannel.official");

    expect(nativeSettingsMock.setSetting).toHaveBeenCalledWith("updateChannel", "official");
  });

  it("explains what following the official releases means once it is selected", () => {
    nativeSettingsMock.settings = { ...FORK_SETTINGS, updateChannel: "official" };

    renderSection();

    expect(screen.getByRole("combobox", { name: CHANNEL_LABEL })).toHaveTextContent(
      "settings.menubar.updateChannel.official",
    );
    expect(screen.getByText("settings.menubar.updateChannelHint.official")).toBeInTheDocument();
  });

  it("keeps the channel selector locked while an update is running", () => {
    nativeSettingsMock.settings = { ...FORK_SETTINGS, updateBusy: true };

    renderSection();

    expect(screen.getByRole("combobox", { name: CHANNEL_LABEL })).toHaveAttribute("disabled");
  });

  it("shows the fork's own version in the footer", () => {
    renderFooter();

    expect(
      screen.getByText('settings.menubar.updates.footerCore:{"version":"2.0.0"}'),
    ).toBeInTheDocument();
  });
});

describe("NativeAppFooter check for updates without the native app", () => {
  const forkReleasePage = (tag) => `https://github.com/LceAn/TokenTracker/releases/tag/${tag}`;
  const release = (tag, htmlUrl = forkReleasePage(tag)) => ({
    ok: true,
    json: async () => ({ tag_name: tag, html_url: htmlUrl }),
  });

  let fetchMock;
  let user;

  beforeEach(() => {
    nativeSettingsMock.available = false;
    nativeSettingsMock.settings = {};
    showToast.mockReset();
    fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    vi.stubEnv("VITE_APP_VERSION", "2.0.0");
    user = userEvent.setup();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  const check = async () => {
    renderFooter();
    await act(async () => {
      await user.click(screen.getByRole("button", { name: "settings.menubar.checkUpdates" }));
    });
  };

  it("checks the official repository unless the build says otherwise", async () => {
    fetchMock.mockResolvedValue(release("v1.5.34", "https://github.com/xiufengsun/TokenTracker/releases/tag/v1.5.34"));

    await check();

    expect(fetchMock).toHaveBeenCalledWith(
      "https://api.github.com/repos/xiufengsun/TokenTracker/releases/latest",
    );
  });

  it("offers a newer fork release", async () => {
    fetchMock.mockResolvedValue(release("v2.0.1"));

    await check();

    expect(await screen.findByRole("dialog")).toHaveTextContent(
      'settings.menubar.updates.modalDescription:{"version":"v2.0.1"}',
    );
    expect(showToast).not.toHaveBeenCalled();
  });

  it("offers the fork's own numbering to the fork's first release", async () => {
    // 1.5.29-fork.1 carried the upstream version plus a revision, before the fork started at 2.0.0.
    vi.stubEnv("VITE_APP_VERSION", "1.5.29-fork.1");
    fetchMock.mockResolvedValue(release("v2.0.0"));

    await check();

    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    expect(showToast).not.toHaveBeenCalled();
  });

  it.each(["v2.0.0", "v1.9.9", "v1.5.29-fork.2"])(
    "stays quiet when the fork release %s is not newer",
    async (tag) => {
      fetchMock.mockResolvedValue(release(tag));

      await check();

      expect(screen.queryByRole("dialog")).toBeNull();
      expect(showToast).toHaveBeenCalledWith({ title: "settings.menubar.updates.upToDate" });
    },
  );

  it("never treats a tag it cannot parse as an update", async () => {
    fetchMock.mockResolvedValue(release("nightly"));

    await check();

    expect(screen.queryByRole("dialog")).toBeNull();
    expect(showToast).toHaveBeenCalledWith({ title: "settings.menubar.updates.upToDate" });
  });

  it("checks the fork repository when the build points at it", async () => {
    vi.resetModules();
    vi.stubEnv("VITE_RELEASE_REPO", "LceAn/TokenTracker");
    fetchMock.mockResolvedValue(release("v2.0.1"));
    const { NativeAppFooter: ForkFooter } = await import("./MenuBarSection.jsx");

    renderFooter(ForkFooter);
    await act(async () => {
      await user.click(screen.getByRole("button", { name: "settings.menubar.checkUpdates" }));
    });

    expect(fetchMock).toHaveBeenCalledWith(
      "https://api.github.com/repos/LceAn/TokenTracker/releases/latest",
    );
  });
});

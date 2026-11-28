import React from "react";
import { Link } from "react-router-dom";
import { Activity, ArrowUpRight, Download, RefreshCw } from "lucide-react";
import { useNativeSettings } from "../../hooks/use-native-settings.js";
import { useLocale } from "../../hooks/useLocale.js";
import { ConfirmModal } from "../../ui/components";
import { showToast } from "../../ui/components/Toast.jsx";
import { STATUSPAGE_URL } from "../../lib/config";
import { copy } from "../../lib/copy";
import { cn } from "../../lib/cn";
import { isNativeWindowsApp } from "../../lib/native-bridge.js";
import { SectionCard, SettingsRow, ToggleSwitch } from "./Controls.jsx";
import { Button } from "../../ui/components/Button.jsx";
import { Select } from "../../ui/components";

// Where browser / CLI mode looks for releases. The native apps have their own
// updater; this only serves the footer shown when that bridge is absent. A
// community-fork build points it at the fork's repository at build time.
const RELEASE_REPO = import.meta.env.VITE_RELEASE_REPO || "xiufengsun/TokenTracker";

export function MenuBarSection() {
  const { available, settings, setSetting, runAction } = useNativeSettings();
  if (!available) return null;
  const isWindows = isNativeWindowsApp();

  // showStats + animatedIcon live on the Widgets page (Menu Bar section) where
  // they sit next to the live preview. This section keeps only the system-level
  // toggles + actions that don't have a visual analogue.
  const launchAtLogin = Boolean(settings?.launchAtLogin);
  const toastOnReset = settings?.toastOnReset !== false;
  const confettiOnReset = settings?.confettiOnReset !== false;
  const launchAtLoginSupported = settings?.launchAtLoginSupported !== false;
  const autoUpdateEnabled = settings?.autoUpdateEnabled !== false;
  const updateStatus = settings?.updateStatus || null;
  const updateBusy = Boolean(settings?.updateBusy);
  const isSyncing = Boolean(settings?.isSyncing);
  // Only a fork build offers a choice (updateChannelSupported); it follows its
  // own releases unless the user picks the official ones.
  const updateChannelSupported = settings?.updateChannelSupported === true;
  const updateChannel = settings?.updateChannel === "official" ? "official" : "fork";
  const updateChannelOptions = [
    { value: "fork", label: copy("settings.menubar.updateChannel.fork") },
    { value: "official", label: copy("settings.menubar.updateChannel.official") },
  ];
  const updateChannelHint = updateChannel === "official"
    ? copy("settings.menubar.updateChannelHint.official")
    : copy("settings.menubar.updateChannelHint.fork");

  return (
    <SectionCard title={copy("settings.section.menubar")}>
      {!isWindows ? (
        <>
          <SettingsRow
            label={copy("settings.menubar.appearance")}
            hint={copy("settings.menubar.appearanceHint")}
            control={
              <Button
                as={Link}
                to="/widgets"
                variant="secondary"
                size="sm"
                className="gap-1.5 no-underline"
              >
                <span>{copy("settings.menubar.appearanceLink")}</span>
                <ArrowUpRight className="h-3.5 w-3.5" aria-hidden />
              </Button>
            }
          />
          <SettingsRow
            label={copy("settings.menubar.toastOnReset")}
            hint={copy("settings.menubar.toastOnResetHint")}
            control={
              <ToggleSwitch
                checked={toastOnReset}
                onChange={() => setSetting("toastOnReset", !toastOnReset)}
                ariaLabel={copy("settings.menubar.toastOnReset")}
              />
            }
          />
          <SettingsRow
            label={copy("settings.menubar.confettiOnReset")}
            hint={copy("settings.menubar.confettiOnResetHint")}
            control={
              <ToggleSwitch
                checked={confettiOnReset}
                onChange={() => setSetting("confettiOnReset", !confettiOnReset)}
                ariaLabel={copy("settings.menubar.confettiOnReset")}
              />
            }
          />
        </>
      ) : null}
      {launchAtLoginSupported ? (
        <SettingsRow
          label={copy("settings.menubar.launchAtLogin")}
          hint={copy("settings.menubar.launchAtLoginHint")}
          control={
            <ToggleSwitch
              checked={launchAtLogin}
              onChange={() => setSetting("launchAtLogin", !launchAtLogin)}
              ariaLabel={copy("settings.menubar.launchAtLogin")}
            />
          }
        />
      ) : null}
      <SettingsRow
        label={copy("settings.menubar.syncNow")}
        hint={copy("settings.menubar.syncNowHint")}
        control={
          <Button
            variant="secondary"
            size="sm"
            onClick={() => runAction("syncNow")}
            disabled={isSyncing}
            className="gap-1.5"
          >
            <RefreshCw className={cn("h-3.5 w-3.5", isSyncing && "animate-spin")} aria-hidden />
            <span>{isSyncing ? copy("settings.menubar.syncing") : copy("settings.menubar.syncNow")}</span>
          </Button>
        }
      />
      <SettingsRow
        label={copy("settings.menubar.autoUpdate")}
        hint={copy("settings.menubar.autoUpdateHint")}
        control={
          <ToggleSwitch
            checked={autoUpdateEnabled}
            onChange={() => setSetting("autoUpdateEnabled", !autoUpdateEnabled)}
            ariaLabel={copy("settings.menubar.autoUpdate")}
          />
        }
      />
      {updateChannelSupported ? (
        <SettingsRow
          label={copy("settings.menubar.updateChannel")}
          hint={updateChannelHint}
          control={
            <Select
              value={updateChannel}
              onValueChange={(value) => setSetting("updateChannel", value)}
              options={updateChannelOptions}
              ariaLabel={copy("settings.menubar.updateChannel")}
              disabled={updateBusy}
              className="px-3 py-1.5 text-xs font-medium"
            />
          }
        />
      ) : null}
      <SettingsRow
        label={copy("settings.menubar.updates")}
        hint={updateStatus || undefined}
        control={
          <Button
            variant="secondary"
            size="sm"
            onClick={() => runAction("checkForUpdates")}
            disabled={updateBusy}
            className="gap-1.5"
          >
            <Download className="h-3.5 w-3.5" aria-hidden />
            <span>{copy("settings.menubar.checkUpdates")}</span>
          </Button>
        }
      />
    </SectionCard>
  );
}

// Accepts "v2.0.0" and the fork's first release, which was tagged after
// upstream ("v1.5.29-fork.1") before the fork started its own numbering.
// Anything else is not an update: a tag that cannot be parsed must never turn
// into a download prompt.
function parseReleaseVersion(value) {
  const match = /^v?(\d+)\.(\d+)\.(\d+)(?:-fork\.(\d+))?$/.exec(
    String(value || "").trim(),
  );
  if (!match) return null;
  return {
    base: match.slice(1, 4).map(Number),
    fork: match[4] === undefined ? -1 : Number(match[4]),
  };
}

function hasUpdate(current, latest) {
  const curr = parseReleaseVersion(current);
  const late = parseReleaseVersion(latest);
  if (!curr || !late) return false;
  for (let i = 0; i < curr.base.length; i += 1) {
    if (curr.base[i] !== late.base[i]) return curr.base[i] < late.base[i];
  }
  return late.fork > curr.fork;
}

export function NativeAppFooter() {
  const { available, settings, runAction } = useNativeSettings();
  const showNativeInfo = available && settings?.version;
  const nativePlatform = settings?.platform || "macos";
  const [checking, setChecking] = React.useState(false);
  const [updateModal, setUpdateModal] = React.useState({
    open: false,
    latestVersion: "",
    htmlUrl: "",
  });
  const { resolvedLocale } = useLocale();
  const currentVersion = import.meta.env.VITE_APP_VERSION || "0.64.2";

  const handleCheckUpdates = async () => {
    setChecking(true);
    try {
      const res = await fetch(`https://api.github.com/repos/${RELEASE_REPO}/releases/latest`);
      if (!res.ok) throw new Error("Failed to fetch");
      const data = await res.json();
      const latestVersion = data.tag_name;

      if (hasUpdate(currentVersion, latestVersion)) {
        setUpdateModal({
          open: true,
          latestVersion,
          htmlUrl: data.html_url
        });
      } else {
        showToast({
          title: copy("settings.menubar.updates.upToDate"),
        });
      }
    } catch (err) {
      console.error("Check updates failed:", err);
      showToast({
        title: copy("settings.menubar.updates.failed"),
      });
    } finally {
      setChecking(false);
    }
  };

  const modalTitle = copy("settings.menubar.updates.modalTitle");
  const modalDescription = copy("settings.menubar.updates.modalDescription", {
    version: updateModal.latestVersion,
  });
  const modalConfirm = copy("settings.menubar.updates.modalConfirm");
  const modalCancel = copy("settings.menubar.updates.modalCancel");

  return (
    <div className="mt-6 flex flex-col items-center justify-center gap-4 text-xs text-oai-gray-500 dark:text-oai-gray-500">
      <div className="flex flex-wrap items-center justify-center gap-2">
        {showNativeInfo ? (
          <>
            <span>
              {nativePlatform === "windows"
                ? copy("settings.menubar.updates.footerCore", { version: settings.version })
                : copy("settings.menubar.updates.footerCombined", {
                    barVersion: settings.version,
                    coreVersion: currentVersion,
                  })}
            </span>
            <span aria-hidden>·</span>
            <button
              type="button"
              onClick={() => runAction("openAbout")}
              className="underline-offset-2 transition-colors hover:text-oai-gray-700 hover:underline dark:hover:text-oai-gray-300"
            >
              GitHub
            </button>
            <span aria-hidden>·</span>
          </>
        ) : (
          <>
            <span>{copy("settings.menubar.updates.footerCore", { version: currentVersion })}</span>
            <span aria-hidden>·</span>
            <button
              type="button"
              onClick={handleCheckUpdates}
              disabled={checking}
              className="underline-offset-2 transition-colors hover:text-oai-gray-700 hover:underline dark:hover:text-oai-gray-300 disabled:opacity-50"
            >
              {checking ? copy("settings.menubar.updates.checking") : copy("settings.menubar.checkUpdates")}
            </button>
            <span aria-hidden>·</span>
          </>
        )}
        <a
          href={STATUSPAGE_URL}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-1.5 underline-offset-2 transition-colors hover:text-oai-gray-700 hover:underline dark:hover:text-oai-gray-300"
        >
          <Activity className="h-3.5 w-3.5" aria-hidden />
          {copy("settings.footer.statusPage")}
        </a>
      </div>

      <ConfirmModal
        open={updateModal.open}
        title={modalTitle}
        description={modalDescription}
        confirmLabel={modalConfirm}
        cancelLabel={modalCancel}
        onConfirm={() => {
          window.open(updateModal.htmlUrl, "_blank");
          setUpdateModal((prev) => ({ ...prev, open: false }));
        }}
        onCancel={() => setUpdateModal((prev) => ({ ...prev, open: false }))}
      />
    </div>
  );
}

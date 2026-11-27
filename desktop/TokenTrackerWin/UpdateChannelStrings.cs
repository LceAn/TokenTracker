namespace TokenTrackerWin;

/// <summary>
/// Localized strings for an update that crosses channels: a community-fork
/// build switching to an official release. Kept apart from
/// <see cref="UpdateStrings"/> because an official build never needs them.
/// </summary>
internal sealed record UpdateChannelStrings(
    string SwitchTitle,
    string SwitchPrompt,   // "{0}" = official version, "{1}" = current version
    string SwitchNow,      // "{0}" = official version (tray menu item)
    string SwitchBalloon)  // "{0}" = official version
{
    public static UpdateChannelStrings For(string locale)
    {
        return locale switch
        {
            NativeLocalization.ChineseLocale => new(
                "切换到官方版本",
                "这会把社区分支版本（当前 {1}）替换为官方版本 {0}，社区分支独有的功能会被移除。\n之后想回到社区分支，需要重新安装社区分支的安装包。\n\n现在切换吗？",
                "切换到官方版本 {0}",
                "官方版本 {0} 可用，点击托盘菜单即可切换。"),
            NativeLocalization.TraditionalChineseLocale => new(
                "切換到官方版本",
                "這會把社群分支版本（目前 {1}）替換為官方版本 {0}，社群分支獨有的功能會被移除。\n之後想回到社群分支，需要重新安裝社群分支的安裝檔。\n\n現在切換嗎？",
                "切換到官方版本 {0}",
                "官方版本 {0} 可用，點擊系統匣選單即可切換。"),
            NativeLocalization.JapaneseLocale => new(
                "公式バージョンに切り替え",
                "コミュニティフォーク版（現在 {1}）を公式バージョン {0} に置き換えます。フォーク独自の機能は削除されます。\nフォークに戻すには、フォークのインストーラーをもう一度実行する必要があります。\n\n切り替えますか？",
                "公式バージョン {0} に切り替え",
                "公式バージョン {0} が利用可能です。トレイメニューから切り替えられます。"),
            NativeLocalization.KoreanLocale => new(
                "공식 버전으로 전환",
                "커뮤니티 포크 버전(현재 {1})을 공식 버전 {0}(으)로 교체합니다. 포크 전용 기능은 제거됩니다.\n포크로 돌아오려면 포크 설치 파일을 다시 실행해야 합니다.\n\n전환할까요?",
                "공식 버전 {0}(으)로 전환",
                "공식 버전 {0}을(를) 사용할 수 있습니다. 트레이 메뉴에서 전환하세요."),
            _ => new(
                "Switch to the official release",
                "This replaces the community fork build (current {1}) with the official release {0}. Features that exist only in the fork will be removed.\nTo come back to the fork afterwards, install the fork's setup again.\n\nSwitch now?",
                "Switch to official {0}",
                "Official release {0} is available — choose it in the tray menu to switch."),
        };
    }
}

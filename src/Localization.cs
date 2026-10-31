using System;

namespace BlackHoleBin
{
    internal enum AppLanguage
    {
        Chinese = 0,
        English = 1
    }

    internal enum ProtectionMode
    {
        Default = 0,
        Blacklist = 1,
        Whitelist = 2
    }

    internal enum RecoveryCleanupMode
    {
        Manual = 0,
        OnStartup = 1
    }

    internal enum VisualTheme
    {
        CosmicBlue = 0,
        NebulaPurple = 1,
        SolarAmber = 2
    }

    internal static class L
    {
        public static AppLanguage Language { get; set; }

        public static string Text(string key)
        {
            return Get(key, Language);
        }

        public static string Get(string key, AppLanguage language)
        {
            bool en = language == AppLanguage.English;
            switch (key)
            {
                case "AppName": return en ? "Black Hole Bin" : "黑洞回收站";
                case "DropHere": return en ? "Drop files here" : "拖文件到这里";
                case "ResizeHint": return en ? "Drag edge to resize" : "拖动边缘调整大小";
                case "DropRelease": return en ? "Release to recycle" : "释放以移入回收站";
                case "Checking": return en ? "Checking..." : "正在检查…";
                case "NoItems": return en ? "No supported items" : "没有可处理的项目";
                case "NoAction": return en ? "Nothing was changed" : "未执行任何操作";
                case "Cancelled": return en ? "Cancelled" : "已取消";
                case "Recycled": return en ? "Moved to Recycle Bin" : "已移入回收站";
                case "RecycleIncomplete": return en ? "Recycle incomplete" : "回收未完成";
                case "RecycleFailed": return en ? "Recycle failed" : "回收失败";
                case "Hide": return en ? "Hide black hole" : "隐藏黑洞";
                case "Show": return en ? "Show black hole" : "显示黑洞";
                case "Settings": return en ? "Settings..." : "设置…";
                case "ResizeMode": return en ? "Drag-resize mode" : "拖拽调整大小";
                case "Undo": return en ? "Undo last recycle" : "撤回上一次回收";
                case "Exit": return en ? "Exit" : "退出";
                case "SettingsTitle": return en ? "Black Hole Bin Settings" : "黑洞回收站设置";
                case "GeneralTab": return en ? "General" : "常规";
                case "SafetyTab": return en ? "Safety rules" : "安全规则";
                case "RecoveryTab": return en ? "Recovery" : "恢复规则";
                case "AppearanceTab": return en ? "Appearance" : "外观";
                case "Animation": return en ? "Enable black-hole animation" : "启用黑洞动画";
                case "AnimationHint": return en ? "Rotating accretion ring, orbiting particles and breathing glow. Disable it for the lowest resource use." : "包含旋转吸积环、轨道粒子和呼吸光晕；关闭后资源占用最低。";
                case "ColorTheme": return en ? "Color theme:" : "配色主题：";
                case "CosmicBlue": return en ? "Cosmic blue" : "深空蓝";
                case "NebulaPurple": return en ? "Nebula purple" : "星云紫";
                case "SolarAmber": return en ? "Solar amber" : "日冕金";
                case "EffectIntensity": return en ? "Effect intensity" : "特效强度";
                case "EffectHint": return en ? "Controls glow, particle density and interaction feedback." : "控制光晕、粒子密度和交互反馈强度。";
                case "Language": return en ? "Language" : "界面语言";
                case "Display": return en ? "Display" : "显示";
                case "AlwaysTop": return en ? "Always stay on top" : "永远显示在最顶端";
                case "HideMaximized": return en ? "Hide when the foreground app is maximized or full screen" : "当前应用最大化或全屏时自动隐藏";
                case "HideHint": return en ? "Use the tray icon to show it again." : "自动隐藏后仍可通过托盘图标重新显示。";
                case "Behavior": return en ? "Behavior" : "行为";
                case "Startup": return en ? "Start after signing in to Windows" : "登录 Windows 后自动启动";
                case "Confirm": return en ? "Ask before moving items to Recycle Bin" : "回收文件前显示确认窗口";
                case "HoleSize": return en ? "Black hole size" : "黑洞大小";
                case "EnableResize": return en ? "Enable drag-resize mode" : "启用拖拽调整大小模式";
                case "SizeLiveHint": return en ? "Drag the slider to resize the desktop black hole immediately." : "拖动滑槽时，桌面黑洞会立即缩放。";
                case "MaxFile": return en ? "Maximum size of one file:" : "允许回收的单文件最大大小：";
                case "Mode": return en ? "Directory rule mode:" : "目录规则模式：";
                case "DefaultMode": return en ? "Default — protect system-critical locations" : "默认模式——保护系统关键目录";
                case "BlacklistMode": return en ? "Blacklist — block listed folders and their contents" : "黑名单——禁止名单目录及其内容";
                case "WhitelistMode": return en ? "Whitelist — only allow contents inside listed folders" : "白名单——只允许名单目录里面的内容";
                case "RuleHintDefault": return en ? "System-critical locations remain protected in every mode." : "无论选择哪种模式，系统关键目录都会始终受到保护。";
                case "AddFolder": return en ? "Add folder..." : "添加目录…";
                case "RemoveFolder": return en ? "Remove" : "移除";
                case "ChooseFolder": return en ? "Choose a folder for the directory rule" : "选择要加入目录规则的文件夹";
                case "NeedRulePath": return en ? "Add at least one folder for blacklist or whitelist mode." : "黑名单或白名单模式至少需要添加一个目录。";
                case "CurrentRule": return en ? "Current rule:" : "当前规则：";
                case "DefaultSummary": return en ? "protect system-critical locations" : "保护系统关键目录";
                case "BlacklistSummary": return en ? "system protection + blacklist ({0} folders)" : "系统保护 + 黑名单（{0} 个目录）";
                case "WhitelistSummary": return en ? "system protection + whitelist ({0} folders)" : "系统保护 + 白名单（{0} 个目录）";
                case "LimitSummary": return en ? "single file <= {0} MB; total operation <= 2048 MB; Recycle Bin only." : "单文件 ≤ {0} MB；单次总量 ≤ 2048 MB；只能进入回收站。";
                case "EnableVault": return en ? "Create a recovery-vault copy before every recycle operation" : "每次回收前创建恢复保险副本";
                case "VaultExplanation": return en ? "The vault prevents data loss when Windows bypasses Recycle Bin. Disabling it can make undo unavailable." : "当 Windows 绕过系统回收站时，保险库可防止数据丢失。关闭后可能无法撤回。";
                case "VaultLocation": return en ? "Recovery vault location:" : "恢复保险库目录：";
                case "OpenVault": return en ? "Open folder" : "打开目录";
                case "VaultCleanup": return en ? "Cleanup policy" : "清理策略";
                case "VaultManual": return en ? "Keep vault copies for manual cleanup" : "保留保险副本，由用户手工清理";
                case "VaultOnStartup": return en ? "Permanently clear the entire vault every time the app starts" : "每次启动软件时永久清空整个保险库";
                case "VaultStartupWarning": return en ? "Warning: startup cleanup permanently removes all vault copies. Items may then only be recoverable from Windows Recycle Bin." : "注意：启动清理会永久移除所有保险副本，之后只能依赖 Windows 系统回收站恢复。";
                case "VaultClearFailed": return en ? "The recovery vault could not be cleared during startup." : "启动时未能清空恢复保险库。";
                case "Save": return en ? "Save" : "保存";
                case "Cancel": return en ? "Cancel" : "取消";
                case "ConfirmTitle": return en ? "Confirm recycle" : "确认移入回收站";
                case "ConfirmQuestion": return en ? "Move these {0} selected items to Recycle Bin?" : "确定将以下 {0} 个项目移入 Windows 回收站吗？";
                case "ConfirmSafety": return en ? "Only Recycle Bin operations are requested. The configured safety rules have already passed." : "程序只请求移入回收站；当前配置的安全规则已经检查通过。";
                case "ConfirmButton": return en ? "Recycle" : "移入回收站";
                case "ScanSummary": return en ? "Safety scan: {0} files, {1} folders, {2}" : "安全扫描：文件 {0} 个，文件夹 {1} 个，总大小 {2}";
                case "UnsafeTitle": return en ? "Blocked by safety rules" : "无法安全回收";
                case "SettingsSaveFailed": return en ? "Settings could not be saved." : "设置未能保存。";
                case "StartupRegistryFailed": return en ? "Cannot modify the current user's startup setting." : "无法修改当前用户的开机启动设置。";
                case "SettingsRegistryFailed": return en ? "Cannot open the current user's settings registry." : "无法打开当前用户设置注册表。";
                case "StillInTray": return en ? "The app is still running in the tray. Right-click the tray icon to exit." : "程序仍在托盘运行，右键托盘图标可退出。";
                case "RecycleSuccessBalloon": return en ? "{0} items were safely moved to Recycle Bin." : "{0} 个项目已安全移入 Windows 回收站。";
                case "OperationAborted": return en ? "The operation was cancelled or incomplete. Unprocessed items remain at their original locations." : "操作已取消或没有全部完成，未完成的项目仍保留在原位置。";
                case "WindowsFailure": return en ? "Windows could not complete the recycle operation." : "Windows 未能完成回收操作。";
                case "UndoConfirmTitle": return en ? "Undo last recycle" : "撤回上一次回收";
                case "UndoConfirm": return en ? "Restore the items from the most recent recycle operation?" : "确定恢复本程序上一次移入回收站的项目吗？";
                case "UndoNone": return en ? "There is no recorded recycle operation to undo." : "没有可撤回的回收记录。";
                case "UndoSuccess": return en ? "The last recycle operation was restored." : "上一次回收的项目已恢复。";
                case "UndoPartial": return en ? "Restored {0} of {1} items. Some items could not be found or their original paths are occupied." : "已恢复 {0}/{1} 个项目。部分项目未找到，或原路径已被占用。";
                case "UndoFailed": return en ? "The last recycle operation could not be restored." : "无法恢复上一次回收的项目。";
                case "UndoPathOccupied": return en ? "An original path is already occupied:" : "原路径已存在同名项目：";
                default: return key;
            }
        }
    }
}

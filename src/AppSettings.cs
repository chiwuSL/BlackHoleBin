using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BlackHoleBin
{
    internal sealed class AppSettings
    {
        public const int MinimumHoleSize = 120;
        public const int MaximumHoleSize = 420;
        public const int MinimumFileSizeLimitMb = 10;
        public const int MaximumFileSizeLimitMb = 2048;

        public bool StartWithWindows { get; set; }
        public bool HideWhenMaximized { get; set; }
        public bool ConfirmBeforeRecycle { get; set; }
        public bool ResizeMode { get; set; }
        public int HoleSize { get; set; }
        public int MaximumFileSizeMb { get; set; }
        public AppLanguage Language { get; set; }
        public ProtectionMode ProtectionMode { get; set; }
        public List<string> RulePaths { get; set; }
        public bool RecoveryVaultEnabled { get; set; }
        public RecoveryCleanupMode RecoveryCleanupMode { get; set; }
        public bool AnimationsEnabled { get; set; }
        public VisualTheme VisualTheme { get; set; }
        public int EffectIntensity { get; set; }

        public long MaximumFileSizeBytes
        {
            get { return (long)MaximumFileSizeMb * 1024L * 1024L; }
        }

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                StartWithWindows = false,
                HideWhenMaximized = false,
                ConfirmBeforeRecycle = true,
                ResizeMode = false,
                HoleSize = 200,
                MaximumFileSizeMb = MaximumFileSizeLimitMb,
                Language = AppLanguage.Chinese,
                ProtectionMode = ProtectionMode.Default,
                RulePaths = new List<string>(),
                RecoveryVaultEnabled = true,
                RecoveryCleanupMode = RecoveryCleanupMode.Manual,
                AnimationsEnabled = true,
                VisualTheme = VisualTheme.SolarAmber,
                EffectIntensity = 70
            };
        }

        public AppSettings Clone()
        {
            AppSettings clone = (AppSettings)MemberwiseClone();
            clone.RulePaths = new List<string>(RulePaths ?? new List<string>());
            return clone;
        }
    }

    internal static class AppSettingsStore
    {
        internal const string SettingsKey = @"Software\BlackHoleBin";
        private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValueName = "BlackHoleBin";

        public static AppSettings Load()
        {
            AppSettings settings = AppSettings.CreateDefault();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKey, false))
                {
                    if (key != null)
                    {
                        settings.HideWhenMaximized = ReadBoolean(key, "HideWhenMaximized", settings.HideWhenMaximized);
                        settings.ConfirmBeforeRecycle = ReadBoolean(key, "ConfirmBeforeRecycle", settings.ConfirmBeforeRecycle);
                        settings.ResizeMode = ReadBoolean(key, "ResizeMode", settings.ResizeMode);
                        settings.HoleSize = ReadInteger(key, "HoleSize", settings.HoleSize);
                        settings.MaximumFileSizeMb = ReadInteger(key, "MaximumFileSizeMb", settings.MaximumFileSizeMb);
                        settings.Language = (AppLanguage)ReadInteger(key, "Language", (int)settings.Language);
                        settings.ProtectionMode = (ProtectionMode)ReadInteger(key, "ProtectionMode", (int)settings.ProtectionMode);
                        settings.RecoveryVaultEnabled = ReadBoolean(key, "RecoveryVaultEnabled", settings.RecoveryVaultEnabled);
                        settings.RecoveryCleanupMode = (RecoveryCleanupMode)ReadInteger(
                            key,
                            "RecoveryCleanupMode",
                            (int)settings.RecoveryCleanupMode);
                        settings.AnimationsEnabled = ReadBoolean(key, "AnimationsEnabled", settings.AnimationsEnabled);
                        settings.VisualTheme = (VisualTheme)ReadInteger(key, "VisualTheme", (int)settings.VisualTheme);
                        settings.EffectIntensity = ReadInteger(key, "EffectIntensity", settings.EffectIntensity);
                        string[] paths = key.GetValue("RulePaths") as string[];
                        if (paths != null)
                        {
                            settings.RulePaths = paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
                        }
                    }
                }

                settings.StartWithWindows = IsStartupEnabled();
            }
            catch
            {
                // Invalid or inaccessible settings fall back to safe defaults.
            }

            settings.HoleSize = Clamp(settings.HoleSize, AppSettings.MinimumHoleSize, AppSettings.MaximumHoleSize);
            settings.MaximumFileSizeMb = Clamp(
                settings.MaximumFileSizeMb,
                AppSettings.MinimumFileSizeLimitMb,
                AppSettings.MaximumFileSizeLimitMb);
            if (!Enum.IsDefined(typeof(AppLanguage), settings.Language))
            {
                settings.Language = AppLanguage.Chinese;
            }
            if (!Enum.IsDefined(typeof(ProtectionMode), settings.ProtectionMode))
            {
                settings.ProtectionMode = ProtectionMode.Default;
            }
            if (!Enum.IsDefined(typeof(RecoveryCleanupMode), settings.RecoveryCleanupMode))
            {
                settings.RecoveryCleanupMode = RecoveryCleanupMode.Manual;
            }
            if (!Enum.IsDefined(typeof(VisualTheme), settings.VisualTheme))
            {
                settings.VisualTheme = VisualTheme.CosmicBlue;
            }
            settings.EffectIntensity = Clamp(settings.EffectIntensity, 0, 100);
            L.Language = settings.Language;
            return settings;
        }

        public static void Save(AppSettings settings)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey))
            {
                if (key == null)
                {
                    throw new InvalidOperationException(L.Text("SettingsRegistryFailed"));
                }

                key.SetValue("HideWhenMaximized", settings.HideWhenMaximized ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("ConfirmBeforeRecycle", settings.ConfirmBeforeRecycle ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("ResizeMode", settings.ResizeMode ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("HoleSize", settings.HoleSize, RegistryValueKind.DWord);
                key.SetValue("MaximumFileSizeMb", settings.MaximumFileSizeMb, RegistryValueKind.DWord);
                key.SetValue("Language", (int)settings.Language, RegistryValueKind.DWord);
                key.SetValue("ProtectionMode", (int)settings.ProtectionMode, RegistryValueKind.DWord);
                key.SetValue("RecoveryVaultEnabled", settings.RecoveryVaultEnabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("RecoveryCleanupMode", (int)settings.RecoveryCleanupMode, RegistryValueKind.DWord);
                key.SetValue("AnimationsEnabled", settings.AnimationsEnabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("VisualTheme", (int)settings.VisualTheme, RegistryValueKind.DWord);
                key.SetValue("EffectIntensity", settings.EffectIntensity, RegistryValueKind.DWord);
                key.SetValue("RulePaths", (settings.RulePaths ?? new List<string>()).ToArray(), RegistryValueKind.MultiString);
            }

            SetStartupEnabled(settings.StartWithWindows);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool ReadBoolean(RegistryKey key, string name, bool fallback)
        {
            object value = key.GetValue(name);
            return value is int ? (int)value != 0 : fallback;
        }

        private static int ReadInteger(RegistryKey key, string name, int fallback)
        {
            object value = key.GetValue(name);
            return value is int ? (int)value : fallback;
        }

        private static bool IsStartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKey, false))
            {
                return key != null && key.GetValue(StartupValueName) != null;
            }
        }

        private static void SetStartupEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKey, true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException(L.Text("StartupRegistryFailed"));
                }

                if (enabled)
                {
                    string executable = Assembly.GetExecutingAssembly().Location;
                    key.SetValue(StartupValueName, "\"" + executable + "\"", RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(StartupValueName, false);
                }
            }
        }
    }

    internal sealed class SettingsDialog : Form
    {
        private readonly AppSettings current;
        private readonly ComboBox languageCombo;
        private readonly RadioButton alwaysOnTopRadio;
        private readonly RadioButton hideWhenMaximizedRadio;
        private readonly CheckBox startupCheckBox;
        private readonly CheckBox confirmationCheckBox;
        private readonly TrackBar holeSizeTrackBar;
        private readonly Label holeSizeValueLabel;
        private readonly NumericUpDown maximumFileSizeInput;
        private readonly ComboBox protectionModeCombo;
        private readonly ListBox rulePathList;
        private readonly Button addFolderButton;
        private readonly Button removeFolderButton;
        private readonly Label ruleSummaryLabel;
        private readonly CheckBox recoveryVaultCheckBox;
        private readonly RadioButton manualCleanupRadio;
        private readonly RadioButton startupCleanupRadio;
        private readonly CheckBox animationCheckBox;
        private readonly ComboBox visualThemeCombo;
        private readonly TrackBar effectIntensityTrackBar;
        private readonly Label effectIntensityValueLabel;
        private readonly Action<int> sizePreview;

        public AppSettings ResultSettings { get; private set; }

        public SettingsDialog(AppSettings settings)
            : this(settings, null)
        {
        }

        public SettingsDialog(AppSettings settings, Action<int> liveSizePreview)
        {
            current = settings.Clone();
            sizePreview = liveSizePreview;
            Text = L.Text("SettingsTitle") + " 1.0.0";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(640, 640);
            Font = new Font("Microsoft YaHei UI", 9f);

            TabControl tabs = new TabControl();
            tabs.Location = new Point(14, 14);
            tabs.Size = new Size(612, 565);

            TabPage generalPage = new TabPage(L.Text("GeneralTab"));
            TabPage safetyPage = new TabPage(L.Text("SafetyTab"));
            TabPage recoveryPage = new TabPage(L.Text("RecoveryTab"));
            TabPage appearancePage = new TabPage(L.Text("AppearanceTab"));
            tabs.TabPages.Add(generalPage);
            tabs.TabPages.Add(appearancePage);
            tabs.TabPages.Add(safetyPage);
            tabs.TabPages.Add(recoveryPage);

            Label languageLabel = NewLabel(L.Text("Language"), 18, 20, 165, 25);
            languageCombo = new ComboBox();
            languageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            languageCombo.Items.Add("中文");
            languageCombo.Items.Add("English");
            languageCombo.SelectedIndex = (int)current.Language;
            languageCombo.Location = new Point(190, 18);
            languageCombo.Size = new Size(170, 26);

            GroupBox displayGroup = new GroupBox();
            displayGroup.Text = L.Text("Display");
            displayGroup.Location = new Point(16, 60);
            displayGroup.Size = new Size(570, 132);

            alwaysOnTopRadio = NewRadio(L.Text("AlwaysTop"), 18, 27, 520);
            alwaysOnTopRadio.Checked = !current.HideWhenMaximized;
            hideWhenMaximizedRadio = NewRadio(L.Text("HideMaximized"), 18, 58, 520);
            hideWhenMaximizedRadio.Checked = current.HideWhenMaximized;
            Label hideHint = NewLabel(L.Text("HideHint"), 38, 91, 510, 24);
            hideHint.ForeColor = Color.DimGray;
            displayGroup.Controls.Add(alwaysOnTopRadio);
            displayGroup.Controls.Add(hideWhenMaximizedRadio);
            displayGroup.Controls.Add(hideHint);

            GroupBox behaviorGroup = new GroupBox();
            behaviorGroup.Text = L.Text("Behavior");
            behaviorGroup.Location = new Point(16, 202);
            behaviorGroup.Size = new Size(570, 96);

            startupCheckBox = NewCheck(L.Text("Startup"), 18, 27, 520);
            startupCheckBox.Checked = current.StartWithWindows;
            confirmationCheckBox = NewCheck(L.Text("Confirm"), 18, 58, 520);
            confirmationCheckBox.Checked = current.ConfirmBeforeRecycle;
            behaviorGroup.Controls.Add(startupCheckBox);
            behaviorGroup.Controls.Add(confirmationCheckBox);

            GroupBox sizeGroup = new GroupBox();
            sizeGroup.Text = L.Text("HoleSize");
            sizeGroup.Location = new Point(16, 308);
            sizeGroup.Size = new Size(570, 128);

            Label sizeLiveHint = NewLabel(L.Text("SizeLiveHint"), 18, 25, 430, 25);
            sizeLiveHint.ForeColor = Color.DimGray;
            holeSizeValueLabel = NewLabel("", 455, 25, 90, 25);
            holeSizeValueLabel.TextAlign = ContentAlignment.MiddleRight;
            holeSizeTrackBar = new TrackBar();
            holeSizeTrackBar.Minimum = AppSettings.MinimumHoleSize;
            holeSizeTrackBar.Maximum = AppSettings.MaximumHoleSize;
            holeSizeTrackBar.TickFrequency = 50;
            holeSizeTrackBar.Value = current.HoleSize;
            holeSizeTrackBar.Location = new Point(14, 55);
            holeSizeTrackBar.Size = new Size(535, 50);
            holeSizeTrackBar.ValueChanged += delegate
            {
                UpdateHoleSizeLabel();
                if (sizePreview != null)
                {
                    sizePreview(holeSizeTrackBar.Value);
                }
            };
            UpdateHoleSizeLabel();
            sizeGroup.Controls.Add(sizeLiveHint);
            sizeGroup.Controls.Add(holeSizeValueLabel);
            sizeGroup.Controls.Add(holeSizeTrackBar);

            generalPage.Controls.Add(languageLabel);
            generalPage.Controls.Add(languageCombo);
            generalPage.Controls.Add(displayGroup);
            generalPage.Controls.Add(behaviorGroup);
            generalPage.Controls.Add(sizeGroup);

            animationCheckBox = NewCheck(L.Text("Animation"), 18, 24, 550);
            animationCheckBox.Checked = current.AnimationsEnabled;
            Label animationHint = NewLabel(L.Text("AnimationHint"), 38, 56, 530, 48);
            animationHint.ForeColor = Color.DimGray;

            Label themeLabel = NewLabel(L.Text("ColorTheme"), 18, 128, 165, 25);
            visualThemeCombo = new ComboBox();
            visualThemeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            visualThemeCombo.Items.Add(L.Text("CosmicBlue"));
            visualThemeCombo.Items.Add(L.Text("NebulaPurple"));
            visualThemeCombo.Items.Add(L.Text("SolarAmber"));
            visualThemeCombo.SelectedIndex = (int)current.VisualTheme;
            visualThemeCombo.Location = new Point(190, 126);
            visualThemeCombo.Size = new Size(220, 26);

            GroupBox intensityGroup = new GroupBox();
            intensityGroup.Text = L.Text("EffectIntensity");
            intensityGroup.Location = new Point(16, 184);
            intensityGroup.Size = new Size(570, 145);
            Label intensityHint = NewLabel(L.Text("EffectHint"), 18, 26, 430, 28);
            intensityHint.ForeColor = Color.DimGray;
            effectIntensityValueLabel = NewLabel(current.EffectIntensity + "%", 470, 26, 70, 25);
            effectIntensityValueLabel.TextAlign = ContentAlignment.MiddleRight;
            effectIntensityTrackBar = new TrackBar();
            effectIntensityTrackBar.Minimum = 0;
            effectIntensityTrackBar.Maximum = 100;
            effectIntensityTrackBar.TickFrequency = 10;
            effectIntensityTrackBar.Value = current.EffectIntensity;
            effectIntensityTrackBar.Location = new Point(14, 66);
            effectIntensityTrackBar.Size = new Size(535, 55);
            effectIntensityTrackBar.ValueChanged += delegate
            {
                effectIntensityValueLabel.Text = effectIntensityTrackBar.Value + "%";
            };
            intensityGroup.Controls.Add(intensityHint);
            intensityGroup.Controls.Add(effectIntensityValueLabel);
            intensityGroup.Controls.Add(effectIntensityTrackBar);
            appearancePage.Controls.Add(animationCheckBox);
            appearancePage.Controls.Add(animationHint);
            appearancePage.Controls.Add(themeLabel);
            appearancePage.Controls.Add(visualThemeCombo);
            appearancePage.Controls.Add(intensityGroup);

            Label maxFileLabel = NewLabel(L.Text("MaxFile"), 18, 22, 300, 25);
            maximumFileSizeInput = new NumericUpDown();
            maximumFileSizeInput.Minimum = AppSettings.MinimumFileSizeLimitMb;
            maximumFileSizeInput.Maximum = AppSettings.MaximumFileSizeLimitMb;
            maximumFileSizeInput.Increment = 10;
            maximumFileSizeInput.Value = current.MaximumFileSizeMb;
            maximumFileSizeInput.Location = new Point(330, 20);
            maximumFileSizeInput.Size = new Size(120, 26);
            Label mbLabel = NewLabel("MB", 458, 22, 60, 25);

            Label modeLabel = NewLabel(L.Text("Mode"), 18, 67, 170, 25);
            protectionModeCombo = new ComboBox();
            protectionModeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            protectionModeCombo.Items.Add(L.Text("DefaultMode"));
            protectionModeCombo.Items.Add(L.Text("BlacklistMode"));
            protectionModeCombo.Items.Add(L.Text("WhitelistMode"));
            protectionModeCombo.SelectedIndex = (int)current.ProtectionMode;
            protectionModeCombo.Location = new Point(190, 65);
            protectionModeCombo.Size = new Size(390, 26);

            Label ruleHint = NewLabel(L.Text("RuleHintDefault"), 18, 101, 560, 40);
            ruleHint.ForeColor = Color.DimGray;

            rulePathList = new ListBox();
            rulePathList.Location = new Point(18, 145);
            rulePathList.Size = new Size(562, 245);
            foreach (string path in current.RulePaths)
            {
                rulePathList.Items.Add(path);
            }
            rulePathList.SelectedIndexChanged += delegate { UpdateRuleControls(); };

            addFolderButton = new Button();
            addFolderButton.Text = L.Text("AddFolder");
            addFolderButton.Location = new Point(382, 402);
            addFolderButton.Size = new Size(96, 32);
            addFolderButton.Click += HandleAddFolder;

            removeFolderButton = new Button();
            removeFolderButton.Text = L.Text("RemoveFolder");
            removeFolderButton.Location = new Point(484, 402);
            removeFolderButton.Size = new Size(96, 32);
            removeFolderButton.Click += delegate
            {
                if (rulePathList.SelectedIndex >= 0)
                {
                    rulePathList.Items.RemoveAt(rulePathList.SelectedIndex);
                    UpdateRuleControls();
                }
            };

            ruleSummaryLabel = NewLabel("", 18, 450, 562, 70);
            ruleSummaryLabel.ForeColor = Color.FromArgb(145, 70, 0);

            protectionModeCombo.SelectedIndexChanged += delegate { UpdateRuleControls(); };
            maximumFileSizeInput.ValueChanged += delegate { UpdateRuleControls(); };
            languageCombo.SelectedIndexChanged += delegate { UpdateRuleControls(); };

            safetyPage.Controls.Add(maxFileLabel);
            safetyPage.Controls.Add(maximumFileSizeInput);
            safetyPage.Controls.Add(mbLabel);
            safetyPage.Controls.Add(modeLabel);
            safetyPage.Controls.Add(protectionModeCombo);
            safetyPage.Controls.Add(ruleHint);
            safetyPage.Controls.Add(rulePathList);
            safetyPage.Controls.Add(addFolderButton);
            safetyPage.Controls.Add(removeFolderButton);
            safetyPage.Controls.Add(ruleSummaryLabel);

            recoveryVaultCheckBox = NewCheck(L.Text("EnableVault"), 18, 24, 560);
            recoveryVaultCheckBox.Checked = current.RecoveryVaultEnabled;

            Label vaultExplanation = NewLabel(L.Text("VaultExplanation"), 38, 58, 535, 55);
            vaultExplanation.ForeColor = Color.DimGray;

            GroupBox cleanupGroup = new GroupBox();
            cleanupGroup.Text = L.Text("VaultCleanup");
            cleanupGroup.Location = new Point(18, 125);
            cleanupGroup.Size = new Size(562, 125);

            manualCleanupRadio = NewRadio(L.Text("VaultManual"), 18, 28, 520);
            manualCleanupRadio.Checked = current.RecoveryCleanupMode == RecoveryCleanupMode.Manual;
            startupCleanupRadio = NewRadio(L.Text("VaultOnStartup"), 18, 67, 520);
            startupCleanupRadio.Checked = current.RecoveryCleanupMode == RecoveryCleanupMode.OnStartup;
            cleanupGroup.Controls.Add(manualCleanupRadio);
            cleanupGroup.Controls.Add(startupCleanupRadio);

            Label vaultLocationLabel = NewLabel(L.Text("VaultLocation"), 18, 274, 250, 25);
            TextBox vaultLocationText = new TextBox();
            vaultLocationText.ReadOnly = true;
            vaultLocationText.Text = RecoveryVault.RootDirectory;
            vaultLocationText.Location = new Point(18, 303);
            vaultLocationText.Size = new Size(438, 26);

            Button openVaultButton = new Button();
            openVaultButton.Text = L.Text("OpenVault");
            openVaultButton.Location = new Point(464, 301);
            openVaultButton.Size = new Size(116, 31);
            openVaultButton.Click += delegate
            {
                try
                {
                    Directory.CreateDirectory(RecoveryVault.RootDirectory);
                    Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"), "\"" + RecoveryVault.RootDirectory + "\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, L.Text("RecoveryTab"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            Label startupWarning = NewLabel(L.Text("VaultStartupWarning"), 18, 358, 562, 75);
            startupWarning.ForeColor = Color.FromArgb(170, 60, 0);

            recoveryPage.Controls.Add(recoveryVaultCheckBox);
            recoveryPage.Controls.Add(vaultExplanation);
            recoveryPage.Controls.Add(cleanupGroup);
            recoveryPage.Controls.Add(vaultLocationLabel);
            recoveryPage.Controls.Add(vaultLocationText);
            recoveryPage.Controls.Add(openVaultButton);
            recoveryPage.Controls.Add(startupWarning);

            Button cancel = new Button();
            cancel.Text = L.Text("Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(436, 592);
            cancel.Size = new Size(88, 34);

            Button save = new Button();
            save.Text = L.Text("Save");
            save.Location = new Point(532, 592);
            save.Size = new Size(94, 34);
            save.Click += HandleSave;

            Controls.Add(tabs);
            Controls.Add(cancel);
            Controls.Add(save);
            AcceptButton = save;
            CancelButton = cancel;
            UpdateRuleControls();
        }

        private void UpdateHoleSizeLabel()
        {
            holeSizeValueLabel.Text = holeSizeTrackBar.Value + " px";
        }

        private void UpdateRuleControls()
        {
            bool custom = protectionModeCombo.SelectedIndex != (int)ProtectionMode.Default;
            rulePathList.Enabled = custom;
            addFolderButton.Enabled = custom;
            removeFolderButton.Enabled = custom && rulePathList.SelectedIndex >= 0;

            AppLanguage selectedLanguage = (AppLanguage)Math.Max(0, languageCombo.SelectedIndex);
            string modeSummary;
            if (protectionModeCombo.SelectedIndex == (int)ProtectionMode.Blacklist)
            {
                modeSummary = string.Format(L.Get("BlacklistSummary", selectedLanguage), rulePathList.Items.Count);
            }
            else if (protectionModeCombo.SelectedIndex == (int)ProtectionMode.Whitelist)
            {
                modeSummary = string.Format(L.Get("WhitelistSummary", selectedLanguage), rulePathList.Items.Count);
            }
            else
            {
                modeSummary = L.Get("DefaultSummary", selectedLanguage);
            }

            ruleSummaryLabel.Text = L.Get("CurrentRule", selectedLanguage) + " " + modeSummary + "\r\n"
                + string.Format(L.Get("LimitSummary", selectedLanguage), (int)maximumFileSizeInput.Value);
        }

        private void HandleAddFolder(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = L.Text("ChooseFolder");
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) == DialogResult.OK
                    && !rulePathList.Items.Cast<string>().Any(
                        path => string.Equals(path, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase)))
                {
                    rulePathList.Items.Add(Path.GetFullPath(dialog.SelectedPath));
                    UpdateRuleControls();
                }
            }
        }

        private void HandleSave(object sender, EventArgs e)
        {
            ProtectionMode mode = (ProtectionMode)protectionModeCombo.SelectedIndex;
            if (mode != ProtectionMode.Default && rulePathList.Items.Count == 0)
            {
                MessageBox.Show(this, L.Text("NeedRulePath"), L.Text("SettingsTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ResultSettings = new AppSettings
            {
                StartWithWindows = startupCheckBox.Checked,
                HideWhenMaximized = hideWhenMaximizedRadio.Checked,
                ConfirmBeforeRecycle = confirmationCheckBox.Checked,
                ResizeMode = current.ResizeMode,
                HoleSize = holeSizeTrackBar.Value,
                MaximumFileSizeMb = (int)maximumFileSizeInput.Value,
                Language = (AppLanguage)languageCombo.SelectedIndex,
                ProtectionMode = mode,
                RulePaths = rulePathList.Items.Cast<string>().ToList(),
                RecoveryVaultEnabled = recoveryVaultCheckBox.Checked,
                RecoveryCleanupMode = startupCleanupRadio.Checked
                    ? RecoveryCleanupMode.OnStartup
                    : RecoveryCleanupMode.Manual,
                AnimationsEnabled = animationCheckBox.Checked,
                VisualTheme = (VisualTheme)visualThemeCombo.SelectedIndex,
                EffectIntensity = effectIntensityTrackBar.Value
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        private static Label NewLabel(string text, int x, int y, int width, int height)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height) };
        }

        private static RadioButton NewRadio(string text, int x, int y, int width)
        {
            return new RadioButton { Text = text, Location = new Point(x, y), Size = new Size(width, 25) };
        }

        private static CheckBox NewCheck(string text, int x, int y, int width)
        {
            return new CheckBox { Text = text, Location = new Point(x, y), Size = new Size(width, 25) };
        }
    }
}

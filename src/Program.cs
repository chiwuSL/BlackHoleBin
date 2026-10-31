using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BlackHoleBin
{
    internal static class Program
    {
        internal static System.Threading.EventWaitHandle ShowRequest;
        [STAThread]
        private static void Main()
        {
            string identity = "Local\\BlackHoleBin-" + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            bool createdRequest;
            using (var request = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, identity + "-Show", out createdRequest))
            using (var instance = new System.Threading.Mutex(false, identity))
            {
                bool acquired;
                try { acquired = instance.WaitOne(0); }
                catch (System.Threading.AbandonedMutexException) { acquired = true; }
                if (!acquired) {
                    request.Set();
                    if (createdRequest) MessageBox.Show("已有旧版黑洞回收站在运行。请先右键通知区域中的黑洞图标，选择“退出”，再打开此版本。\r\n\r\nAn older version is running. Exit it from the tray before launching this version.", "黑洞回收站 / Black Hole Bin", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                ShowRequest = request;
                try { RunApplication(); }
                finally { instance.ReleaseMutex(); }
            }
        }

        private static void RunApplication()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppSettings settings = AppSettingsStore.Load();
            if (settings.RecoveryCleanupMode == RecoveryCleanupMode.OnStartup)
            {
                try
                {
                    RecoveryVault.ClearAll();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        L.Text("VaultClearFailed") + "\r\n\r\n" + ex.Message,
                        L.Text("AppName"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            Application.Run(new BlackHoleForm(settings));
        }
    }

    internal sealed class BlackHoleForm : Form
    {
        private const int WmNcLButtonDown = 0x00A1;
        private const int HtCaption = 2;

        private readonly NotifyIcon trayIcon;
        private readonly Timer statusTimer;
        private readonly Timer foregroundTimer;
        private readonly Timer animationTimer;
        private readonly ContextMenuStrip mainMenu;
        private readonly ToolStripMenuItem visibilityMenuItem;
        private readonly ToolStripMenuItem resizeModeMenuItem;
        private readonly ToolStripMenuItem settingsMenuItem;
        private readonly ToolStripMenuItem undoMenuItem;
        private readonly ToolStripMenuItem exitMenuItem;
        private AppSettings settings;
        private bool dragActive;
        private bool allowExit;
        private bool userHidden;
        private bool autoHidden;
        private bool resizeDragging;
        private bool moveDragging;
        private Point moveGrabOffset;
        private bool menuInteractionActive;
        private int interactionGuardDepth;
        private Point resizeCenterScreen;
        private string statusText;
        private float animationPhase;
        private float successPulse;
        private bool mouseHover;
        private Bitmap desktopSnapshot;
        private Bitmap desktopCache;
        private Rectangle desktopCacheBounds;
        private Bitmap distortedDesktop;
        private GpuRenderer gpu;
        private readonly System.Diagnostics.Stopwatch renderClock = System.Diagnostics.Stopwatch.StartNew();
        private long lastCapture;
        private bool captureExcluded;
        private bool captureErrorReported;
        private bool captureInProgress;
        private bool updatingBackground;
        private bool runtimeDisposed;
        private bool operationBusy;
        private bool manualShowOverride;
        private bool autoHideNoticeShown;
        private DateTime startupVisibleUntil = DateTime.UtcNow.AddSeconds(8);
        [DllImport("user32.dll", SetLastError=true)] private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

        public BlackHoleForm()
            : this(AppSettingsStore.Load())
        {
        }

        public BlackHoleForm(AppSettings initialSettings)
        {
            settings = initialSettings ?? AppSettings.CreateDefault();
            L.Language = settings.Language;
            statusText = DefaultStatusText;

            Text = L.Text("AppName");
            AccessibleName = L.Text("AppName");
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(settings.HoleSize, settings.HoleSize);
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            AllowDrop = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(15, 15, 18);
            Opacity = 0.999d;

            Region = new Region(new GraphicsPath());
            ApplyRoundRegion();

            mainMenu = new ContextMenuStrip();

            visibilityMenuItem = new ToolStripMenuItem(L.Text("Hide"));
            visibilityMenuItem.Click += delegate { ToggleVisible(); };
            mainMenu.Items.Add(visibilityMenuItem);

            settingsMenuItem = new ToolStripMenuItem(L.Text("Settings"));
            settingsMenuItem.Click += delegate { ShowSettingsDialog(); };
            mainMenu.Items.Add(settingsMenuItem);

            resizeModeMenuItem = new ToolStripMenuItem(L.Text("ResizeMode"));
            resizeModeMenuItem.CheckOnClick = true;
            resizeModeMenuItem.Checked = settings.ResizeMode;
            resizeModeMenuItem.CheckedChanged += delegate
            {
                settings.ResizeMode = resizeModeMenuItem.Checked;
                statusText = DefaultStatusText;
                TrySaveSettings();
                Invalidate();
            };
            mainMenu.Items.Add(resizeModeMenuItem);

            undoMenuItem = new ToolStripMenuItem(L.Text("Undo"));
            undoMenuItem.Click += delegate { UndoLastRecycle(); };
            mainMenu.Items.Add(undoMenuItem);
            mainMenu.Items.Add(new ToolStripSeparator());

            exitMenuItem = new ToolStripMenuItem(L.Text("Exit"));
            exitMenuItem.Click += delegate
            {
                allowExit = true;
                Close();
            };
            mainMenu.Items.Add(exitMenuItem);
            mainMenu.Opening += delegate(object sender, System.ComponentModel.CancelEventArgs args)
            {
                if (operationBusy) { args.Cancel=true; return; }
                if (!menuInteractionActive)
                {
                    menuInteractionActive = true;
                    interactionGuardDepth++;
                }
                visibilityMenuItem.Text = Visible ? L.Text("Hide") : L.Text("Show");
                resizeModeMenuItem.Checked = settings.ResizeMode;
                undoMenuItem.Enabled = UndoRecordStore.HasRecord();
            };
            mainMenu.Closed += delegate
            {
                if (menuInteractionActive)
                {
                    menuInteractionActive = false;
                    interactionGuardDepth = Math.Max(0, interactionGuardDepth - 1);
                }
            };
            ContextMenuStrip = mainMenu;

            trayIcon = new NotifyIcon();
            trayIcon.Text = L.Text("AppName");
            trayIcon.Icon = CreateBlackHoleIcon();
            trayIcon.Visible = true;
            trayIcon.ContextMenuStrip = mainMenu;
            trayIcon.DoubleClick += delegate { ToggleVisible(); };

            statusTimer = new Timer();
            statusTimer.Interval = 2600;
            statusTimer.Tick += delegate
            {
                statusTimer.Stop();
                statusText = DefaultStatusText;
                Invalidate();
            };

            foregroundTimer = new Timer();
            foregroundTimer.Interval = 350;
            foregroundTimer.Tick += HandleForegroundTimer;

            animationTimer = new Timer();
            animationTimer.Interval = 16;
            animationTimer.Tick += delegate
            {
                if (!Visible || WindowState == FormWindowState.Minimized) return;
                if (gpu != null && captureExcluded && renderClock.ElapsedMilliseconds-lastCapture>=66)
                    CaptureDesktopBackground();
                animationPhase = (animationPhase + (dragActive ? 5.0f : 2.0f)) % 360f;
                if (successPulse > 0f)
                {
                    successPulse = Math.Max(0f, successPulse - 0.045f);
                }
                Invalidate();
            };
            ApplyAnimationSettings();

            DragEnter += HandleDragEnter;
            DragOver += HandleDragOver;
            DragLeave += HandleDragLeave;
            DragDrop += HandleDragDrop;
            MouseDown += HandleMouseDown;
            MouseMove += HandleMouseMove;
            MouseUp += HandleMouseUp;
            MouseCaptureChanged += delegate {
                if (!Capture) { moveDragging=false; resizeDragging=false; }
            };
            LocationChanged += delegate {
                if (!IsHandleCreated) return;
                UpdateBackgroundFromCache();
                Invalidate();
            };
            MouseEnter += delegate { mouseHover = true; Invalidate(); };
            MouseLeave += delegate { mouseHover = false; Invalidate(); };
            SizeChanged += delegate
            {
                if (ClientSize.Width > 0 && ClientSize.Height > 0)
                {
                    ApplyRoundRegion();
                    Invalidate();
                }
            };
            Shown += delegate
            {
                startupVisibleUntil = DateTime.UtcNow.AddSeconds(8);
                MoveToDefaultPosition();
                try { gpu = new GpuRenderer(); } catch (Exception ex) {
                    MessageBox.Show(this, "GPU: " + ex.Message, L.Text("AppName"));
                }
                Opacity = 0.999d;
                // Keep the overlay visible to remote desktop and recording tools.
                captureExcluded = SetWindowDisplayAffinity(Handle, 0);
                CaptureDesktopBackground();
                Opacity = 0.999d;
                foregroundTimer.Start();
            };
            FormClosing += HandleFormClosing;
        }

        private string DefaultStatusText
        {
            get { return settings != null && settings.ResizeMode ? L.Text("ResizeHint") : L.Text("DropHere"); }
        }

        private void ApplyRoundRegion()
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(ClientRectangle);
                Region oldRegion = Region;
                Region = new Region(path);
                if (oldRegion != null)
                {
                    oldRegion.Dispose();
                }
            }
        }

        private void ShowSettingsDialog()
        {
            int originalSize = settings.HoleSize;
            interactionGuardDepth++;
            try
            {
                using (SettingsDialog dialog = new SettingsDialog(
                    settings,
                    size => ResizeBlackHole(size, false)))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        ResizeBlackHole(originalSize, false);
                        settings.HoleSize = originalSize;
                        return;
                    }

                    AppSettings proposed = dialog.ResultSettings;
                    AppLanguage previousLanguage = L.Language;
                    try
                    {
                        L.Language = proposed.Language;
                        AppSettingsStore.Save(proposed);
                        settings = proposed;
                        manualShowOverride = false;
                        ApplyAnimationSettings();
                        resizeModeMenuItem.Checked = settings.ResizeMode;
                        ResizeBlackHole(settings.HoleSize, false);
                        RefreshLocalizedUi();
                        Invalidate();
                    }
                    catch (Exception ex)
                    {
                        L.Language = previousLanguage;
                        ResizeBlackHole(originalSize, false);
                        settings.HoleSize = originalSize;
                        MessageBox.Show(
                            this,
                            L.Text("SettingsSaveFailed") + "\r\n\r\n" + ex.Message,
                            L.Text("SettingsTitle"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
            finally
            {
                interactionGuardDepth = Math.Max(0, interactionGuardDepth - 1);
                ApplyDisplayPolicy();
            }
        }

        private void RefreshLocalizedUi()
        {
            Text = L.Text("AppName");
            AccessibleName = L.Text("AppName");
            trayIcon.Text = L.Text("AppName");
            visibilityMenuItem.Text = Visible ? L.Text("Hide") : L.Text("Show");
            settingsMenuItem.Text = L.Text("Settings");
            resizeModeMenuItem.Text = L.Text("ResizeMode");
            undoMenuItem.Text = L.Text("Undo");
            exitMenuItem.Text = L.Text("Exit");
            statusText = DefaultStatusText;
        }

        private async void UndoLastRecycle()
        {
            if (operationBusy) return;
            operationBusy=true; interactionGuardDepth++;
            try { await UndoLastRecycleCore(); }
            catch(Exception ex) { MessageBox.Show(this, ex.Message, L.Text("UndoFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { operationBusy=false; interactionGuardDepth=Math.Max(0,interactionGuardDepth-1); ApplyDisplayPolicy(); }
        }

        private async System.Threading.Tasks.Task UndoLastRecycleCore()
        {
            if (!UndoRecordStore.HasRecord())
            {
                MessageBox.Show(this, L.Text("UndoNone"), L.Text("UndoConfirmTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(
                this,
                L.Text("UndoConfirm"),
                L.Text("UndoConfirmTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            UseWaitCursor = true;
            try
            {
                UndoResult result = await OperationWorker.Run(() => UndoService.RestoreLast());
                string message = result.Success
                    ? L.Text("UndoSuccess")
                    : (result.TotalCount == 0
                        ? L.Text("UndoNone")
                        : string.Format(L.Text("UndoPartial"), result.RestoredCount, result.TotalCount));
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    message += "\r\n\r\n" + result.ErrorMessage;
                }
                MessageBox.Show(
                    this,
                    message,
                    L.Text("UndoConfirmTitle"),
                    MessageBoxButtons.OK,
                    result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void MoveToDefaultPosition()
        {
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(area.Right - Width - 28, area.Bottom - Height - 28);
        }

        internal void RestoreVisible()
        {
            manualShowOverride = true;
            userHidden = false;
            autoHidden = false;
            WindowState = FormWindowState.Normal;
            Opacity = 0.999d;
            MoveToDefaultPosition();
            Show();
            TopMost = true;
            BringToFront();
            CaptureDesktopBackground();
            Invalidate();
        }

        private void ToggleVisible()
        {
            if (Visible)
            {
                userHidden = true;
                manualShowOverride = false;
                autoHidden = false;
                Hide();
            }
            else
            {
                RestoreVisible();
            }
        }

        private void HandleForegroundTimer(object sender, EventArgs e)
        {
            if (Program.ShowRequest != null && Program.ShowRequest.WaitOne(0)) RestoreVisible();
            ApplyDisplayPolicy();
        }

        private void ApplyDisplayPolicy()
        {
            if (!TopMost)
            {
                TopMost = true;
            }
            if (interactionGuardDepth > 0 || moveDragging || resizeDragging || manualShowOverride || DateTime.UtcNow < startupVisibleUntil)
            {
                return;
            }
            bool shouldHide = settings.HideWhenMaximized
                && WindowStateDetector.IsForegroundMaximizedOrFullscreen(Handle);

            if (shouldHide)
            {
                if (Visible && !userHidden)
                {
                    autoHidden = true;
                    Hide();
                    if (!autoHideNoticeShown) {
                        autoHideNoticeShown=true;
                        trayIcon.ShowBalloonTip(2500,L.Text("AppName"),L.Language==AppLanguage.Chinese ? "已按“最大化时隐藏”设置隐藏黑洞。右键托盘图标选择“显示黑洞”可找回。" : "Hidden by the maximized-app setting. Use Show black hole in the tray menu to restore it.",ToolTipIcon.Info);
                    }
                }
            }
            else if (autoHidden)
            {
                autoHidden = false;
                if (!userHidden)
                {
                    Show();
                    TopMost = true;
                }
            }
        }

        private void HandleFormClosing(object sender, FormClosingEventArgs e)
        {
            if (operationBusy) { e.Cancel=true; return; }
            if (!allowExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                userHidden = true;
                autoHidden = false;
                Hide();
                trayIcon.ShowBalloonTip(1200, L.Text("AppName"), L.Text("StillInTray"), ToolTipIcon.Info);
                return;
            }

            DisposeRuntime();
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing)DisposeRuntime();
            base.Dispose(disposing);
        }

        private void DisposeRuntime()
        {
            if(runtimeDisposed)return;
            runtimeDisposed=true;
            foregroundTimer.Stop();
            statusTimer.Stop();
            animationTimer.Stop();
            animationTimer.Dispose();
            if (gpu != null) gpu.Dispose();
            if (desktopSnapshot != null) desktopSnapshot.Dispose();
            if (desktopCache != null) desktopCache.Dispose();
            if (distortedDesktop != null) distortedDesktop.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            mainMenu.Dispose();
        }

        private void HandleMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (settings.ResizeMode && IsResizeEdge(e.Location))
                {
                    resizeDragging = true;
                    Capture = true;
                    resizeCenterScreen = PointToScreen(new Point(ClientSize.Width / 2, ClientSize.Height / 2));
                    statusText = ClientSize.Width + " px";
                    Invalidate();
                }
                else
                {
                    moveDragging = true;
                    moveGrabOffset = e.Location;
                    Capture = true;
                    CaptureDesktopBackground();
                }
            }
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            if (moveDragging)
            {
                if ((e.Button & MouseButtons.Left)==0) return;
                Point screen=PointToScreen(e.Location);
                Location=new Point(screen.X-moveGrabOffset.X,screen.Y-moveGrabOffset.Y);
                if (renderClock.ElapsedMilliseconds-lastCapture>=40) CaptureDesktopBackground();
                return;
            }
            if (!resizeDragging)
            {
                return;
            }

            Point current = PointToScreen(e.Location);
            double dx = current.X - resizeCenterScreen.X;
            double dy = current.Y - resizeCenterScreen.Y;
            int size = (int)Math.Round(Math.Sqrt((dx * dx) + (dy * dy)) * 2.0);
            size = Math.Max(AppSettings.MinimumHoleSize, Math.Min(AppSettings.MaximumHoleSize, size));
            ResizeBlackHole(size, true);
            statusText = size + " px";
            Invalidate();
        }

        private void HandleMouseUp(object sender, MouseEventArgs e)
        {
            if (moveDragging && e.Button==MouseButtons.Left)
            {
                moveDragging=false;
                Capture=false;
                CaptureDesktopBackground();
                Invalidate();
                return;
            }
            if (!resizeDragging)
            {
                return;
            }

            resizeDragging = false;
            Capture = false;
            settings.HoleSize = ClientSize.Width;
            CaptureDesktopBackground();
            TrySaveSettings();
            ShowStatus(settings.HoleSize + " px");
        }

        private bool IsResizeEdge(Point point)
        {
            double center = ClientSize.Width / 2.0;
            double dx = point.X - center;
            double dy = point.Y - center;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));
            return distance >= center * 0.68;
        }

        private void ResizeBlackHole(int size, bool keepCenter)
        {
            size = Math.Max(AppSettings.MinimumHoleSize, Math.Min(AppSettings.MaximumHoleSize, size));
            Point center = keepCenter
                ? resizeCenterScreen
                : PointToScreen(new Point(ClientSize.Width / 2, ClientSize.Height / 2));
            ClientSize = new Size(size, size);
            Location = new Point(center.X - Width / 2, center.Y - Height / 2);
            settings.HoleSize = size;
            if (!resizeDragging && Visible && Opacity > 0d)
            {
                CaptureDesktopBackground();
            }
        }

        private void TrySaveSettings()
        {
            try
            {
                AppSettingsStore.Save(settings);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    L.Text("SettingsSaveFailed") + "\r\n\r\n" + ex.Message,
                    L.Text("SettingsTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void HandleDragEnter(object sender, DragEventArgs e)
        {
            if (operationBusy) { e.Effect=DragDropEffects.None; return; }
            if (HasFileDrop(e.Data))
            {
                dragActive = true;
                e.Effect = DragDropEffects.Move;
                statusText = L.Text("DropRelease");
                Invalidate();
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void HandleDragOver(object sender, DragEventArgs e)
        {
            e.Effect = !operationBusy && HasFileDrop(e.Data) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void HandleDragLeave(object sender, EventArgs e)
        {
            dragActive = false;
            statusText = DefaultStatusText;
            Invalidate();
        }

        private async void HandleDragDrop(object sender, DragEventArgs e)
        {
            if (operationBusy) return;
            operationBusy = true;
            interactionGuardDepth++;
            AppSettings operationSettings = settings.Clone();
            try { await ProcessDrop(e, operationSettings); }
            catch (Exception ex) {
                MessageBox.Show(this, ex.Message, L.Text("RecycleFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowStatus(L.Text("RecycleFailed"));
            }
            finally {
                UseWaitCursor=false;
                operationBusy=false;
                interactionGuardDepth=Math.Max(0,interactionGuardDepth-1);
                ApplyDisplayPolicy();
            }
        }

        private async System.Threading.Tasks.Task ProcessDrop(DragEventArgs e, AppSettings operationSettings)
        {
            dragActive = false;
            statusText = L.Text("Checking");
            Invalidate();

            string[] rawPaths = e.Data.GetData(DataFormats.FileDrop) as string[];
            List<string> paths = NormalizePaths(rawPaths);
            if (paths.Count == 0)
            {
                ShowStatus(L.Text("NoItems"));
                return;
            }

            UseWaitCursor = true;
            SafetyReport safety;
            try
            {
                safety = await OperationWorker.Run(() => RecycleService.InspectPaths(paths, operationSettings));
            }
            finally
            {
                UseWaitCursor = false;
            }

            if (!safety.IsSafe)
            {
                MessageBox.Show(
                    this,
                    safety.ErrorMessage,
                    L.Text("UnsafeTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                ShowStatus(L.Text("NoAction"));
                return;
            }

            if (operationSettings.ConfirmBeforeRecycle)
            {
                using (ConfirmRecycleDialog dialog = new ConfirmRecycleDialog(paths, safety))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        ShowStatus(L.Text("Cancelled"));
                        return;
                    }
                }
            }

            UseWaitCursor = true;
            try
            {
                IntPtr owner = Handle;
                RecycleResult result = await OperationWorker.Run(() => RecycleService.MoveToRecycleBin(paths, owner, operationSettings));
                if (result.Success)
                {
                    successPulse = 1f;
                    if (settings.AnimationsEnabled && !animationTimer.Enabled)
                    {
                        animationTimer.Start();
                    }
                    ShowStatus(L.Text("Recycled"));
                    trayIcon.ShowBalloonTip(
                        1400,
                        L.Text("Recycled"),
                        string.Format(L.Text("RecycleSuccessBalloon"), result.ProcessedCount),
                        ToolTipIcon.Info);
                }
                else
                {
                    string message = result.Aborted
                        ? L.Text("OperationAborted")
                        : result.ErrorMessage;
                    MessageBox.Show(
                        this,
                        message,
                        L.Text("RecycleIncomplete"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    ShowStatus(L.Text("RecycleIncomplete"));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    L.Text("WindowsFailure") + "\r\n\r\n" + ex.Message,
                    L.Text("RecycleFailed"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                ShowStatus(L.Text("RecycleFailed"));
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private static bool HasFileDrop(IDataObject data)
        {
            return data != null && data.GetDataPresent(DataFormats.FileDrop);
        }

        private static List<string> NormalizePaths(string[] rawPaths)
        {
            List<string> result = new List<string>();
            if (rawPaths == null)
            {
                return result;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawPath in rawPaths)
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    continue;
                }

                try
                {
                    string fullPath = Path.GetFullPath(rawPath);
                    if ((File.Exists(fullPath) || Directory.Exists(fullPath)) && seen.Add(fullPath))
                    {
                        result.Add(fullPath);
                    }
                }
                catch
                {
                    // Invalid paths are ignored before the confirmation dialog.
                }
            }

            return result
                .Where(path => !result.Any(parent =>
                    !string.Equals(path, parent, StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(parent)
                    && IsPathInside(path, parent)))
                .ToList();
        }

        private static bool IsPathInside(string path, string parent)
        {
            string normalizedParent = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return path.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        private void ShowStatus(string text)
        {
            statusText = text;
            statusTimer.Stop();
            statusTimer.Start();
            Invalidate();
        }

        private void ApplyAnimationSettings()
        {
            if (settings.AnimationsEnabled)
            {
                animationTimer.Start();
            }
            else
            {
                // Keep the desktop live even when the plasma animation is paused.
                animationTimer.Start();
                animationPhase = 24f;
                successPulse = 0f;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (gpu != null && desktopSnapshot != null && desktopSnapshot.Width == ClientSize.Width)
            {
                try {
                    float t=settings.AnimationsEnabled ? (float)renderClock.Elapsed.TotalSeconds : 0f;
                    e.Graphics.DrawImageUnscaled(gpu.Render(desktopSnapshot,t,settings.EffectIntensity/100f,dragActive?1f:successPulse*.7f,(int)settings.VisualTheme),0,0);
                    if (dragActive || settings.ResizeMode || (statusTimer.Enabled && statusText != L.Text("DropHere")))
                        TextRenderer.DrawText(e.Graphics,statusText,Font,new Rectangle(8,Height-40,Width-16,30),Color.White,Color.FromArgb(30,30,30),TextFormatFlags.HorizontalCenter);
                    return;
                } catch { gpu.Dispose(); gpu=null; }
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            float scale = Math.Min(ClientSize.Width, ClientSize.Height) / 200f;
            float intensity = Math.Max(0f, Math.Min(1f, settings.EffectIntensity / 100f));
            Color accent = GetAccentColor(settings.VisualTheme);
            Color accentBright = GetBrightAccentColor(settings.VisualTheme);
            float activity = dragActive ? 1f : (mouseHover ? 0.55f : 0f);
            float breathing = settings.AnimationsEnabled
                ? (float)((Math.Sin(animationPhase * Math.PI / 90.0) + 1.0) * 0.5)
                : 0.45f;
            if (distortedDesktop != null)
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.DrawImageUnscaled(distortedDesktop, 0, 0);
            }
            else
            {
                using (SolidBrush space = new SolidBrush(Color.FromArgb(5, 7, 11)))
                {
                    e.Graphics.FillEllipse(space, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                }
            }
            DrawLensedHalo(e.Graphics, scale, intensity, breathing, activity, accent, accentBright);
            DrawAccretionDisk(e.Graphics, scale, intensity, accent, accentBright, false);


            if (successPulse > 0f)
            {
                float pulseSize = ClientSize.Width * (0.48f + (1f - successPulse) * 0.40f);
                float pulseX = (ClientSize.Width - pulseSize) / 2f;
                using (Pen pulse = new Pen(Color.FromArgb((int)(190 * successPulse), accentBright), Math.Max(2f, 5f * scale * successPulse)))
                {
                    e.Graphics.DrawEllipse(pulse, pulseX, pulseX, pulseSize, pulseSize);
                }
            }

            {
                float centerSize = ClientSize.Width * 0.38f;
                float centerPosition = (ClientSize.Width - centerSize) / 2f;
                using (SolidBrush center = new SolidBrush(Color.Black))
                using (Pen photonGlow = new Pen(Color.FromArgb((int)(105 * intensity), accentBright), Math.Max(2f, 7f * scale)))
                using (Pen photonRing = new Pen(Color.FromArgb((int)(245 * intensity), Color.FromArgb(255, 244, 204)), Math.Max(1f, 1.55f * scale)))
                {
                    e.Graphics.FillEllipse(center, centerPosition, centerPosition, centerSize, centerSize);
                    e.Graphics.DrawEllipse(photonGlow, centerPosition - 1f, centerPosition - 1f, centerSize + 2f, centerSize + 2f);
                    e.Graphics.DrawEllipse(photonRing, centerPosition, centerPosition, centerSize, centerSize);
                }
                DrawAccretionDisk(e.Graphics, scale, intensity, accent, accentBright, true);
            }

            if (settings.ResizeMode || dragActive)
            {
                int alpha = dragActive ? 205 : 125;
                using (Pen edge = new Pen(Color.FromArgb(alpha, accentBright), Math.Max(1.5f, 2.2f * scale)))
                {
                    float inset = 3f * scale;
                    e.Graphics.DrawEllipse(edge, inset, inset, ClientSize.Width - inset * 2f - 1f, ClientSize.Height - inset * 2f - 1f);
                }
            }

            Rectangle textArea = new Rectangle(
                (int)Math.Round(ClientSize.Width * 0.11),
                (int)Math.Round(ClientSize.Height * 0.72),
                (int)Math.Round(ClientSize.Width * 0.78),
                (int)Math.Round(ClientSize.Height * 0.17));
            float fontSize = Math.Max(7f, Math.Min(14f, 8.5f * scale));
            if (dragActive || settings.ResizeMode || (statusTimer.Enabled && statusText != L.Text("DropHere")))
            using (Font statusFont = new Font("Microsoft YaHei UI", fontSize, FontStyle.Regular))
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    statusText,
                    statusFont,
                    textArea,
                    dragActive ? Color.White : Color.FromArgb(220, 225, 230),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }
        }

        private async void CaptureDesktopBackground()
        {
            if (captureInProgress) return;
            captureInProgress=true;
            try
            {
                if (!Visible || ClientSize.Width <= 0 || ClientSize.Height <= 0)
                {
                    return;
                }
                double previousOpacity = Opacity;
                if (!captureExcluded && previousOpacity > 0d) return;
                IntPtr overlay=Handle;
                Rectangle area=new Rectangle(PointToScreen(Point.Empty),ClientSize);
                int margin=Math.Max(128,ClientSize.Width/2);
                area.Inflate(margin,margin);
                Bitmap snapshot=await System.Threading.Tasks.Task.Run(()=>BehindWindowCapture.Capture(overlay,area));
                if (runtimeDisposed || IsDisposed || Disposing) {snapshot.Dispose();return;}
                Bitmap oldCache=desktopCache;
                desktopCache=snapshot;
                desktopCacheBounds=area;
                if(oldCache!=null)oldCache.Dispose();
                lastCapture = renderClock.ElapsedMilliseconds;
                UpdateBackgroundFromCache();
                Opacity = previousOpacity <= 0d ? 1d : previousOpacity;
                Invalidate();
            }
            catch (Exception ex)
            {
                if (runtimeDisposed || IsDisposed || Disposing) return;
                if (!captureErrorReported) {
                    captureErrorReported = true;
                    Console.Error.WriteLine("Desktop capture: " + ex.Message);
                    ShowStatus(L.Language == AppLanguage.Chinese ? "桌面取样暂不可用" : "Desktop capture unavailable");
                }
                if (desktopSnapshot == null) {
                    desktopSnapshot = new Bitmap(ClientSize.Width,ClientSize.Height);
                    using (Graphics g=Graphics.FromImage(desktopSnapshot)) g.Clear(Color.FromArgb(24,26,30));
                }
                Opacity = 0.999d;
            }
            finally {captureInProgress=false;}
        }

        private void UpdateBackgroundFromCache()
        {
            if (updatingBackground || runtimeDisposed || desktopCache==null || IsDisposed || Disposing || !IsHandleCreated) return;
            updatingBackground=true;
            try {
            Rectangle viewport=new Rectangle(PointToScreen(Point.Empty),ClientSize);
            if (!desktopCacheBounds.Contains(viewport)) return;
            Rectangle crop=viewport;
            crop.Offset(-desktopCacheBounds.Left,-desktopCacheBounds.Top);
            // A deep copy keeps the crop independent of the cache's GDI+ storage.
            Bitmap next=new Bitmap(crop.Width,crop.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using(Graphics copy=Graphics.FromImage(next))
                copy.DrawImage(desktopCache,new Rectangle(Point.Empty,crop.Size),crop,GraphicsUnit.Pixel);
            Bitmap old=desktopSnapshot;
            desktopSnapshot=next;
            if(old!=null)old.Dispose();
            if(gpu==null) {
                Bitmap oldDistorted=distortedDesktop;
                distortedDesktop=BuildDistortedDesktop(next);
                if(oldDistorted!=null)oldDistorted.Dispose();
            }
            } finally {updatingBackground=false;}
        }

        private Bitmap BuildDistortedDesktop(Bitmap source)
        {
            Bitmap result = new Bitmap(source.Width, source.Height);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImageUnscaled(source, 0, 0);
                float cx = source.Width / 2f;
                float cy = source.Height / 2f;
                for (int band = 0; band < 12; band++)
                {
                    float outerRadius = source.Width * (0.46f - band * 0.016f);
                    float innerRadius = outerRadius - source.Width * 0.020f;
                    using (GraphicsPath outer = new GraphicsPath())
                    using (GraphicsPath inner = new GraphicsPath())
                    {
                        outer.AddEllipse(cx - outerRadius, cy - outerRadius, outerRadius * 2f, outerRadius * 2f);
                        inner.AddEllipse(cx - innerRadius, cy - innerRadius, innerRadius * 2f, innerRadius * 2f);
                        using (Region ring = new Region(outer))
                        {
                            ring.Exclude(inner);
                            GraphicsState state = graphics.Save();
                            graphics.SetClip(ring, CombineMode.Replace);
                            float magnification = 1.02f + (11 - band) * 0.012f;
                            float width = source.Width * magnification;
                            float height = source.Height * magnification;
                            graphics.TranslateTransform(cx, cy);
                            graphics.RotateTransform((band - 5.5f) * 0.7f);
                            graphics.TranslateTransform(-cx, -cy);
                            graphics.DrawImage(source, cx - width / 2f, cy - height / 2f, width, height);
                            graphics.Restore(state);
                        }
                    }
                }
            }
            return result;
        }

        private void DrawLensedHalo(Graphics graphics, float scale, float intensity, float breathing, float activity, Color accent, Color bright)
        {
            RectangleF aura = new RectangleF(
                ClientSize.Width * 0.205f,
                ClientSize.Height * 0.155f,
                ClientSize.Width * 0.59f,
                ClientSize.Height * 0.69f);
            using (Pen diffuse = new Pen(Color.FromArgb((int)((38 + breathing * 18 + activity * 25) * intensity), bright), Math.Max(4f, 13f * scale)))
            {
                graphics.DrawArc(diffuse, aura, 184f, 172f);
                graphics.DrawArc(diffuse, aura, 4f, 172f);
            }
            for (int i = 0; i < 9; i++)
            {
                float insetX = ClientSize.Width * (0.205f + i * 0.007f);
                float insetY = ClientSize.Height * (0.155f + i * 0.008f);
                RectangleF lens = new RectangleF(insetX, insetY, ClientSize.Width - insetX * 2f, ClientSize.Height - insetY * 2f);
                int alpha = (int)((22 + (8 - i) * 9 + breathing * 16 + activity * 30) * intensity);
                Color strand = i % 3 == 0 ? bright : (i % 3 == 1 ? Color.FromArgb(255, 226, 185) : accent);
                using (Pen pen = new Pen(Color.FromArgb(Math.Min(190, alpha), strand), Math.Max(0.7f, (1.1f + (i % 3) * 0.65f) * scale)))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    graphics.DrawArc(pen, lens, 178f + i * 1.8f, 184f - i * 3.6f);
                    graphics.DrawArc(pen, lens, -2f + i * 1.8f, 184f - i * 3.6f);
                }
            }
        }

        private void DrawAccretionDisk(Graphics graphics, float scale, float intensity, Color accent, Color bright, bool frontSide)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(ClientSize.Width / 2f, ClientSize.Height / 2f);
                graphics.RotateTransform(-9f);
                float glowWidth = ClientSize.Width * 0.91f;
                float glowHeight = ClientSize.Height * 0.20f;
                RectangleF glowOrbit = new RectangleF(-glowWidth / 2f, -glowHeight / 2f + (frontSide ? 6f : -2f) * scale, glowWidth, glowHeight);
                using (Pen diskGlow = new Pen(
                    Color.FromArgb((int)((frontSide ? 76 : 42) * intensity), frontSide ? Color.FromArgb(255, 239, 205) : accent),
                    Math.Max(5f, (frontSide ? 12f : 8f) * scale)))
                {
                    diskGlow.StartCap = diskGlow.EndCap = LineCap.Round;
                    graphics.DrawArc(diskGlow, glowOrbit, frontSide ? 4f : 184f, 172f);
                }
                int strands = frontSide ? 9 : 18;
                for (int i = 0; i < strands; i++)
                {
                    float width = ClientSize.Width * (0.88f - i * 0.016f);
                    float height = ClientSize.Height * (0.20f - i * 0.0048f);
                    float yOffset = frontSide ? (2f + i * 0.65f) * scale : (-3f + i * 0.32f) * scale;
                    RectangleF orbit = new RectangleF(-width / 2f, -height / 2f + yOffset, width, height);
                    Color strand = i % 5 == 0 ? bright : (i % 3 == 0 ? Color.FromArgb(255, 198, 130) : accent);
                    int baseAlpha = frontSide ? 165 : 82;
                    int alpha = Math.Min(245, (int)((baseAlpha + (i % 4) * 18) * intensity));
                    float thickness = Math.Max(0.65f, (frontSide ? 1.25f + (i % 3) * 0.8f : 0.8f + (i % 4) * 0.45f) * scale);
                    using (Pen line = new Pen(Color.FromArgb(alpha, strand), thickness))
                    {
                        line.StartCap = line.EndCap = LineCap.Round;
                        if (frontSide)
                        {
                            graphics.DrawArc(line, orbit, 4f + (animationPhase * 0.12f + i * 3f) % 18f, 172f);
                        }
                        else
                        {
                            graphics.DrawArc(line, orbit, 184f + (animationPhase * 0.08f + i * 2f) % 14f, 168f);
                        }
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawStarField(Graphics graphics, float scale, float intensity, Color bright)
        {
            int count = 16 + (int)(20 * intensity);
            for (int i = 0; i < count; i++)
            {
                double angle = (i * 137.508 + 23.0) * Math.PI / 180.0;
                float radius = ClientSize.Width * (0.18f + (i % 13) * 0.027f);
                float x = ClientSize.Width / 2f + (float)Math.Cos(angle) * radius;
                float y = ClientSize.Height / 2f + (float)Math.Sin(angle) * radius;
                float size = Math.Max(0.7f, (0.55f + i % 3 * 0.38f) * scale);
                int alpha = Math.Min(180, (int)((45 + i % 5 * 19) * intensity));
                using (Brush star = new SolidBrush(Color.FromArgb(alpha, i % 7 == 0 ? bright : Color.White)))
                {
                    graphics.FillEllipse(star, x, y, size, size);
                }
            }
        }

        private void DrawOrbitParticles(Graphics graphics, float scale, float intensity, Color bright)
        {
            int count = 5 + (int)(17 * intensity);
            float centerX = ClientSize.Width / 2f;
            float centerY = ClientSize.Height / 2f;
            for (int i = 0; i < count; i++)
            {
                double angle = ((i * 137.508) + animationPhase * (0.55 + (i % 4) * 0.12)) * Math.PI / 180.0;
                float radius = ClientSize.Width * (0.29f + (i % 7) * 0.021f);
                float x = centerX + (float)Math.Cos(angle) * radius;
                float y = centerY + (float)Math.Sin(angle) * radius * 0.46f - (float)Math.Cos(angle) * ClientSize.Height * 0.035f;
                float size = Math.Max(1.2f, (1.2f + (i % 3) * 0.65f) * scale);
                int alpha = Math.Min(235, (int)((75 + (i % 5) * 28) * intensity));
                using (Brush particle = new SolidBrush(Color.FromArgb(alpha, bright)))
                {
                    graphics.FillEllipse(particle, x - size / 2f, y - size / 2f, size, size);
                }
            }
        }

        private static Color GetAccentColor(VisualTheme theme)
        {
            switch (theme)
            {
                case VisualTheme.NebulaPurple: return Color.FromArgb(160, 104, 199);
                case VisualTheme.SolarAmber: return Color.FromArgb(226, 134, 62);
                default: return Color.FromArgb(119, 172, 203);
            }
        }

        private static Color GetBrightAccentColor(VisualTheme theme)
        {
            switch (theme)
            {
                case VisualTheme.NebulaPurple: return Color.FromArgb(246, 208, 255);
                case VisualTheme.SolarAmber: return Color.FromArgb(255, 232, 177);
                default: return Color.FromArgb(226, 244, 255);
            }
        }

        private static Icon CreateBlackHoleIcon()
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                using (Pen ring = new Pen(Color.SlateGray, 4f))
                {
                    graphics.DrawEllipse(ring, 3, 3, 25, 25);
                }
                using (Brush center = new SolidBrush(Color.Black))
                {
                    graphics.FillEllipse(center, 8, 8, 16, 16);
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }

    internal sealed class ConfirmRecycleDialog : Form
    {
        public ConfirmRecycleDialog(IList<string> paths, SafetyReport report)
        {
            Text = L.Text("ConfirmTitle");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(540, 410);
            Font = new Font("Microsoft YaHei UI", 9f);

            Label title = new Label();
            title.AutoSize = false;
            title.Location = new Point(20, 18);
            title.Size = new Size(500, 45);
            title.Font = new Font(Font, FontStyle.Bold);
            title.Text = string.Format(L.Text("ConfirmQuestion"), paths.Count);

            Label safety = new Label();
            safety.AutoSize = false;
            safety.Location = new Point(20, 62);
            safety.Size = new Size(500, 45);
            safety.ForeColor = Color.FromArgb(125, 70, 0);
            safety.Text = L.Text("ConfirmSafety");

            TextBox list = new TextBox();
            list.Location = new Point(20, 112);
            list.Size = new Size(500, 225);
            list.Multiline = true;
            list.ReadOnly = true;
            list.ScrollBars = ScrollBars.Both;
            list.WordWrap = false;
            list.Text = BuildPathList(paths, report);

            Button cancel = new Button();
            cancel.Text = L.Text("Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(336, 357);
            cancel.Size = new Size(88, 34);

            Button confirm = new Button();
            confirm.Text = L.Text("ConfirmButton");
            confirm.DialogResult = DialogResult.OK;
            confirm.Location = new Point(432, 357);
            confirm.Size = new Size(88, 34);

            Controls.Add(title);
            Controls.Add(safety);
            Controls.Add(list);
            Controls.Add(cancel);
            Controls.Add(confirm);

            AcceptButton = confirm;
            CancelButton = cancel;
        }

        private static string BuildPathList(IList<string> paths, SafetyReport report)
        {
            StringBuilder builder = new StringBuilder();

            foreach (string path in paths)
            {
                builder.AppendLine(path);
            }

            builder.AppendLine();
            builder.AppendFormat(
                L.Text("ScanSummary"),
                report.FileCount,
                report.FolderCount,
                FormatBytes(report.TotalBytes));

            return builder.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return string.Format("{0:0.##} {1}", value, units[unit]);
        }
    }

    internal sealed class SafetyReport
    {
        public bool IsSafe { get; set; }
        public string ErrorMessage { get; set; }
        public long TotalBytes { get; set; }
        public int FileCount { get; set; }
        public int FolderCount { get; set; }
    }

    internal sealed class RecycleResult
    {
        public bool Success { get; set; }
        public bool Aborted { get; set; }
        public int ProcessedCount { get; set; }
        public string ErrorMessage { get; set; }
        public string RecoveryVaultDirectory { get; set; }
    }

    internal static class RecycleService
    {
        public const long MaximumRecycleBytes = 2L * 1024L * 1024L * 1024L;
        private const int MaximumScannedEntries = 100000;
        private const uint FofNoConfirmation = 0x0010;
        private const uint FofAllowUndo = 0x0040;
        private const uint FofxRecycleOnDelete = 0x00080000;
        private const uint FofxAddUndoRecord = 0x20000000;

        private static readonly Guid ShellItemGuid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
        private static readonly Guid FileOperationClassGuid = new Guid("3AD05575-8857-4850-9277-11B85BDB8E09");

        public static bool ValidatePaths(IList<string> paths, out string error)
        {
            SafetyReport report = InspectPaths(paths, AppSettings.CreateDefault());
            error = report.ErrorMessage;
            return report.IsSafe;
        }

        public static SafetyReport InspectPaths(IList<string> paths)
        {
            return InspectPaths(paths, AppSettings.CreateDefault());
        }

        public static SafetyReport InspectPaths(IList<string> paths, AppSettings settings)
        {
            SafetyReport report = new SafetyReport();
            HashSet<string> visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int scannedEntries = 0;

            foreach (string path in paths)
            {
                if (path.StartsWith(@"\\", StringComparison.Ordinal))
                {
                    return Unsafe(M("不接受网络或 UNC 路径：", "Network and UNC paths are not allowed:"), path);
                }

                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    return Unsafe(M("项目不存在或无法访问：", "The item does not exist or is inaccessible:"), path);
                }

                string root;
                try
                {
                    root = Path.GetPathRoot(path);
                    if (string.IsNullOrEmpty(root))
                    {
                        throw new InvalidOperationException();
                    }
                }
                catch
                {
                    return Unsafe(M("无法确定项目所在的本机磁盘：", "The local drive cannot be determined:"), path);
                }

                if (string.Equals(
                    path.TrimEnd(Path.DirectorySeparatorChar),
                    root.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Unsafe(M("禁止回收整个磁盘根目录：", "A drive root cannot be recycled:"), path);
                }

                try
                {
                    DriveInfo drive = new DriveInfo(root);
                    if (drive.DriveType != DriveType.Fixed)
                    {
                        return Unsafe(M("只允许回收本机固定磁盘上的项目：", "Only items on fixed local drives are allowed:"), path);
                    }
                }
                catch
                {
                    return Unsafe(M("无法验证项目所在磁盘：", "The item's drive cannot be verified:"), path);
                }

                string criticalReason;
                try { PathSafety.RejectReparseChain(path); }
                catch(Exception ex) { return Unsafe(ex.Message,path); }
                if (IsCriticalPath(path, out criticalReason))
                {
                    return Unsafe(criticalReason, path);
                }

                string directoryRuleReason;
                if (IsBlockedByDirectoryRules(path, settings, out directoryRuleReason))
                {
                    return Unsafe(directoryRuleReason, path);
                }

                if (File.Exists(path))
                {
                    SafetyReport fileFailure = InspectFile(path, report, settings, ref scannedEntries);
                    if (fileFailure != null)
                    {
                        return fileFailure;
                    }
                }
                else
                {
                    Queue<string> pending = new Queue<string>();
                    pending.Enqueue(path);

                    while (pending.Count > 0)
                    {
                        string directory = pending.Dequeue();
                        if (IsCriticalPath(directory,out criticalReason)) return Unsafe(criticalReason,directory);
                        if (!visitedDirectories.Add(directory))
                        {
                            continue;
                        }

                        FileAttributes directoryAttributes;
                        try
                        {
                            directoryAttributes = File.GetAttributes(directory);
                        }
                        catch (Exception ex)
                        {
                            return Unsafe(M("无法读取文件夹属性，已阻止操作：", "Folder attributes cannot be read; operation blocked: ") + ex.Message, directory);
                        }

                        if ((directoryAttributes & FileAttributes.ReparsePoint) != 0)
                        {
                            return Unsafe(M("不允许回收包含重解析点的文件夹：", "Folders containing reparse points are not allowed:"), directory);
                        }

                        if ((directoryAttributes & FileAttributes.System) != 0)
                        {
                            return Unsafe(M("禁止回收具有系统属性的文件夹：", "Folders with the System attribute are protected:"), directory);
                        }

                        report.FolderCount++;
                        scannedEntries++;
                        if (scannedEntries > MaximumScannedEntries)
                        {
                            return Unsafe(M("项目超过安全扫描上限 100,000 项：", "The operation exceeds the 100,000-entry scan limit:"), path);
                        }

                        string[] entries;
                        try
                        {
                            entries = Directory.GetFileSystemEntries(directory);
                        }
                        catch (Exception ex)
                        {
                            return Unsafe(M("无法完整扫描文件夹，已阻止操作：", "The folder cannot be fully scanned; operation blocked: ") + ex.Message, directory);
                        }

                        foreach (string entry in entries)
                        {
                            if (Directory.Exists(entry))
                            {
                                pending.Enqueue(entry);
                            }
                            else if (File.Exists(entry))
                            {
                                SafetyReport fileFailure = InspectFile(entry, report, settings, ref scannedEntries);
                                if (fileFailure != null)
                                {
                                    return fileFailure;
                                }
                            }
                            else
                            {
                                return Unsafe(M("发现无法验证的目录项目：", "An unverifiable directory entry was found:"), entry);
                            }
                        }
                    }
                }
            }

            report.IsSafe = true;
            return report;
        }

        private static SafetyReport InspectFile(
            string path,
            SafetyReport report,
            AppSettings settings,
            ref int scannedEntries)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                PathSafety.RejectReparseChain(path);
                string criticalReason;
                if(IsCriticalPath(path,out criticalReason))return Unsafe(criticalReason,path);
                if ((attributes & FileAttributes.System) != 0)
                {
                    return Unsafe(M("禁止回收具有系统属性的文件：", "Files with the System attribute are protected:"), path);
                }

                long length = new FileInfo(path).Length;
                if (length > settings.MaximumFileSizeBytes)
                {
                    return Unsafe(
                        string.Format(
                            M("单个文件超过你设定的 {0} MB 上限：", "The file exceeds your configured {0} MB limit:"),
                            settings.MaximumFileSizeMb),
                        path);
                }

                if (report.TotalBytes > MaximumRecycleBytes - length)
                {
                    return Unsafe(M("本次项目总大小超过 2 GB 硬性上限：", "The operation exceeds the hard 2 GB total limit:"), path);
                }

                report.TotalBytes += length;
                report.FileCount++;
                scannedEntries++;
                if (scannedEntries > MaximumScannedEntries)
                {
                    return Unsafe(M("项目超过安全扫描上限 100,000 项：", "The operation exceeds the 100,000-entry scan limit:"), path);
                }
                return null;
            }
            catch (Exception ex)
            {
                return Unsafe(M("无法读取文件大小或属性，已阻止操作：", "File size or attributes cannot be read; operation blocked: ") + ex.Message, path);
            }
        }

        private static bool IsBlockedByDirectoryRules(string path, AppSettings settings, out string reason)
        {
            if (settings == null || settings.ProtectionMode == ProtectionMode.Default)
            {
                reason = null;
                return false;
            }

            string normalizedPath = NormalizeForComparison(path);
            List<string> rules = (settings.RulePaths ?? new List<string>())
                .Where(rule => !string.IsNullOrWhiteSpace(rule))
                .Select(NormalizeForComparison)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (settings.ProtectionMode == ProtectionMode.Blacklist)
            {
                bool blocked = rules.Any(rule =>
                    IsSameOrInside(normalizedPath, rule)
                    || IsSameOrInside(rule, normalizedPath));
                reason = blocked
                    ? M("项目命中你设置的黑名单目录：", "The item is blocked by your directory blacklist:")
                    : null;
                return blocked;
            }

            bool allowed = rules.Any(rule =>
                !string.Equals(normalizedPath, rule, StringComparison.OrdinalIgnoreCase)
                && IsSameOrInside(normalizedPath, rule));
            reason = allowed
                ? null
                : M(
                    "白名单模式只允许回收名单目录里面的内容，且不能回收名单目录本身：",
                    "Whitelist mode only allows contents inside listed folders, not the listed folder itself:");
            return !allowed;
        }

        private static bool IsCriticalPath(string path, out string reason)
        {
            string fullPath = NormalizeForComparison(path);
            string[] protectedTrees =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                RecoveryVault.RootDirectory
            };

            foreach (string protectedTree in protectedTrees)
            {
                if (!string.IsNullOrEmpty(protectedTree) && IsSameOrInside(fullPath, protectedTree))
                {
                    reason = M(
                        "禁止回收系统关键目录、恢复保险库及其内容：",
                        "System-critical locations, the recovery vault, and their contents are protected:");
                    return true;
                }
            }

            string[] protectedExactFolders =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                Path.GetDirectoryName(Application.ExecutablePath)
            };

            foreach (string protectedFolder in protectedExactFolders)
            {
                if (!string.IsNullOrEmpty(protectedFolder)
                    && string.Equals(
                        fullPath,
                        NormalizeForComparison(protectedFolder),
                        StringComparison.OrdinalIgnoreCase))
                {
                    reason = M("禁止回收用户关键目录或程序自身目录：", "Critical user folders and the application's own folder are protected:");
                    return true;
                }
            }

            string usersRoot = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            if (!string.IsNullOrEmpty(usersRoot)
                && string.Equals(
                    Path.GetDirectoryName(fullPath),
                    NormalizeForComparison(usersRoot),
                    StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(fullPath))
            {
                reason = M("禁止回收整个 Windows 用户配置目录：", "Whole Windows user-profile folders are protected:");
                return true;
            }

            if (string.Equals(
                fullPath,
                NormalizeForComparison(Application.ExecutablePath),
                StringComparison.OrdinalIgnoreCase))
            {
                reason = M("禁止回收正在运行的黑洞回收站程序：", "The running Black Hole Bin executable is protected:");
                return true;
            }

            reason = null;
            return false;
        }

        private static bool IsSameOrInside(string path, string parent)
        {
            string normalizedParent = NormalizeForComparison(parent);
            return string.Equals(path, normalizedParent, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeForComparison(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static SafetyReport Unsafe(string message, string path)
        {
            return new SafetyReport
            {
                IsSafe = false,
                ErrorMessage = message + "\r\n\r\n" + path
            };
        }

        private static string M(string chinese, string english)
        {
            return L.Language == AppLanguage.English ? english : chinese;
        }

        public static RecycleResult MoveToRecycleBin(IList<string> paths, IntPtr ownerWindow)
        {
            return MoveToRecycleBin(paths, ownerWindow, AppSettings.CreateDefault());
        }

        public static RecycleResult MoveToRecycleBin(
            IList<string> paths,
            IntPtr ownerWindow,
            AppSettings settings)
        {
            SafetyReport safety = InspectPaths(paths, settings);
            if (!safety.IsSafe)
            {
                throw new InvalidOperationException(safety.ErrorMessage);
            }

            DateTime operationStartedUtc = DateTime.UtcNow;
            string recoveryVault = settings.RecoveryVaultEnabled
                ? RecoveryVault.CreateBackup(paths)
                : null;

            IFileOperation operation = null;
            List<IShellItem> shellItems = new List<IShellItem>();
            var progress = new List<RecycleProgress>();
            var progressPointers = new List<IntPtr>();

            try
            {
                Type operationType = Type.GetTypeFromCLSID(FileOperationClassGuid, true);
                operation = (IFileOperation)Activator.CreateInstance(operationType);

                ThrowIfFailed(operation.SetOwnerWindow(ownerWindow));
                ThrowIfFailed(operation.SetOperationFlags(
                    FofNoConfirmation |
                    FofAllowUndo |
                    FofxRecycleOnDelete |
                    FofxAddUndoRecord));

                foreach (string path in paths)
                {
                    IShellItem item;
                    Guid guid = ShellItemGuid;
                    int createResult = SHCreateItemFromParsingName(path, IntPtr.Zero, ref guid, out item);
                    ThrowIfFailed(createResult);
                    shellItems.Add(item);
                    var sink = new RecycleProgress();
                    string guardedPath=path;
                    sink.Preflight=()=>InspectPaths(new List<string>{guardedPath},settings).IsSafe;
                    progress.Add(sink);
                    IntPtr pointer = Marshal.GetComInterfaceForObject(sink, typeof(IRecycleProgress));
                    progressPointers.Add(pointer);
                    ThrowIfFailed(operation.DeleteItem(item, pointer));
                }

                int operationResult = operation.PerformOperations();
                if (progress.Any(sink => sink.Blocked))
                    throw new IOException(M("Windows 未确认使用回收站，已阻止删除。原文件不会被本次拦截操作删除。", "Windows did not confirm recycling; deletion was blocked."));
                if (progress.Any(sink => sink.UnexpectedDeletion))
                    throw new IOException(M("Windows 未返回回收站项目。请使用撤回检查恢复副本。", "Windows returned no recycled item. Check the recovery copy using Undo."));
                ThrowIfFailed(operationResult);

                bool aborted;
                ThrowIfFailed(operation.GetAnyOperationsAborted(out aborted));

                int processed = progress.Count(sink => sink.Confirmed);

                if (!aborted && processed == paths.Count)
                {
                    return new RecycleResult
                    {
                        Success = true,
                        ProcessedCount = processed,
                        RecoveryVaultDirectory = recoveryVault
                    };
                }

                return new RecycleResult
                {
                    Success = false,
                    Aborted = aborted,
                    ProcessedCount = processed,
                    RecoveryVaultDirectory = recoveryVault,
                    ErrorMessage = string.Format(
                        M(
                            "只完成了 {0}/{1} 个项目。未完成的项目仍保留在原位置。",
                            "Only {0}/{1} items were processed. Unprocessed items remain at their original locations."),
                        processed,
                        paths.Count)
                };
            }
            finally
            {
                var completed = paths.Where((path, index) => index < progress.Count &&
                    (progress[index].Confirmed || progress[index].UnexpectedDeletion)).ToList();
                if (completed.Count > 0)
                    UndoRecordStore.Save(completed, operationStartedUtc, recoveryVault);
                foreach (IntPtr pointer in progressPointers) Marshal.Release(pointer);
                GC.KeepAlive(progress);
                foreach (IShellItem item in shellItems)
                {
                    if (item != null && Marshal.IsComObject(item))
                    {
                        Marshal.FinalReleaseComObject(item);
                    }
                }

                if (operation != null && Marshal.IsComObject(operation))
                {
                    Marshal.FinalReleaseComObject(operation);
                }
            }
        }

        private static void ThrowIfFailed(int hresult)
        {
            if (hresult < 0)
            {
                Marshal.ThrowExceptionForHR(hresult);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            string path,
            IntPtr bindContext,
            ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        [PreserveSig]
        int BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);

        [PreserveSig]
        int GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem parent);

        [PreserveSig]
        int GetDisplayName(uint displayNameType, out IntPtr name);

        [PreserveSig]
        int GetAttributes(uint mask, out uint attributes);

        [PreserveSig]
        int Compare([MarshalAs(UnmanagedType.Interface)] IShellItem other, uint hint, out int order);
    }

    [ComImport]
    [Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileOperation
    {
        [PreserveSig]
        int Advise(IntPtr progressSink, out uint cookie);

        [PreserveSig]
        int Unadvise(uint cookie);

        [PreserveSig]
        int SetOperationFlags(uint operationFlags);

        [PreserveSig]
        int SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);

        [PreserveSig]
        int SetProgressDialog(IntPtr progressDialog);

        [PreserveSig]
        int SetProperties(IntPtr propertyChangeArray);

        [PreserveSig]
        int SetOwnerWindow(IntPtr ownerWindow);

        [PreserveSig]
        int ApplyPropertiesToItem([MarshalAs(UnmanagedType.Interface)] IShellItem item);

        [PreserveSig]
        int ApplyPropertiesToItems([MarshalAs(UnmanagedType.IUnknown)] object items);

        [PreserveSig]
        int RenameItem([MarshalAs(UnmanagedType.Interface)] IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string newName, IntPtr progressSink);

        [PreserveSig]
        int RenameItems([MarshalAs(UnmanagedType.IUnknown)] object items, [MarshalAs(UnmanagedType.LPWStr)] string newName);

        [PreserveSig]
        int MoveItem([MarshalAs(UnmanagedType.Interface)] IShellItem item, [MarshalAs(UnmanagedType.Interface)] IShellItem destinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string newName, IntPtr progressSink);

        [PreserveSig]
        int MoveItems([MarshalAs(UnmanagedType.IUnknown)] object items, [MarshalAs(UnmanagedType.Interface)] IShellItem destinationFolder);

        [PreserveSig]
        int CopyItem([MarshalAs(UnmanagedType.Interface)] IShellItem item, [MarshalAs(UnmanagedType.Interface)] IShellItem destinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string copyName, IntPtr progressSink);

        [PreserveSig]
        int CopyItems([MarshalAs(UnmanagedType.IUnknown)] object items, [MarshalAs(UnmanagedType.Interface)] IShellItem destinationFolder);

        [PreserveSig]
        int DeleteItem([MarshalAs(UnmanagedType.Interface)] IShellItem item, IntPtr progressSink);

        [PreserveSig]
        int DeleteItems([MarshalAs(UnmanagedType.IUnknown)] object items);

        [PreserveSig]
        int NewItem([MarshalAs(UnmanagedType.Interface)] IShellItem destinationFolder, uint fileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string templateName, IntPtr progressSink);

        [PreserveSig]
        int PerformOperations();

        [PreserveSig]
        int GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
}

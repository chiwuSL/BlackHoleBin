using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BlackHoleBin
{
    internal static class VisualSnapshotTest
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string outputPath = args.Length > 0
                ? Path.GetFullPath(args[0])
                : Path.Combine(Environment.CurrentDirectory, "ui-preview.png");
            string settingsOutputPath = args.Length > 1
                ? Path.GetFullPath(args[1])
                : Path.Combine(Path.GetDirectoryName(outputPath), "settings-preview.png");
            string safetyOutputPath = args.Length > 2
                ? Path.GetFullPath(args[2])
                : Path.Combine(Path.GetDirectoryName(outputPath), "settings-safety-preview.png");
            string englishOutputPath = args.Length > 3
                ? Path.GetFullPath(args[3])
                : Path.Combine(Path.GetDirectoryName(outputPath), "settings-english-preview.png");
            string recoveryOutputPath = args.Length > 4
                ? Path.GetFullPath(args[4])
                : Path.Combine(Path.GetDirectoryName(outputPath), "settings-recovery-preview.png");
            string appearanceOutputPath = args.Length > 5
                ? Path.GetFullPath(args[5])
                : Path.Combine(Path.GetDirectoryName(outputPath), "settings-appearance-preview.png");

            using (BlackHoleForm form = new BlackHoleForm())
            {
                form.CreateControl();
                using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
                    bitmap.Save(outputPath, ImageFormat.Png);
                }
            }

            L.Language = AppLanguage.Chinese;
            using (SettingsDialog dialog = new SettingsDialog(AppSettings.CreateDefault()))
            {
                dialog.StartPosition = FormStartPosition.Manual;
                dialog.Location = new Point(-32000, -32000);
                dialog.Show();
                Application.DoEvents();
                TabControl tabs = dialog.Controls.OfType<TabControl>().First();
                tabs.SelectedIndex = 2;
                Application.DoEvents();
                tabs.SelectedIndex = 0;
                dialog.Refresh();
                Application.DoEvents();
                using (Bitmap bitmap = new Bitmap(dialog.Width, dialog.Height))
                {
                    dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
                    bitmap.Save(settingsOutputPath, ImageFormat.Png);
                }

                tabs.SelectedIndex = 1;
                Application.DoEvents();
                using (Bitmap bitmap = new Bitmap(dialog.Width, dialog.Height))
                {
                    dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
                    bitmap.Save(appearanceOutputPath, ImageFormat.Png);
                }

                tabs.SelectedIndex = 2;
                Application.DoEvents();
                using (Bitmap bitmap = new Bitmap(dialog.Width, dialog.Height))
                {
                    dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
                    bitmap.Save(safetyOutputPath, ImageFormat.Png);
                }

                tabs.SelectedIndex = 3;
                dialog.Refresh();
                Application.DoEvents();
                using (Bitmap bitmap = new Bitmap(dialog.Width, dialog.Height))
                {
                    dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
                    bitmap.Save(recoveryOutputPath, ImageFormat.Png);
                }
                dialog.Hide();
            }

            L.Language = AppLanguage.English;
            AppSettings englishSettings = AppSettings.CreateDefault();
            englishSettings.Language = AppLanguage.English;
            using (SettingsDialog dialog = new SettingsDialog(englishSettings))
            {
                dialog.StartPosition = FormStartPosition.Manual;
                dialog.Location = new Point(-32000, -32000);
                dialog.Show();
                Application.DoEvents();
                using (Bitmap bitmap = new Bitmap(dialog.Width, dialog.Height))
                {
                    dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
                    bitmap.Save(englishOutputPath, ImageFormat.Png);
                }
                dialog.Hide();
            }

            Console.WriteLine("PASS: " + outputPath);
            Console.WriteLine("PASS: " + settingsOutputPath);
            Console.WriteLine("PASS: " + safetyOutputPath);
            Console.WriteLine("PASS: " + englishOutputPath);
            Console.WriteLine("PASS: " + recoveryOutputPath);
            Console.WriteLine("PASS: " + appearanceOutputPath);
            return 0;
        }
    }
}

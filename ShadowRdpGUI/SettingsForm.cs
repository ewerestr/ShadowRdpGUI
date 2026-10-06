using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShadowRdpGUI
{
    public class SettingsForm : Form
    {
        public AppSettings Settings { get; }

        private readonly CheckBox chkDebug;
        private readonly CheckBox chkLog;
        private readonly CheckBox chkDump;
        private readonly CheckBox chkDark;

        private const string AppVersion = "1.1";
        private const string GithubUrl = "https://github.com/ewerestr/ShadowRdpGUI";

        public SettingsForm(AppSettings current)
        {
            Settings = current;

            Text = "Настройки ShadowRdpGUI";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(420, 350);

            chkDebug = new CheckBox
            {
                Text = "Включить отладочный вывод",
                Location = new Point(15, 15),
                AutoSize = true,
                Checked = current.EnableDebug
            };

            chkLog = new CheckBox
            {
                Text = "Вести лог в файл (shadowrdp.log)",
                Location = new Point(15, 45),
                AutoSize = true,
                Checked = current.EnableLog
            };

            chkDump = new CheckBox
            {
                Text = "Сохранять дамп окон (windows_dump.txt)",
                Location = new Point(15, 75),
                AutoSize = true,
                Checked = current.EnableWindowDump
            };

            chkDark = new CheckBox
            {
                Text = "Тёмная тема",
                Location = new Point(15, 105),
                AutoSize = true,
                Checked = current.DarkTheme
            };

            var lblHint = new Label
            {
                Text = "Файлы сохраняются в:\r\n" + Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ShadowRdpGUI"),
                Location = new Point(15, 145),
                AutoSize = true,
                ForeColor = Color.Gray
            };

            var lblVersion = new Label
            {
                Text = $"ShadowRdpGUI v{AppVersion}",
                Location = new Point(15, 210),
                AutoSize = true,
                ForeColor = Color.Gray
            };

            var lnkGithub = new LinkLabel
            {
                Text = GithubUrl,
                Location = new Point(15, 232),
                AutoSize = true,
                LinkColor = Color.FromArgb(0, 102, 204),
                ActiveLinkColor = Color.FromArgb(0, 153, 255),
                VisitedLinkColor = Color.FromArgb(102, 0, 204)
            };
            lnkGithub.LinkClicked += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = GithubUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Не удалось открыть ссылку:\r\n" + ex.Message,
                        "Ошибка",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            };

            var btnOpen = new Button
            {
                Text = "Открыть папку",
                Location = new Point(15, 305),
                Size = new Size(120, 28)
            };
            btnOpen.Click += (s, e) =>
            {
                try
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "ShadowRdpGUI");
                    Directory.CreateDirectory(dir);
                    Process.Start("explorer.exe", dir);
                }
                catch { }
            };

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(235, 305),
                Size = new Size(75, 28)
            };
            btnOk.Click += (s, e) => Apply();

            var btnCancel = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Location = new Point(320, 305),
                Size = new Size(85, 28)
            };

            Controls.AddRange(new Control[]
            {
            chkDebug, chkLog, chkDump, chkDark,
            lblHint, lblVersion, lnkGithub,
            btnOpen, btnOk, btnCancel
            });

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void Apply()
        {
            Settings.EnableDebug = chkDebug.Checked;
            Settings.EnableLog = chkLog.Checked;
            Settings.EnableWindowDump = chkDump.Checked;
            Settings.DarkTheme = chkDark.Checked;

            Logger.DebugEnabled = Settings.EnableDebug;
            Logger.LogEnabled = Settings.EnableLog;
            Logger.DumpEnabled = Settings.EnableWindowDump;
        }
    }
}
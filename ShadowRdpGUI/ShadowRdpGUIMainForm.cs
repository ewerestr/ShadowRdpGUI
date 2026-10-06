using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShadowRdpGUI
{
    public partial class ShadowRdpGUIMainForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
        
        private const string MSTSC_WINDOW_CLASS = "SrApiViewerAxContainerClass";

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private const uint WM_CLOSE = 0x0010;
        private const uint WM_SIZE = 0x0005;

        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_BORDER = 0x00800000;

        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOZORDER = 0x0004;

        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private const uint SMTO_NORMAL = 0x0000;

        private System.Windows.Forms.Timer refreshTimer;
        private readonly int _currentSessionId;
        private AppSettings _settings;

        public ShadowRdpGUIMainForm()
        {
            InitializeComponent();
            Logger.Write("[CONSTRUCTOR] Старт инициализации формы");

            // === Настройки + логгер ===
            _settings = AppSettings.Load();
            Logger.DebugEnabled = _settings.EnableDebug;
            Logger.LogEnabled = _settings.EnableLog;
            Logger.DumpEnabled = _settings.EnableWindowDump;
            Logger.Write($"=== ShadowRdpGUI запущен. ConfigPath={AppSettings.ConfigPath} ===");
            Logger.Debug($"[CONSTRUCTOR] Debug={_settings.EnableDebug}, Log={_settings.EnableLog}, Dump={_settings.EnableWindowDump}, Dark={_settings.DarkTheme}");

            // === Заголовок ===
            _currentSessionId = Process.GetCurrentProcess().SessionId;
            string userName = Environment.UserName;
            this.Text = $"ShadowRdpGUI - {userName}, SessionID:{_currentSessionId}";
            Logger.Write($"[CONSTRUCTOR] Пользователь={userName}, SessionId={_currentSessionId}");

            // === Таймер автообновления ===
            refreshTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            refreshTimer.Tick += refreshTimer_Tick;
            refreshTimer.Start();

            // === Подписки ===
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
            chkAutoRefresh.CheckedChanged += ChkAutoRefresh_CheckedChanged;
            btnSettings.Click += BtnSettings_Click;
            listBoxSessions.DrawItem += listBoxSessions_DrawItem;
            listBoxSessions.DoubleClick += listBoxSessions_DoubleClick;
            tabSessions.MouseUp += tabSessions_MouseUp;

            // === Тема ===
            ApplyTheme(_settings.DarkTheme);

            RefreshSessionList();
            Logger.Write("[CONSTRUCTOR] Инициализация завершена");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Logger.Debug("[LOAD] Form1_Load");
            listBoxSessions.ItemHeight = listBoxSessions.Font.Height + 6;
            RefreshSessionList();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Logger.Write($"[FORM-CLOSING] Закрытие формы. Открыто вкладок: {tabSessions.TabPages.Count}");

            foreach (TabPage page in tabSessions.TabPages)
            {
                if (page is RdpTabPage rdpTab && rdpTab.RdpProcess != null)
                {
                    try
                    {
                        if (!rdpTab.RdpProcess.HasExited)
                        {
                            Logger.Write($"[FORM-CLOSING] Kill PID={rdpTab.RdpProcess.Id} user={rdpTab.Session?.Username}");
                            rdpTab.RdpProcess.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"[FORM-CLOSING] Ошибка Kill: {ex.Message}");
                    }
                }
            }

            Logger.Write("=== ShadowRdpGUI завершён ===");
        }

        private void refreshTimer_Tick(object sender, EventArgs e)
        {
            if (!chkAutoRefresh.Checked) return;
            RefreshSessionList();
        }

        private void ChkAutoRefresh_CheckedChanged(object sender, EventArgs e)
        {
            Logger.Write($"[REFRESH] Автообновление: {(chkAutoRefresh.Checked ? "включено" : "выключено")}");
            if (chkAutoRefresh.Checked)
                RefreshSessionList();
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            Logger.Debug("[SETTINGS] Открытие диалога настроек");
            using (var dlg = new SettingsForm(_settings))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _settings.Save();
                    Logger.Write($"[SETTINGS] Сохранены: Debug={_settings.EnableDebug}, Log={_settings.EnableLog}, Dump={_settings.EnableWindowDump}, Dark={_settings.DarkTheme}");
                    ApplyTheme(_settings.DarkTheme);
                }
                else
                {
                    Logger.Debug("[SETTINGS] Диалог отменён");
                }
            }
        }

        private void RefreshSessionList()
        {
            try
            {
                string selectedUsername = (listBoxSessions.SelectedItem as SessionInfo)?.Username;

                listBoxSessions.BeginUpdate();
                listBoxSessions.Items.Clear();

                var sessions = WtsManager.GetActiveSessions();
                Logger.Debug($"[REFRESH] Получено сессий: {sessions.Count}");

                foreach (var session in sessions)
                    listBoxSessions.Items.Add(session);

                listBoxSessions.EndUpdate();

                if (selectedUsername != null)
                {
                    for (int i = 0; i < listBoxSessions.Items.Count; i++)
                    {
                        if (listBoxSessions.Items[i] is SessionInfo s && s.Username == selectedUsername)
                        {
                            listBoxSessions.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"[REFRESH] ОШИБКА: {ex}");
            }
        }

        private void listBoxSessions_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= listBoxSessions.Items.Count) return;
            if (!(listBoxSessions.Items[e.Index] is SessionInfo session)) return;

            bool dark = _settings?.DarkTheme == true;

            // Фон рисуем вручную, чтобы поддержать тёмную тему
            Color bgColor;
            Color textColor;

            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
            {
                bgColor = dark ? Color.FromArgb(0, 120, 215) : SystemColors.Highlight;
                textColor = Color.White;
            }
            else
            {
                bgColor = dark ? Color.FromArgb(45, 45, 48) : Color.White;

                if (session.SessionId == _currentSessionId)
                    textColor = dark ? Color.IndianRed : Color.Firebrick;
                else if (IsUserTabOpen(session.Username))
                    textColor = dark ? Color.LightGreen : Color.Green;
                else
                    textColor = dark ? Color.WhiteSmoke : Color.Black;
            }

            using (var b = new SolidBrush(bgColor))
                e.Graphics.FillRectangle(b, e.Bounds);

            TextRenderer.DrawText(
                e.Graphics,
                session.Username,
                e.Font,
                e.Bounds,
                textColor,
                TextFormatFlags.Left
                    | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.NoPrefix);

            e.DrawFocusRectangle();
        }

        private void listBoxSessions_DoubleClick(object sender, EventArgs e)
        {
            if (listBoxSessions.SelectedItem is SessionInfo session)
            {
                Logger.Debug($"[START] DoubleClick по '{session.Username}' (SessionId={session.SessionId})");
                StartShadowSession(session);
            }
        }

        private void StartShadowSession(SessionInfo session)
        {
            Logger.Write($"[START] Запрос подключения к '{session.Username}' (SessionId={session.SessionId})");

            if (session.SessionId == _currentSessionId)
            {
                Logger.Write("[START] Отказ: попытка подключения к своему же сеансу");
                MessageBox.Show(
                    "Это ваш собственный сеанс. Подключение к нему через Shadow RDP невозможно.",
                    "Недоступно",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            foreach (TabPage page in tabSessions.TabPages)
            {
                if (page is RdpTabPage existing && existing.Session.SessionId == session.SessionId)
                {
                    Logger.Debug($"[START] Вкладка уже существует, переключаюсь на неё");
                    tabSessions.SelectedTab = existing;
                    return;
                }
            }

            var tab = new RdpTabPage(session);
            tab.Resize += Tab_Resize;

            var existingWindows = new HashSet<IntPtr>(FindMstscWindows());
            Logger.Debug($"[START] Существующих SrApiViewer-окон до запуска: {existingWindows.Count}");
            Logger.DumpWindows($"[START] До запуска mstsc (user={session.Username}, sessionId={session.SessionId})");

            var psi = new ProcessStartInfo
            {
                FileName = "mstsc.exe",
                Arguments = $"/shadow:{session.SessionId} /control /v:localhost",
                UseShellExecute = false
            };

            var process = Process.Start(psi);
            tab.RdpProcess = process;
            Logger.Write($"[START] mstsc запущен. PID={process.Id}, args='{psi.Arguments}'");

            tabSessions.TabPages.Add(tab);
            tabSessions.SelectedTab = tab;

            Task.Run(() => WaitAndEmbed(tab, process, existingWindows));

            listBoxSessions.Invalidate();
        }

        private List<IntPtr> FindMstscWindows()
        {
            var result = new List<IntPtr>();
            EnumWindows((hWnd, lParam) =>
            {
                var sb = new StringBuilder(256);
                GetClassName(hWnd, sb, sb.Capacity);

                if (sb.ToString() == MSTSC_WINDOW_CLASS && GetParent(hWnd) == IntPtr.Zero)
                    result.Add(hWnd);

                return true;
            }, IntPtr.Zero);
            return result;
        }

        private void WaitAndEmbed(RdpTabPage tab, Process process, HashSet<IntPtr> existingWindows)
        {
            Logger.Debug($"[WAIT] Начало ожидания окна для '{tab.Session.Username}' (PID={process.Id})");
            IntPtr handle = IntPtr.Zero;
            int foundAtIteration = -1;

            for (int i = 0; i < 150; i++)
            {
                if (tab.IsDisposed)
                {
                    Logger.Debug($"[WAIT] Вкладка disposed на итерации {i}, выходим");
                    return;
                }

                foreach (var h in FindMstscWindows())
                {
                    if (existingWindows.Contains(h)) continue;
                    if (!IsWindowVisible(h)) continue;
                    handle = h;
                    foundAtIteration = i;
                    break;
                }
                if (handle != IntPtr.Zero) break;

                Thread.Sleep(200);
            }

            Logger.DumpWindows($"[WAIT] После ожидания (user={tab.Session.Username}, handle=0x{handle.ToInt64():X8}, iteration={foundAtIteration})");

            if (handle == IntPtr.Zero)
            {
                Logger.Write($"[WAIT] ТАЙМАУТ: окно не найдено за 30с для '{tab.Session.Username}'. Удаляю вкладку.");
                RemoveTabSafe(tab);
                return;
            }

            Logger.Write($"[WAIT] Найдено окно 0x{handle.ToInt64():X8} на итерации {foundAtIteration} (~{foundAtIteration * 200} мс)");

            if (tab.IsDisposed)
            {
                Logger.Debug("[WAIT] Вкладка disposed после нахождения окна, выходим");
                return;
            }

            tab.EmbeddedHandle = handle;

            if (tab.InvokeRequired)
                tab.Invoke(new Action(() => EmbedWindow(tab, handle)));
            else
                EmbedWindow(tab, handle);
        }

        private void EmbedWindow(RdpTabPage tab, IntPtr handle)
        {
            Logger.Write($"[EMBED] Внедрение окна 0x{handle.ToInt64():X8} в '{tab.Session.Username}'");

            try
            {
                ShowWindow(handle, SW_HIDE);
                IntPtr oldParent = SetParent(handle, tab.Handle);
                Logger.Debug($"[EMBED] SetParent: старый родитель=0x{oldParent.ToInt64():X8}, новый=0x{tab.Handle.ToInt64():X8}");

                int style = GetWindowLong(handle, GWL_STYLE);
                style &= ~WS_CAPTION;
                style &= ~WS_THICKFRAME;
                style &= ~WS_BORDER;
                SetWindowLong(handle, GWL_STYLE, style);

                SetWindowPos(
                    handle, IntPtr.Zero,
                    0, 0, tab.ClientSize.Width, tab.ClientSize.Height,
                    SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOZORDER);

                ShowWindow(handle, SW_SHOW);
                tab.Invalidate();
                tab.Update();
                Logger.Debug($"[EMBED] Размер вкладки: {tab.ClientSize.Width}x{tab.ClientSize.Height}");

                Task.Delay(700).ContinueWith(_ => TryResize(tab));
                Task.Delay(2000).ContinueWith(_ => TryResize(tab));
            }
            catch (Exception ex)
            {
                Logger.Write($"[EMBED] ОШИБКА: {ex}");
            }
        }

        private void TryResize(RdpTabPage tab)
        {
            try
            {
                if (!tab.IsDisposed && !tab.IsClosing)
                    tab.BeginInvoke(new Action(() => ResizeEmbeddedWindow(tab)));
            }
            catch (Exception ex)
            {
                Logger.Debug($"[RESIZE] TryResize ошибка: {ex.Message}");
            }
        }

        private void Tab_Resize(object sender, EventArgs e)
        {
            if (sender is RdpTabPage tab && tab.EmbeddedHandle != IntPtr.Zero && !tab.IsClosing)
                ResizeEmbeddedWindow(tab);
        }

        private void ResizeEmbeddedWindow(RdpTabPage tab)
        {
            if (tab.EmbeddedHandle == IntPtr.Zero) return;
            if (tab.IsClosing) return;

            int w = tab.ClientSize.Width;
            int h = tab.ClientSize.Height;

            MoveWindow(tab.EmbeddedHandle, 0, 0, w, h, true);

            IntPtr lParam = (IntPtr)((h << 16) | (w & 0xFFFF));
            SendMessageTimeout(
                tab.EmbeddedHandle,
                WM_SIZE,
                IntPtr.Zero,
                lParam,
                SMTO_ABORTIFHUNG | SMTO_NORMAL,
                200,
                out _);

            Logger.Debug($"[RESIZE] '{tab.Session.Username}' -> {w}x{h}");
        }

        private void tabSessions_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            for (int i = 0; i < tabSessions.TabCount; i++)
            {
                if (!tabSessions.GetTabRect(i).Contains(e.Location)) continue;

                tabSessions.SelectedIndex = i;

                if (tabSessions.TabPages[i] is RdpTabPage rdpTab)
                {
                    var menu = new ContextMenuStrip();
                    var closeItem = menu.Items.Add("Закрыть подключение");
                    closeItem.Click += (s, args) => CloseTab(rdpTab);
                    menu.Show(tabSessions, e.Location);
                }
                break;
            }
        }

        private void CloseTab(RdpTabPage tab)
        {
            if (tab == null) return;
            if (tab.IsClosing) return;
            if (!tabSessions.TabPages.Contains(tab)) return;

            tab.IsClosing = true;
            Logger.Write($"[CLOSE] Начало закрытия '{tab.Session?.Username}', PID={tab.RdpProcess?.Id}");

            IntPtr handle = tab.EmbeddedHandle;
            Process proc = tab.RdpProcess;
            tab.EmbeddedHandle = IntPtr.Zero;

            if (handle != IntPtr.Zero)
            {
                try { ShowWindow(handle, SW_HIDE); }
                catch (Exception ex) { Logger.Debug($"[CLOSE] ShowWindow ошибка: {ex.Message}"); }
            }

            if (handle != IntPtr.Zero)
            {
                try
                {
                    SetParent(handle, IntPtr.Zero);
                    Logger.Debug($"[CLOSE] Окно 0x{handle.ToInt64():X8} отвязано");
                }
                catch (Exception ex) { Logger.Debug($"[CLOSE] SetParent ошибка: {ex.Message}"); }
            }

            tabSessions.TabPages.Remove(tab);
            listBoxSessions.Invalidate();
            Logger.Debug("[CLOSE] Вкладка удалена из TabControl");

            Task.Run(() =>
            {
                if (handle != IntPtr.Zero)
                {
                    try { PostMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero); }
                    catch { }
                }

                try
                {
                    if (proc != null && !proc.HasExited)
                    {
                        if (!proc.WaitForExit(3000))
                        {
                            Logger.Write($"[CLOSE] PID={proc.Id} не завершился за 3с, Kill");
                            proc.Kill();
                            proc.WaitForExit(2000);
                        }
                        else
                        {
                            Logger.Debug($"[CLOSE] PID={proc.Id} завершился сам");
                        }
                    }
                }
                catch (Exception ex) { Logger.Debug($"[CLOSE] WaitForExit ошибка: {ex.Message}"); }

                try
                {
                    tab.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (!tab.IsDisposed) tab.Dispose();
                            Logger.Write($"[CLOSE] Завершено для '{tab.Session?.Username}'");
                        }
                        catch { }
                    }));
                }
                catch { }
            });
        }

        private void RemoveTabSafe(RdpTabPage tab)
        {
            try
            {
                tab.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (tabSessions.TabPages.Contains(tab))
                            tabSessions.TabPages.Remove(tab);
                        if (!tab.IsDisposed)
                            tab.Dispose();
                        listBoxSessions.Invalidate();
                    }
                    catch { }
                }));
            }
            catch { }
        }

        private bool IsUserTabOpen(string username)
        {
            foreach (TabPage page in tabSessions.TabPages)
            {
                if (page is RdpTabPage rdp && rdp.Session.Username == username)
                    return true;
            }
            return false;
        }

        private void ApplyTheme(bool dark)
        {
            Logger.Debug($"[THEME] Применение темы: {(dark ? "dark" : "light")}");

            Color windowBack = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
            Color inputBack = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Window;
            Color fore = dark ? Color.WhiteSmoke : SystemColors.ControlText;
            Color border = dark ? Color.FromArgb(70, 70, 70) : SystemColors.ControlDark;

            this.BackColor = windowBack;
            this.ForeColor = fore;

            ApplyThemeRecursive(this, windowBack, inputBack, fore, border, dark);

            this.Invalidate(true);
            listBoxSessions.Invalidate();
        }

        private void ApplyThemeRecursive(Control parent, Color windowBack, Color inputBack, Color fore, Color border, bool dark)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is TextBox || c is ListBox || c is RichTextBox)
                {
                    c.BackColor = inputBack;
                    c.ForeColor = fore;
                }
                else if (c is Button btn)
                {
                    btn.BackColor = dark ? Color.FromArgb(60, 60, 60) : SystemColors.Control;
                    btn.ForeColor = fore;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = border;
                }
                else if (c is TabControl tabCtrl)
                {
                    tabCtrl.BackColor = windowBack;
                    tabCtrl.ForeColor = fore;
                }
                else if (c is TabPage tabPage)
                {
                    tabPage.BackColor = windowBack;
                    tabPage.ForeColor = fore;
                }
                else if (c is SplitContainer sp)
                {
                    sp.BackColor = border;
                    sp.Panel1.BackColor = windowBack;
                    sp.Panel2.BackColor = windowBack;
                    sp.ForeColor = fore;
                }
                else if (c is CheckBox || c is RadioButton)
                {
                    c.BackColor = Color.Transparent;
                    c.ForeColor = fore;
                }
                else if (c is Label)
                {
                    c.BackColor = Color.Transparent;
                    c.ForeColor = fore;
                }
                else
                {
                    c.BackColor = windowBack;
                    c.ForeColor = fore;
                }

                if (c.HasChildren)
                    ApplyThemeRecursive(c, windowBack, inputBack, fore, border, dark);
            }
        }
    }
}
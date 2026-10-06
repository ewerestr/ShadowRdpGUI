using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ShadowRdpGUI_Lite
{
    public partial class ShadowRdpGUILiteMainForm : Form
    {
        private readonly int _currentSessionId;

        public ShadowRdpGUILiteMainForm()
        {
            InitializeComponent();
            _currentSessionId = Process.GetCurrentProcess().SessionId;
            Text = $"ShadowRdpGUI Lite - ID:{_currentSessionId}";
        }

        private void RefreshSessionList()
        {
            string selectedUsername = (listBoxSessions.SelectedItem as SessionInfo)?.Username;

            listBoxSessions.BeginUpdate();
            listBoxSessions.Items.Clear();

            foreach (var s in WtsManager.GetActiveSessions())
                listBoxSessions.Items.Add(s);

            listBoxSessions.EndUpdate();

            // Восстановить выделение
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

        private void ListBoxSessions_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= listBoxSessions.Items.Count) return;
            if (!(listBoxSessions.Items[e.Index] is SessionInfo session)) return;

            e.DrawBackground();

            Color color;
            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                color = Color.White;
            else if (session.SessionId == _currentSessionId)
                color = Color.Firebrick;
            else
                color = Color.Black;

            TextRenderer.DrawText(
                e.Graphics,
                session.Username,
                e.Font,
                e.Bounds,
                color,
                TextFormatFlags.Left
                    | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.NoPrefix);

            e.DrawFocusRectangle();
        }

        private void ListBoxSessions_DoubleClick(object sender, EventArgs e)
        {
            if (!(listBoxSessions.SelectedItem is SessionInfo session)) return;

            if (session.SessionId == _currentSessionId)
            {
                MessageBox.Show(
                    "Это ваш собственный сеанс. Подключение к нему невозможно.",
                    "Недоступно",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "mstsc.exe",
                    Arguments = $"/shadow:{session.SessionId} /control /v:localhost",
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось запустить mstsc:\r\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            RefreshSessionList();
        }

        private void ShadowRdpGUILiteMainForm_Load(object sender, EventArgs e)
        {
            RefreshSessionList();
        }

        private void lblLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/ewerestr/ShadowRdpGUI",
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
        }
    }
}

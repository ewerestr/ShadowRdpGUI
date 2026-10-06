using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace ShadowRdpGUI
{
    public class RdpTabPage : TabPage
    {
        public SessionInfo Session { get; set; }
        public Process RdpProcess { get; set; }
        public IntPtr EmbeddedHandle { get; set; } = IntPtr.Zero;
        public bool IsClosing { get; set; } = false;

        public RdpTabPage(SessionInfo session) : base(session.Username)
        {
            Session = session;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ShadowRdpGUI_Lite
{
    public static class WtsManager
    {
        [DllImport("Wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int WTSEnumerateSessionsW(
            IntPtr hServer, int Reserved, int Version,
            ref IntPtr ppSessionInfo, ref int pCount);

        [DllImport("Wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("Wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool WTSQuerySessionInformationW(
            IntPtr hServer, int sessionId, WTS_INFO_CLASS wtsInfoClass,
            out IntPtr ppBuffer, out int pBytesReturned);

        private enum WTS_INFO_CLASS { WTSUserName = 5 }

        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            public int SessionID;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pWinStationName;
            public int State;
        }

        public static List<SessionInfo> GetActiveSessions()
        {
            var result = new List<SessionInfo>();
            IntPtr ppSessionInfo = IntPtr.Zero;
            int count = 0;

            if (WTSEnumerateSessionsW(IntPtr.Zero, 0, 1, ref ppSessionInfo, ref count) == 0)
                return result;

            try
            {
                int dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                IntPtr current = ppSessionInfo;

                for (int i = 0; i < count; i++)
                {
                    var si = (WTS_SESSION_INFO)Marshal.PtrToStructure(current, typeof(WTS_SESSION_INFO));
                    current = IntPtr.Add(current, dataSize);

                    if (si.pWinStationName == null) continue;
                    if (!si.pWinStationName.StartsWith("rdp-tcp", StringComparison.OrdinalIgnoreCase)) continue;

                    string user = GetString(si.SessionID, WTS_INFO_CLASS.WTSUserName);
                    if (string.IsNullOrWhiteSpace(user) || user == "SYSTEM") continue;

                    result.Add(new SessionInfo
                    {
                        SessionId = si.SessionID,
                        Username = user
                    });
                }
            }
            finally
            {
                WTSFreeMemory(ppSessionInfo);
            }

            return result;
        }

        private static string GetString(int sessionId, WTS_INFO_CLASS infoClass)
        {
            IntPtr buffer = IntPtr.Zero;
            int bytes = 0;
            string result = string.Empty;

            if (WTSQuerySessionInformationW(IntPtr.Zero, sessionId, infoClass, out buffer, out bytes))
            {
                try
                {
                    if (bytes > 2)
                        result = Marshal.PtrToStringUni(buffer) ?? string.Empty;
                }
                finally
                {
                    WTSFreeMemory(buffer);
                }
            }
            return result;
        }
    }
}

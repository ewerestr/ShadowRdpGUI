using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ShadowRdpGUI
{
    public class SessionInfo
    {
        public int SessionId { get; set; }
        public string Username { get; set; }
        public string State { get; set; }

        public override string ToString() => Username;
    }

    public static class WtsManager
    {
        // ВАЖНО: указываем CharSet.Unicode и суффикс W, чтобы получить UTF-16
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

        private enum WTS_INFO_CLASS
        {
            WTSUserName = 5,
            WTSDomainName = 7,
            WTSConnectState = 8
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            public int SessionID;
            // Меняем LPStr на LPWStr, т.к. работаем с Unicode-версией
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pWinStationName;
            public int State;
        }

        public static List<SessionInfo> GetActiveSessions()
        {
            var sessions = new List<SessionInfo>();
            IntPtr ppSessionInfo = IntPtr.Zero;
            int count = 0;

            if (WTSEnumerateSessionsW(IntPtr.Zero, 0, 1, ref ppSessionInfo, ref count) != 0)
            {
                try
                {
                    int dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                    IntPtr current = ppSessionInfo;

                    for (int i = 0; i < count; i++)
                    {
                        var si = (WTS_SESSION_INFO)Marshal.PtrToStructure(current, typeof(WTS_SESSION_INFO));
                        current = IntPtr.Add(current, dataSize);

                        // Фильтруем только RDP-сессии
                        if (si.pWinStationName != null &&
                            si.pWinStationName.StartsWith("rdp-tcp", StringComparison.OrdinalIgnoreCase))
                        {
                            string userName = GetSessionString(si.SessionID, WTS_INFO_CLASS.WTSUserName);

                            // Пустые логины отсекают слушателя (rdp-tcp) и служебные сессии
                            if (!string.IsNullOrWhiteSpace(userName) && userName != "SYSTEM")
                            {
                                sessions.Add(new SessionInfo
                                {
                                    SessionId = si.SessionID,
                                    Username = userName,
                                    State = GetStateString(si.State)
                                });
                            }
                        }
                    }
                }
                finally
                {
                    WTSFreeMemory(ppSessionInfo);
                }
            }
            return sessions;
        }

        private static string GetSessionString(int sessionId, WTS_INFO_CLASS infoClass)
        {
            IntPtr buffer = IntPtr.Zero;
            int bytesReturned = 0;
            string result = string.Empty;

            if (WTSQuerySessionInformationW(IntPtr.Zero, sessionId, infoClass, out buffer, out bytesReturned))
            {
                try
                {
                    // Для Unicode-строки минимум 2 байта (сам нуль-терминатор)
                    if (bytesReturned > 2)
                    {
                        result = Marshal.PtrToStringUni(buffer) ?? string.Empty;
                    }
                }
                finally
                {
                    WTSFreeMemory(buffer);
                }
            }
            return result;
        }

        private static string GetStateString(int state)
        {
            switch (state)
            {
                case 0: return "Active";
                case 1: return "Connected";
                case 2: return "ConnectQuery";
                case 3: return "Shadow";
                case 4: return "Disconnected";
                case 5: return "Idle";
                case 6: return "Listen";
                case 7: return "Reset";
                case 8: return "Down";
                case 9: return "Init";
                default: return "Unknown";
            }
        }
    }
}

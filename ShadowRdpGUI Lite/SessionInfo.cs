namespace ShadowRdpGUI_Lite
{
    public class SessionInfo
    {
        public int SessionId { get; set; }
        public string Username { get; set; }

        public override string ToString() => Username;
    }
}

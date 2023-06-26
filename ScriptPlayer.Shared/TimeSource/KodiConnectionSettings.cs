﻿namespace ScriptPlayer.Shared
{
    public class KodiConnectionSettings
    {
        public string User { get; set; }
        public string Password { get; set; }

        public string Ip { get; set; }
        public int HttpPort { get; set; }
        public int TcpPort { get; set; }

        public KodiConnectionSettings()
        {
            Ip = DefaultIp;
            HttpPort = DefaultHttpPort;
            TcpPort = DefaultTcpPort;
            User = DefaultUser;
            Password = DefaultPassword;
        }

        public const int DefaultHttpPort = 8080; // this port can be changed through kodi's gui
        public const int DefaultTcpPort = 9090; // this port can only be changed in kodi's advancedsettings.xml https://kodi.wiki/view/Advancedsettings.xml#jsonrpc
        public const string DefaultUser = "kodi";
        public const string DefaultPassword = "";
        public const string DefaultIp = "localhost";
    }
}

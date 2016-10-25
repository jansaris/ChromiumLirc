using System.Configuration;

namespace Lirc2Chromium
{
    public class Configuration
    {
        public string LircEndpoint => ConfigurationManager.AppSettings["LircEndpoint"];
        public string ProcessName => ConfigurationManager.AppSettings["ProcessName"];
        public string KeyMapFile => ConfigurationManager.AppSettings["KeyMapFile"];
        public string XdoTool => ConfigurationManager.AppSettings["XdoTool"];
    }
}
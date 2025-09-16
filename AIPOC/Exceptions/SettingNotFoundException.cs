using System;
using Serilog;

namespace AIPOC.Exceptions
{
    public class SettingNotFoundException : Exception
    {
        public SettingNotFoundException(ILogger logger, string settingName)
            : base($"Setting '{settingName}' not found.")
        {
            logger?.Error($"Setting '{settingName}' not found.");
        }
    }
}

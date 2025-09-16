using System;
using Serilog;

namespace AIPOC.Exceptions
{
    public class SettingWrongTypeException : Exception
    {
        public SettingWrongTypeException(ILogger logger, string settingName, string expectedType)
            : base($"Setting '{settingName}' is not of type '{expectedType}'.")
        {
            logger?.Error($"Setting '{settingName}' is not of type '{expectedType}'.");
        }
    }
}

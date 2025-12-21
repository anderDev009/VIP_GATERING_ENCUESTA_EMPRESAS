using System;
using System.Linq;

namespace VIP_GATERING.Infrastructure.Identity;

public static class IdentityDefaults
{
    public const string EnvPasswordKey = "IDENTITY_DEFAULT_PASSWORD";
    public const string FallbackPassword = "Dev123$!Dev123$!Dev1";

    public static string GetDefaultPassword()
    {
        var envPwd = Environment.GetEnvironmentVariable(EnvPasswordKey);
        if (!string.IsNullOrWhiteSpace(envPwd) && MeetsPolicy(envPwd))
        {
            return envPwd;
        }
        return FallbackPassword;
    }

    private static bool MeetsPolicy(string pwd)
    {
        if (pwd.Length < 20) return false;
        var hasUpper = pwd.Any(char.IsUpper);
        var hasLower = pwd.Any(char.IsLower);
        var hasDigit = pwd.Any(char.IsDigit);
        var hasSymbol = pwd.Any(ch => !char.IsLetterOrDigit(ch));
        return hasUpper && hasLower && hasDigit && hasSymbol;
    }
}

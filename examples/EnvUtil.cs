using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClaudeCodeSdk.Examples;

internal static class EnvUtil
{
    internal static Dictionary<string, string?> CreateEnv()
    {
        //return new Dictionary<string, string?>()
        //{
        //     {"ANTHROPIC_AUTH_TOKEN", "" },
        //    {"ANTHROPIC_BASE_URL", "" },
        //};

        return new Dictionary<string, string?>();
    }
}
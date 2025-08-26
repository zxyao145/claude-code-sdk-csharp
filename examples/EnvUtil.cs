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
        //    {"ANTHROPIC_AUTH_TOKEN", "sk-xxx" },
        //    {"ANTHROPIC_BASE_URL", "xxx" },
        //};

        return new Dictionary<string, string?>();
    }
}

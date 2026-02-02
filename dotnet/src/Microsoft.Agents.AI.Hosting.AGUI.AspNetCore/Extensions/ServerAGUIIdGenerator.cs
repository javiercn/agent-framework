// Copyright (c) Microsoft. All rights reserved.

using System.Security.Cryptography;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.Extensions;

internal static class ServerAGUIIdGenerator
{
    private const int DefaultEntropyLength = 24;
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    internal static string NewId(string? prefix = "id")
    {
        var entropy = GetRandomString(DefaultEntropyLength);
        return string.IsNullOrEmpty(prefix) ? entropy : $"{prefix}_{entropy}";
    }

    internal static string NewReasoningSessionId() => NewId("reasoning");

    internal static string NewReasoningMessageId() => NewId("reasoning_msg");

    internal static string NewInterruptId() => NewId("interrupt");

    private static string GetRandomString(int length)
    {
#if NET8_0_OR_GREATER
        return RandomNumberGenerator.GetString(Chars, length);
#else
        var bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = Chars[bytes[i] % Chars.Length];
        }

        return new string(result);
#endif
    }
}

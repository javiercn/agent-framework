// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.Extensions;
using Xunit;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.UnitTests;

public class ServerAGUIIdGeneratorTests
{
    [Fact]
    public void NewId_WithPrefix_ReturnsFormattedId()
    {
        var id = ServerAGUIIdGenerator.NewId("test");

        id.Should().StartWith("test_");
        id.Should().HaveLength("test_".Length + 24);
    }

    [Fact]
    public void NewId_WithNullPrefix_ReturnsOnlyEntropy()
    {
        var id = ServerAGUIIdGenerator.NewId(null);

        id.Should().HaveLength(24);
        id.Should().NotContain("_");
    }

    [Fact]
    public void NewId_WithEmptyPrefix_ReturnsOnlyEntropy()
    {
        var id = ServerAGUIIdGenerator.NewId(string.Empty);

        id.Should().HaveLength(24);
        id.Should().NotContain("_");
    }

    [Fact]
    public void NewId_GeneratesUniqueIds()
    {
        var ids = new HashSet<string>();

        for (int i = 0; i < 1000; i++)
        {
            ids.Add(ServerAGUIIdGenerator.NewId("test"));
        }

        ids.Should().HaveCount(1000);
    }

    [Fact]
    public void NewReasoningSessionId_ReturnsIdWithReasoningPrefix()
    {
        var id = ServerAGUIIdGenerator.NewReasoningSessionId();

        id.Should().StartWith("reasoning_");
        id.Should().HaveLength("reasoning_".Length + 24);
    }

    [Fact]
    public void NewReasoningMessageId_ReturnsIdWithReasoningMsgPrefix()
    {
        var id = ServerAGUIIdGenerator.NewReasoningMessageId();

        id.Should().StartWith("reasoning_msg_");
        id.Should().HaveLength("reasoning_msg_".Length + 24);
    }

    [Fact]
    public void NewInterruptId_ReturnsIdWithInterruptPrefix()
    {
        var id = ServerAGUIIdGenerator.NewInterruptId();

        id.Should().StartWith("interrupt_");
        id.Should().HaveLength("interrupt_".Length + 24);
    }

    [Fact]
    public void NewId_ContainsOnlyAlphanumericCharactersInEntropy()
    {
        var id = ServerAGUIIdGenerator.NewId(null);

        id.Should().MatchRegex("^[A-Za-z0-9]+$");
    }

    [Fact]
    public void NewId_WithPrefix_ContainsOnlyAlphanumericCharactersAndUnderscore()
    {
        var id = ServerAGUIIdGenerator.NewId("test");

        id.Should().MatchRegex("^[A-Za-z0-9_]+$");
    }
}

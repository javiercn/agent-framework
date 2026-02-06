// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Agents.AI.AGUI.Extensions;
using Xunit;

namespace Microsoft.Agents.AI.AGUI.UnitTests;

public class ClientAGUIIdGeneratorTests
{
    [Fact]
    public void NewId_WithPrefix_ReturnsFormattedId()
    {
        var id = ClientAGUIIdGenerator.NewId("test");

        id.Should().StartWith("test_");
        id.Should().HaveLength("test_".Length + 24);
    }

    [Fact]
    public void NewId_WithNullPrefix_ReturnsOnlyEntropy()
    {
        var id = ClientAGUIIdGenerator.NewId(null);

        id.Should().HaveLength(24);
        id.Should().NotContain("_");
    }

    [Fact]
    public void NewId_WithEmptyPrefix_ReturnsOnlyEntropy()
    {
        var id = ClientAGUIIdGenerator.NewId(string.Empty);

        id.Should().HaveLength(24);
        id.Should().NotContain("_");
    }

    [Fact]
    public void NewId_GeneratesUniqueIds()
    {
        var ids = new HashSet<string>();

        for (int i = 0; i < 1000; i++)
        {
            ids.Add(ClientAGUIIdGenerator.NewId("test"));
        }

        ids.Should().HaveCount(1000);
    }

    [Fact]
    public void NewMessageId_ReturnsIdWithMsgPrefix()
    {
        var id = ClientAGUIIdGenerator.NewMessageId();

        id.Should().StartWith("msg_");
        id.Should().HaveLength("msg_".Length + 24);
    }

    [Fact]
    public void NewToolCallId_ReturnsIdWithCallPrefix()
    {
        var id = ClientAGUIIdGenerator.NewToolCallId();

        id.Should().StartWith("call_");
        id.Should().HaveLength("call_".Length + 24);
    }

    [Fact]
    public void NewFunctionOutputId_ReturnsIdWithResultPrefix()
    {
        var id = ClientAGUIIdGenerator.NewFunctionOutputId();

        id.Should().StartWith("result_");
        id.Should().HaveLength("result_".Length + 24);
    }

    [Fact]
    public void NewId_ContainsOnlyAlphanumericCharactersInEntropy()
    {
        var id = ClientAGUIIdGenerator.NewId(null);

        id.Should().MatchRegex("^[A-Za-z0-9]+$");
    }

    [Fact]
    public void NewId_WithPrefix_ContainsOnlyAlphanumericCharactersAndUnderscore()
    {
        var id = ClientAGUIIdGenerator.NewId("test");

        id.Should().MatchRegex("^[A-Za-z0-9_]+$");
    }
}

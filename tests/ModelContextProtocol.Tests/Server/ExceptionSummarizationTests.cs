using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.ExceptionSummarization;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ModelContextProtocol.Tests.Utils;
using System.ComponentModel;

namespace ModelContextProtocol.Tests.Server;

public class ExceptionSummarizationWithoutSummarizerTests : ClientServerTestBase
{
    public ExceptionSummarizationWithoutSummarizerTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    protected override void ConfigureServices(ServiceCollection services, IMcpServerBuilder mcpServerBuilder)
    {
        mcpServerBuilder.WithCallToolHandler((request, cancellationToken) =>
        {
            throw new McpProtocolException("Tool not available", McpErrorCode.InvalidParams);
        });
    }

    [Fact]
    public async Task RequestHandlerException_WithoutSummarizer_LogsRawException()
    {
        await using var client = await CreateMcpClientForServer();

        // Act
        await Assert.ThrowsAnyAsync<McpException>(
            async () => await client.CallToolAsync("any_tool", cancellationToken: TestContext.Current.CancellationToken));

        // Assert: raw exception was passed to logger (Exception field is non-null)
        var warningLogs = MockLoggerProvider.LogMessages
            .Where(m => m.LogLevel == LogLevel.Warning && m.Message.Contains("request handler failed"))
            .ToList();

        Assert.NotEmpty(warningLogs);
        Assert.Contains(warningLogs, m => m.Exception is not null);
    }
}

public class ExceptionSummarizationWithSummarizerTests : ClientServerTestBase
{
    public ExceptionSummarizationWithSummarizerTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    protected override void ConfigureServices(ServiceCollection services, IMcpServerBuilder mcpServerBuilder)
    {
        services.AddSingleton<IExceptionSummarizer>(new TestExceptionSummarizer());
        mcpServerBuilder.WithCallToolHandler((request, cancellationToken) =>
        {
            throw new McpProtocolException("Tool not available", McpErrorCode.InvalidParams);
        });
    }

    [Fact]
    public async Task RequestHandlerException_WithSummarizer_LogsSummaryNotRawException()
    {
        await using var client = await CreateMcpClientForServer();

        // Act
        await Assert.ThrowsAnyAsync<McpException>(
            async () => await client.CallToolAsync("any_tool", cancellationToken: TestContext.Current.CancellationToken));

        // Assert: summarized parts are in log message, raw exception is not passed
        var warningLogs = MockLoggerProvider.LogMessages
            .Where(m => m.LogLevel == LogLevel.Warning && m.Message.Contains("request handler failed"))
            .ToList();

        Assert.NotEmpty(warningLogs);
        // With summarizer, the log should contain all ExceptionSummary parts and no raw exception
        Assert.Contains(warningLogs, m => m.Exception is null
            && m.Message.Contains("ExceptionType: McpProtocolException")
            && m.Message.Contains("Description: Test summary for McpProtocolException")
            && m.Message.Contains("AdditionalDetails:")
            && m.Message.Contains("StackTrace:"));
    }

    [Fact]
    public async Task RequestHandlerException_WithSummarizer_ClientReceivesStandardError()
    {
        await using var client = await CreateMcpClientForServer();

        // Act: client should still get standard MCP error
        var ex = await Assert.ThrowsAnyAsync<McpException>(
            async () => await client.CallToolAsync("any_tool", cancellationToken: TestContext.Current.CancellationToken));

        // Assert: protocol error preserves the original exception message (not the summary)
        Assert.Contains("Tool not available", ex.Message);
    }
}

public sealed class TestExceptionSummarizer : IExceptionSummarizer
{
    public ExceptionSummary Summarize(Exception exception)
    {
        return new ExceptionSummary(
            exception.GetType().Name,
            $"Test summary for {exception.GetType().Name}",
            additionalDetails: string.Empty);
    }
}

public class ToolCallErrorWithSummarizerTests : ClientServerTestBase
{
    public ToolCallErrorWithSummarizerTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    protected override void ConfigureServices(ServiceCollection services, IMcpServerBuilder mcpServerBuilder)
    {
        services.AddSingleton<IExceptionSummarizer>(new TestExceptionSummarizer());
        mcpServerBuilder.WithCallToolHandler((request, cancellationToken) =>
        {
            // Non-MCP exception: caught by McpServerImpl, logged via ToolCallError, converted to error result.
            throw new InvalidOperationException("Something went wrong internally");
        });
    }

    [Fact]
    public async Task ToolCallError_WithSummarizer_LogsSummaryNotRawException()
    {
        await using var client = await CreateMcpClientForServer();

        // Act: non-MCP exception is converted to CallToolResult with IsError=true
        var result = await client.CallToolAsync("any_tool", cancellationToken: TestContext.Current.CancellationToken);

        // Assert: ToolCallError logged with summary, not raw exception
        var errorLogs = MockLoggerProvider.LogMessages
            .Where(m => m.LogLevel == LogLevel.Error && m.Message.Contains("threw an unhandled exception"))
            .ToList();

        Assert.NotEmpty(errorLogs);
        Assert.Contains(errorLogs, m => m.Exception is null
            && m.Message.Contains("ExceptionType: InvalidOperationException")
            && m.Message.Contains("Description: Test summary for InvalidOperationException")
            && m.Message.Contains("StackTrace:"));
    }
}

public class ToolCallErrorWithoutSummarizerTests : ClientServerTestBase
{
    public ToolCallErrorWithoutSummarizerTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    protected override void ConfigureServices(ServiceCollection services, IMcpServerBuilder mcpServerBuilder)
    {
        mcpServerBuilder.WithCallToolHandler((request, cancellationToken) =>
        {
            throw new InvalidOperationException("Something went wrong internally");
        });
    }

    [Fact]
    public async Task ToolCallError_WithoutSummarizer_LogsRawException()
    {
        await using var client = await CreateMcpClientForServer();

        // Act
        var result = await client.CallToolAsync("any_tool", cancellationToken: TestContext.Current.CancellationToken);

        // Assert: raw exception was passed to logger
        var errorLogs = MockLoggerProvider.LogMessages
            .Where(m => m.LogLevel == LogLevel.Error && m.Message.Contains("threw an unhandled exception"))
            .ToList();

        Assert.NotEmpty(errorLogs);
        Assert.Contains(errorLogs, m => m.Exception is not null);
    }
}

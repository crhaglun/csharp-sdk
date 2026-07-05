using Microsoft.Extensions.Diagnostics.ExceptionSummarization;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace ModelContextProtocol;

/// <summary>
/// Wires an <see cref="IExceptionSummarizer"/> from DI into <see cref="McpServerOptions"/>
/// when the service has been registered (e.g. via <c>AddExceptionSummarizer</c>).
/// </summary>
internal sealed class McpServerExceptionSummarizationSetup(
    IExceptionSummarizer? exceptionSummarizer = null) : IConfigureOptions<McpServerOptions>
{
    public void Configure(McpServerOptions options)
    {
        options.ExceptionSummarizer ??= exceptionSummarizer;
    }
}

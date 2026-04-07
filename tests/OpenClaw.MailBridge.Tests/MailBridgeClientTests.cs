using System.IO;
using System.Text;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;
using ClientProgram = OpenClaw.MailBridge.Client.Program;

namespace OpenClaw.MailBridge.Tests;

[TestClass]
public class MailBridgeClientTests
{
    [TestMethod]
    public void Client_program_should_prefer_pipe_name_override_over_settings_fallback()
    {
        var pipeName = ClientProgram.ResolvePipeName(
            new Dictionary<string, string> { ["pipe_name"] = "override-pipe" }
        );

        pipeName.Should().Be("override-pipe");
    }

    [TestMethod]
    public async Task Client_program_should_keep_json_on_stdout_and_diagnostics_on_stderr_with_exit_code_mapping()
    {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());

        var exitCode = await ClientProgram.RunAsync(
            new[] { "status" },
            (_, req) =>
                Task.FromResult(
                    RpcResponse.Failure(req.Id, BridgeErrorCodes.InvalidRequest, "Bad request")
                ),
            stdout,
            stderr
        );

        exitCode.Should().Be(5);
        stdout.ToString().Should().Contain("\"ok\":false");
        stdout.ToString().Should().Contain(BridgeErrorCodes.InvalidRequest);
        stderr.ToString().Should().BeEmpty();
    }
}

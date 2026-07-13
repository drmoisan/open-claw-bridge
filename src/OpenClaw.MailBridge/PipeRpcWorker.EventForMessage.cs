using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// RPC dispatch handler for <see cref="BridgeMethods.GetEventForMessage"/> (issue #146). Split into a
/// partial-class file so the handler is added without growing the near-cap <c>PipeRpcWorker.cs</c>.
///
/// Contract: a malformed message bridge id yields <see cref="BridgeErrorCodes.InvalidRequest"/>
/// (the caller maps this to HTTP 400). A decodable id that is unlinked — ordinary mail, an absent
/// message row, or a linked key that matches no event — yields <c>Success(id, null)</c> (mirroring
/// <c>send_mail</c>), never <see cref="BridgeErrorCodes.NotFound"/>, so the deterministic scheduling
/// pipeline degrades to its calendar-view fallback rather than surfacing a spurious 404.
/// </summary>
internal sealed partial class PipeRpcWorker
{
    private async Task<RpcResponse> HandleGetEventForMessageAsync(RpcRequest req)
    {
        var bridgeId = RequireParameter(req, "id");
        if (!BridgeIdCodec.TryDecodeMessageId(bridgeId, out _, out _))
        {
            return RpcResponse.Failure(
                req.Id,
                BridgeErrorCodes.InvalidRequest,
                "The supplied message bridge ID is invalid."
            );
        }

        var evt = await repo.GetEventForMessageAsync(bridgeId);
        return evt is null
            ? RpcResponse.Success(req.Id, null)
            : RpcResponse.Success(req.Id, ResponseShaper.ShapeEvent(evt, settings));
    }
}

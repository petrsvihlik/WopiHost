using CellBridge.AspNetCore;
using CellBridge.Storage.Abstractions;

namespace WopiHost.CellBridge;

/// <summary>
/// Makes the WOPI access token the authority inside cellbridge instead of the per-document grants
/// cellbridge would otherwise store. Every document is bound to this policy and every subject gets
/// read/write; the per-request <see cref="CellBridgeActor.AccessLimit"/> set by
/// <see cref="CellBridgeProcessor"/> is what narrows a request to the file the WOPI route resolved.
/// </summary>
internal sealed class WopiCellBridgeAuthorizationPolicy : ICellBridgeAuthorizationPolicy
{
    private static readonly DocumentAuthorizationBinding s_binding = new("wopi", ContractVersion: 1, Revision: 1);

    public string? PolicyDomain => s_binding.PolicyDomain;

    public DocumentAuthorizationBinding? BindNewDocument(Guid resourceId) => s_binding;

    public ICellBridgeAuthorizationSnapshot? Resolve(DocumentState state) => new Snapshot(state.ResourceId);

    private sealed record Snapshot(Guid ResourceId) : ICellBridgeAuthorizationSnapshot
    {
        public DocumentAuthorizationBinding? Binding => s_binding;

        public DocumentAccess Evaluate(string subject) => DocumentAccess.Read | DocumentAccess.Write;
    }
}

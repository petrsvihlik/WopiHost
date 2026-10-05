using CellBridge.AspNetCore;
using CellBridge.Storage.Abstractions;

namespace WopiHost.CellBridge;

/// <summary>
/// Grants every actor read/write access inside cellbridge. Authorization already happened at the
/// WOPI layer: the <c>COBALT</c> endpoint requires the access token's update permission before the
/// processor runs, so cellbridge's own per-document grants would only duplicate (or contradict) it.
/// </summary>
internal sealed class WopiCellBridgeAccessEvaluator : ICellBridgeAccessEvaluator
{
    public DocumentAccess Evaluate(CellBridgeActor actor, DocumentState state) => DocumentAccess.Read | DocumentAccess.Write;
}

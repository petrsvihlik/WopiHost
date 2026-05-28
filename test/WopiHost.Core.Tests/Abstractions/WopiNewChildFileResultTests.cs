using Moq;
using WopiHost.Abstractions;

namespace WopiHost.Core.Tests.Abstractions;

/// <summary>
/// Tests for the <see cref="WopiNewChildFileResult"/> factory shorthands. Each factory must
/// set <see cref="WopiNewChildFileResult.Outcome"/> to the matching enum value and populate
/// only the outcome-relevant payload field; the others stay null.
/// </summary>
public class WopiNewChildFileResultTests
{
    [Fact]
    public void Success_SetsOutcomeAndFile()
    {
        var file = Mock.Of<IWopiWritableFile>();

        var result = WopiNewChildFileResult.Success(file);

        Assert.Equal(WopiNewChildFileOutcome.Success, result.Outcome);
        Assert.Same(file, result.File);
        Assert.Null(result.ValidRelativeTargetSuggestion);
        Assert.Null(result.ExistingLockId);
    }

    [Fact]
    public void BadRequest_SetsOnlyOutcome()
    {
        var result = WopiNewChildFileResult.BadRequest();

        Assert.Equal(WopiNewChildFileOutcome.BadRequest, result.Outcome);
        Assert.Null(result.File);
        Assert.Null(result.ValidRelativeTargetSuggestion);
        Assert.Null(result.ExistingLockId);
    }

    [Fact]
    public void Conflict_CarriesSuggestion()
    {
        var result = WopiNewChildFileResult.Conflict("Report (1).docx");

        Assert.Equal(WopiNewChildFileOutcome.Conflict, result.Outcome);
        Assert.Equal("Report (1).docx", result.ValidRelativeTargetSuggestion);
        Assert.Null(result.File);
        Assert.Null(result.ExistingLockId);
    }

    [Fact]
    public void Locked_CarriesExistingLockId()
    {
        var result = WopiNewChildFileResult.Locked("lock-A");

        Assert.Equal(WopiNewChildFileOutcome.Locked, result.Outcome);
        Assert.Equal("lock-A", result.ExistingLockId);
        Assert.Null(result.File);
        Assert.Null(result.ValidRelativeTargetSuggestion);
    }

    [Fact]
    public void InternalError_SetsOnlyOutcome()
    {
        var result = WopiNewChildFileResult.InternalError();

        Assert.Equal(WopiNewChildFileOutcome.InternalError, result.Outcome);
        Assert.Null(result.File);
        Assert.Null(result.ValidRelativeTargetSuggestion);
        Assert.Null(result.ExistingLockId);
    }
}

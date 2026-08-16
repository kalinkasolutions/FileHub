using System.Collections.ObjectModel;
using Shared;

namespace FileHub.IntegrationTests;

public sealed class OperationResultTests
{
    [Fact]
    public void Value_of_successful_value_type_result_returns_the_value()
    {
        var id = Guid.NewGuid();

        var result = OperationResult<Guid>.Success(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value);
    }

    [Fact]
    public void Value_of_failed_value_type_result_throws_instead_of_returning_default()
    {
        // Regression: with a value-type T (Guid), a `m_value ?? throw` guard never fires, because
        // default(Guid) is not null — .Value would silently return Guid.Empty on a failure.
        var result = OperationResult<Guid>.NotFound("nope");

        Assert.True(result.HasError);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Value_of_failed_reference_type_result_throws()
    {
        var result = OperationResult<string>.BadRequest("bad");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Value_of_successful_reference_type_result_returns_the_value()
    {
        var result = OperationResult<string>.Success("hello");

        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Success_without_a_payload_is_not_an_error()
    {
        var result = OperationResult<Empty>.Success();

        Assert.True(result.IsSuccess);
        Assert.Equal(ResultCode.Success, result.ResultCode);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    [Theory]
    [InlineData(ResultCode.NotFound)]
    [InlineData(ResultCode.Forbidden)]
    [InlineData(ResultCode.BadGateway)]
    [InlineData(ResultCode.BadRequest)]
    [InlineData(ResultCode.Error)]
    public void MapError_keeps_the_result_code_and_the_message(ResultCode code)
    {
        var mapped = Failure<string>(code, "went wrong").MapError<int>();

        Assert.Equal(code, mapped.ResultCode);
        Assert.Equal("went wrong", mapped.ErrorMessage);
    }

    [Fact]
    public void MapError_keeps_the_validation_errors()
    {
        var errors = new ReadOnlyDictionary<string, string[]>(
            new Dictionary<string, string[]> { ["Path"] = ["too long"] });

        var mapped = OperationResult<string>.Validation(errors).MapError<int>();

        Assert.Equal(ResultCode.Validation, mapped.ResultCode);
        Assert.Equal(["too long"], mapped.ValidationErrors["Path"]);
    }

    [Fact]
    public void MapError_on_a_successful_result_throws()
    {
        Assert.Throws<InvalidOperationException>(() => OperationResult<string>.Success("fine").MapError<int>());
    }

    [Fact]
    public void A_successful_result_carries_no_validation_errors()
    {
        Assert.Empty(OperationResult<string>.Success("fine").ValidationErrors);
    }

    [Fact]
    public void Success_refuses_a_null_value()
    {
        Assert.Throws<ArgumentNullException>(() => OperationResult<string>.Success(null!));
    }

    [Theory]
    [InlineData(ResultCode.Success, false)]
    [InlineData(ResultCode.NotFound, true)]
    [InlineData(ResultCode.Forbidden, true)]
    [InlineData(ResultCode.BadGateway, true)]
    [InlineData(ResultCode.BadRequest, true)]
    [InlineData(ResultCode.Validation, true)]
    [InlineData(ResultCode.Error, true)]
    public void Anything_that_is_not_an_explicit_success_is_an_error(ResultCode code, bool isError)
    {
        // A code added to the enum later is a failure by default rather than silently a success.
        Assert.Equal(isError, code.IsError());
    }

    private static OperationResult<T> Failure<T>(ResultCode code, string message) => code switch
    {
        ResultCode.NotFound => OperationResult<T>.NotFound(message),
        ResultCode.Forbidden => OperationResult<T>.Forbidden(message),
        ResultCode.BadGateway => OperationResult<T>.BadGateway(message),
        ResultCode.BadRequest => OperationResult<T>.BadRequest(message),
        _ => OperationResult<T>.Error(message)
    };
}

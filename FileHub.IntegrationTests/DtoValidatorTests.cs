using Dtos.Account;
using Dtos.BasePaths;
using Dtos.Shares;
using FileHub.BusinessLogic.Validation;
using Shared;

namespace FileHub.IntegrationTests;

public sealed class DtoValidatorTests
{
    [Fact]
    public void Validate_returns_success_for_a_valid_dto()
    {
        var result = DtoValidator.Validate(new SaveBasePathDto { Path = "/srv/media", Name = "Media" });

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_flags_a_missing_required_field(string path)
    {
        var result = DtoValidator.Validate(new SaveBasePathDto { Path = path, Name = "Media" });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(SaveBasePathDto.Path), result.ValidationErrors.Keys);
    }

    [Fact]
    public void Validate_flags_a_value_over_the_maximum_length()
    {
        var result = DtoValidator.Validate(new SaveBasePathDto { Path = "/srv/media", Name = new string('x', 201) });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(SaveBasePathDto.Name), result.ValidationErrors.Keys);
    }

    [Fact]
    public void Validate_flags_a_value_outside_its_range()
    {
        var result = DtoValidator.Validate(new CreateShareDto { RelativePath = "a.txt", MaxDownloadCount = -1 });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(CreateShareDto.MaxDownloadCount), result.ValidationErrors.Keys);
    }

    [Fact]
    public void Validate_flags_a_field_that_has_to_match_another()
    {
        var result = DtoValidator.Validate(new ChangePasswordDto
        {
            CurrentPassword = "current-password",
            NewPassword = "brand-new-password",
            ConfirmPassword = "something-else"
        });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Equal(["The new passwords do not match."], result.ValidationErrors[nameof(ChangePasswordDto.ConfirmPassword)]);
    }

    [Fact]
    public void Validate_reports_every_failing_field_at_once()
    {
        var result = DtoValidator.Validate(new ChangeEmailDto { Email = "not-an-address", CurrentPassword = string.Empty });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(ChangeEmailDto.Email), result.ValidationErrors.Keys);
        Assert.Contains(nameof(ChangeEmailDto.CurrentPassword), result.ValidationErrors.Keys);
    }

    [Fact]
    public void Validate_refuses_a_null_dto()
    {
        Assert.Throws<ArgumentNullException>(() => DtoValidator.Validate<SaveBasePathDto>(null!));
    }
}

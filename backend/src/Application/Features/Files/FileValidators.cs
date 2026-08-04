using FluentValidation;

namespace ThucLuc.Application.Features.Files;

public sealed class UploadFileRequestValidator : AbstractValidator<UploadFileRequest>
{
    public UploadFileRequestValidator()
    {
        RuleFor(x => x.DonViId)
            .GreaterThan(0)
            .WithMessage("DonViId là bắt buộc.");

        RuleFor(x => x.EntityType)
            .NotEmpty()
            .WithMessage("EntityType là bắt buộc.");

        RuleFor(x => x.EntityId)
            .GreaterThan(0)
            .WithMessage("EntityId là bắt buộc.");

        RuleFor(x => x.KyCode)
            .NotEmpty()
            .WithMessage("KyCode là bắt buộc.");
    }
}

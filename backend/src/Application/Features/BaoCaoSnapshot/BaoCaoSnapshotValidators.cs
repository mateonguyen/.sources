using FluentValidation;

namespace ThucLuc.Application.Features.BaoCaoSnapshot;

public sealed class CreateBaoCaoSnapshotRequestValidator : AbstractValidator<CreateBaoCaoSnapshotRequest>
{
    public CreateBaoCaoSnapshotRequestValidator()
    {
        RuleFor(x => x.KyBaoCaoId).GreaterThan(0);
        RuleFor(x => x.DonViId).GreaterThan(0);
        RuleFor(x => x.GhiChu).MaximumLength(2000);
    }
}

public sealed class UpdateBaoCaoSnapshotRequestValidator : AbstractValidator<UpdateBaoCaoSnapshotRequest>
{
    public UpdateBaoCaoSnapshotRequestValidator()
    {
        RuleFor(x => x.GhiChu).MaximumLength(2000);
    }
}

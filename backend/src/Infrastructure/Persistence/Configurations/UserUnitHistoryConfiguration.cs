using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.Identity;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class UserUnitHistoryConfiguration : IEntityTypeConfiguration<UserUnitHistory>
{
    public void Configure(EntityTypeBuilder<UserUnitHistory> builder)
    {
        builder.ToTable("IDM_USER_UNIT_HISTORY");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.TransferredAt }).HasDatabaseName("IX_UUH_USER_TIME");
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Api.IntegrationTests.Infrastructure;
using ThucLuc.Domain.Entities.Identity;
using ThucLuc.Domain.Entities.Reporting;
using ThucLuc.Domain.Entities.System;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Api.IntegrationTests;

public sealed class DonViChangeIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;
    public DonViChangeIntegrationTests(ApiTestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task TransferUsers_ReplacesRoles_KeepsHistory_AndInvalidatesOldToken()
    {
        await _factory.ResetDataAsync();
        using var admin = await _factory.CreateAuthorizedClientAsync("admin", "Test@123");
        using var oldClient = await _factory.CreateAuthorizedClientAsync("donvi.user", "Test@123");
        var ids = await _factory.ExecuteDbContextAsync(async db =>
        {
            var user = await db.Users.FirstAsync(x => x.UserName == "donvi.user");
            var target = await db.DonVis.FirstAsync(x => x.MaDonVi == "CA_DN");
            return (UserId: user.Id, SourceId: user.DonViId, TargetId: target.Id);
        });
        var payload = new { items = new[] { new { userId = ids.UserId, sourceDonViId = ids.SourceId, targetDonViId = ids.TargetId, roleIds = new[] { 4003L } } } };
        var preview = await admin.PostAsJsonAsync("/api/v1/users/transfers/preview", payload);
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await admin.PostAsJsonAsync("/api/v1/users/transfers", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await oldClient.GetAsync("/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.ExecuteDbContextAsync(async db =>
        {
            var user = await db.Users.FirstAsync(x => x.Id == ids.UserId);
            user.DonViId.Should().Be(ids.TargetId);
            (await db.UserRoleAssignments.Where(x => x.UserId == ids.UserId).Select(x => x.DonViId).Distinct().ToListAsync())
                .Should().Equal(ids.TargetId);
            (await db.UserUnitHistories.CountAsync(x => x.UserId == ids.UserId)).Should().Be(1);
        });
    }

    [Fact]
    public async Task Merge_PreservesSubmittedSnapshot_AndMovesTreeAndAccount()
    {
        await _factory.ResetDataAsync();
        var ids = await _factory.ExecuteDbContextAsync(async db =>
        {
            var source = new DonVi { MaDonVi = "MERGE_SOURCE", TenDonVi = "Nguồn", CapDonVi = "CAP_1", CapSo = 1, IsActive = true };
            var target = new DonVi { MaDonVi = "MERGE_TARGET", TenDonVi = "Đích", CapDonVi = "CAP_1", CapSo = 1, IsActive = true };
            db.DonVis.AddRange(source, target);
            await db.SaveChangesAsync();
            var child = new DonVi { MaDonVi = "MERGE_CHILD", TenDonVi = "Con", ParentId = source.Id, CapDonVi = "CAP_2", CapSo = 2, IsActive = true };
            var user = new ApplicationUser { UserName = "merge.user", NormalizedUserName = "MERGE.USER", HoTen = "Merge User", DonViId = source.Id, IsActive = true };
            db.DonVis.Add(child);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.UserRoleAssignments.Add(new UserRoleAssignment { UserId = user.Id, RoleId = 4003, DonViId = source.Id });
            db.BaoCaoSnapshots.Add(new BaoCaoSnapshot { KyBaoCaoId = 6001, DonViId = source.Id, TrangThai = SnapshotStatus.Submitted });
            await db.SaveChangesAsync();
            return (SourceId: source.Id, TargetId: target.Id, ChildId: child.Id, UserId: user.Id);
        });
        using var admin = await _factory.CreateAuthorizedClientAsync("admin", "Test@123");
        var payload = new { sourceIds = new[] { ids.SourceId }, targetId = ids.TargetId, note = "Quyết định thử nghiệm" };
        var preview = await admin.PostAsJsonAsync("/api/v1/don-vi/changes/merge/preview", payload);
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await admin.PostAsJsonAsync("/api/v1/don-vi/changes/merge", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await _factory.ExecuteDbContextAsync(async db =>
        {
            (await db.DonVis.FirstAsync(x => x.Id == ids.SourceId)).IsActive.Should().BeFalse();
            (await db.DonVis.FirstAsync(x => x.Id == ids.ChildId)).ParentId.Should().Be(ids.TargetId);
            (await db.Users.FirstAsync(x => x.Id == ids.UserId)).DonViId.Should().Be(ids.TargetId);
            (await db.BaoCaoSnapshots.FirstAsync(x => x.DonViId == ids.SourceId)).DonViId.Should().Be(ids.SourceId);
            (await db.DonViSuccessors.CountAsync(x => x.SourceDvId == ids.SourceId && x.TargetDvId == ids.TargetId)).Should().Be(1);
        });
    }

    [Fact]
    public async Task Successor_CanReadOnlyReportsSubmittedBeforeMerge()
    {
        await _factory.ResetDataAsync();
        var ids = await _factory.ExecuteDbContextAsync(async db =>
        {
            var source = new DonVi { MaDonVi = "HISTORY_SOURCE", TenDonVi = "Đơn vị cũ", CapDonVi = "CAP_1", CapSo = 1, IsActive = true };
            var target = new DonVi { MaDonVi = "HISTORY_TARGET", TenDonVi = "Đơn vị mới", CapDonVi = "CAP_1", CapSo = 1, IsActive = true };
            db.DonVis.AddRange(source, target);
            await db.SaveChangesAsync();
            var user = await db.Users.FirstAsync(x => x.UserName == "viewer.user");
            var assignments = await db.UserRoleAssignments.Where(x => x.UserId == user.Id).ToListAsync();
            var roleIds = assignments.Select(x => x.RoleId).ToArray();
            db.UserRoleAssignments.RemoveRange(assignments);
            await db.SaveChangesAsync();
            user.DonViId = source.Id;
            foreach (var roleId in roleIds)
                db.UserRoleAssignments.Add(new UserRoleAssignment { UserId = user.Id, RoleId = roleId, DonViId = source.Id });
            var oldReport = new BaoCaoSnapshot
            {
                KyBaoCaoId = 6001, DonViId = source.Id, TrangThai = SnapshotStatus.Submitted,
                SubmittedAt = DateTime.UtcNow.AddDays(-1)
            };
            db.BaoCaoSnapshots.Add(oldReport);
            db.DonViVersions.Add(new DonViVersion
            {
                DonViId = source.Id, MaDonVi = source.MaDonVi, TenDonVi = source.TenDonVi,
                CapDonVi = source.CapDonVi, CheDoNhapLieu = source.CheDoNhapLieu,
                IsActive = true, ValidFrom = DateTime.UtcNow.AddDays(-2)
            });
            await db.SaveChangesAsync();
            return (Source: source.Id, Target: target.Id, Report: oldReport.Id);
        });
        using var admin = await _factory.CreateAuthorizedClientAsync("admin", "Test@123");
        var merge = await admin.PostAsJsonAsync("/api/v1/don-vi/changes/merge", new { sourceIds = new[] { ids.Source }, targetId = ids.Target });
        merge.StatusCode.Should().Be(HttpStatusCode.OK, await merge.Content.ReadAsStringAsync());
        using var successor = await _factory.CreateAuthorizedClientAsync("viewer.user", "Test@123");
        var history = await successor.GetAsync("/api/v1/snapshot/my-history");
        history.StatusCode.Should().Be(HttpStatusCode.OK, await history.Content.ReadAsStringAsync());
        using (var document = JsonDocument.Parse(await history.Content.ReadAsStringAsync()))
            document.RootElement.GetProperty("data").EnumerateArray()
                .Any(x => x.GetProperty("id").GetInt64() == ids.Report
                    && x.GetProperty("tenDonVi").GetString() == "Đơn vị cũ").Should().BeTrue();

        var laterId = await _factory.ExecuteDbContextAsync(async db =>
        {
            var later = new BaoCaoSnapshot
            {
                KyBaoCaoId = 6001, DonViId = ids.Source, TrangThai = SnapshotStatus.Submitted,
                SubmittedAt = DateTime.Now.AddMinutes(5)
            };
            db.BaoCaoSnapshots.Add(later);
            await db.SaveChangesAsync();
            return later.Id;
        });
        var laterResponse = await successor.GetAsync($"/api/v1/snapshot/{laterId}");
        laterResponse.StatusCode.Should().Be(HttpStatusCode.NotFound, await laterResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Split_RequiresDraftResolution_AndMovesExplicitAllocations()
    {
        await _factory.ResetDataAsync();
        var ids = await _factory.ExecuteDbContextAsync(async db =>
        {
            var source = new DonVi { MaDonVi = "SPLIT_SOURCE", TenDonVi = "Nguồn", CapDonVi = "CAP_1", CapSo = 1, IsActive = true };
            var target = new DonVi { MaDonVi = "SPLIT_TARGET", TenDonVi = "Đích", CapDonVi = "CAP_1", CapSo = 1, IsActive = true };
            db.DonVis.AddRange(source, target);
            await db.SaveChangesAsync();
            var child = new DonVi { MaDonVi = "SPLIT_CHILD", TenDonVi = "Con", ParentId = source.Id, CapDonVi = "CAP_2", CapSo = 2, IsActive = true };
            db.DonVis.Add(child);
            var user = await db.Users.FirstAsync(x => x.UserName == "donvi.user");
            var assignments = await db.UserRoleAssignments.Where(x => x.UserId == user.Id).ToListAsync();
            var roleIds = assignments.Select(x => x.RoleId).ToArray();
            db.UserRoleAssignments.RemoveRange(assignments);
            await db.SaveChangesAsync();
            user.DonViId = source.Id;
            foreach (var roleId in roleIds)
                db.UserRoleAssignments.Add(new UserRoleAssignment { UserId = user.Id, RoleId = roleId, DonViId = source.Id });
            var draft = new BaoCaoSnapshot { KyBaoCaoId = 6001, DonViId = source.Id, TrangThai = SnapshotStatus.Draft };
            db.BaoCaoSnapshots.Add(draft);
            await db.SaveChangesAsync();
            return (Source: source.Id, Target: target.Id, Child: child.Id, User: user.Id, Draft: draft.Id);
        });
        using var admin = await _factory.CreateAuthorizedClientAsync("admin", "Test@123");
        var payload = new
        {
            sourceId = ids.Source,
            children = new[] { new { childId = ids.Child, targetId = ids.Target } },
            users = new[] { new { userId = ids.User, targetId = ids.Target, roleIds = new[] { 4003L } } },
            deactivateSource = true
        };
        var blocked = await admin.PostAsJsonAsync("/api/v1/don-vi/changes/split", payload);
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.ExecuteDbContextAsync(async db =>
        {
            db.BaoCaoSnapshots.Remove(await db.BaoCaoSnapshots.FirstAsync(x => x.Id == ids.Draft));
            await db.SaveChangesAsync();
        });
        var result = await admin.PostAsJsonAsync("/api/v1/don-vi/changes/split", payload);
        result.StatusCode.Should().Be(HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
        await _factory.ExecuteDbContextAsync(async db =>
        {
            (await db.DonVis.FirstAsync(x => x.Id == ids.Source)).IsActive.Should().BeFalse();
            (await db.DonVis.FirstAsync(x => x.Id == ids.Child)).ParentId.Should().Be(ids.Target);
            (await db.Users.FirstAsync(x => x.Id == ids.User)).DonViId.Should().Be(ids.Target);
        });
    }
}

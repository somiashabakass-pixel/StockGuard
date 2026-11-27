using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;
using Xunit;

namespace StockGuard.Tests.Integration;

public sealed class IdentityDatabaseIntegrationTests
{
    private static ApplicationUser CreateUser(string? email = null)
    {
        email ??= $"{Guid.NewGuid():N}@example.test";

        return new ApplicationUser
        {
            FirstName = "Integration",
            LastName = "Test",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            IsActive = true
        };
    }

    private static Organization CreateOrganization()
    {
        return new Organization
        {
            Name = $"Identity Test {Guid.NewGuid():N}",
            IsActive = true
        };
    }

    private static OrganizationMembership CreateMembership(
        Guid userId,
        Guid organizationId,
        OrganizationRole role = OrganizationRole.Viewer)
    {
        return new OrganizationMembership
        {
            UserId = userId,
            OrganizationId = organizationId,
            Role = role,
            IsActive = true
        };
    }

    private static async Task AssertDatabaseErrorAsync(
        StockGuardDbContext context,
        string expectedSqlState,
        string expectedConstraint)
    {
        var exception =
            await Assert.ThrowsAsync<DbUpdateException>(
                async () => await context.SaveChangesAsync());

        var postgresException =
            Assert.IsType<PostgresException>(
                exception.InnerException);

        Assert.Equal(
            expectedSqlState,
            postgresException.SqlState);

        Assert.Equal(
            expectedConstraint,
            postgresException.ConstraintName);
    }

    [Fact]
    public async Task DuplicateNormalizedEmail_IsRejected()
    {
        await using var context = TestDatabase.CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();

        var first = CreateUser();
        var second = CreateUser(first.Email);

        second.UserName = $"{Guid.NewGuid():N}@example.test";
        second.NormalizedUserName =
            second.UserName.ToUpperInvariant();

        context.Users.Add(first);
        await context.SaveChangesAsync();

        context.Users.Add(second);

        await AssertDatabaseErrorAsync(
            context,
            PostgresErrorCodes.UniqueViolation,
            "UX_users_normalized_email");
    }

    [Fact]
    public async Task DuplicateMembership_IsRejected()
    {
        await using var context = TestDatabase.CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();

        var user = CreateUser();
        var organization = CreateOrganization();

        context.Users.Add(user);
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        context.OrganizationMemberships.Add(
            CreateMembership(user.Id, organization.Id));

        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        context.OrganizationMemberships.Add(
            CreateMembership(user.Id, organization.Id));

        await AssertDatabaseErrorAsync(
            context,
            PostgresErrorCodes.UniqueViolation,
            "PK_organization_memberships");
    }

    [Fact]
    public async Task InvalidOrganizationRole_IsRejected()
    {
        await using var context = TestDatabase.CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();

        var user = CreateUser();
        var organization = CreateOrganization();

        context.Users.Add(user);
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        context.OrganizationMemberships.Add(
            CreateMembership(
                user.Id,
                organization.Id,
                (OrganizationRole)999));

        await AssertDatabaseErrorAsync(
            context,
            PostgresErrorCodes.CheckViolation,
            "CK_organization_memberships_role");
    }

    [Fact]
    public async Task NonexistentUser_IsRejected()
    {
        await using var context = TestDatabase.CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();

        var organization = CreateOrganization();

        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        context.OrganizationMemberships.Add(
            CreateMembership(
                Guid.NewGuid(),
                organization.Id));

        await AssertDatabaseErrorAsync(
            context,
            PostgresErrorCodes.ForeignKeyViolation,
            "FK_organization_memberships_users_UserId");
    }

    [Fact]
    public async Task NonexistentOrganization_IsRejected()
    {
        await using var context = TestDatabase.CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();

        var user = CreateUser();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.OrganizationMemberships.Add(
            CreateMembership(
                user.Id,
                Guid.NewGuid()));

        await AssertDatabaseErrorAsync(
            context,
            PostgresErrorCodes.ForeignKeyViolation,
            "FK_organization_memberships_organizations_OrganizationId");
    }

    [Fact]
    public async Task UserWithMembership_CannotBeDeleted()
    {
        await using var context = TestDatabase.CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();

        var user = CreateUser();
        var organization = CreateOrganization();

        context.Users.Add(user);
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        context.OrganizationMemberships.Add(
            CreateMembership(user.Id, organization.Id));

        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        var persistedUser = await context.Users
            .SingleAsync(x => x.Id == user.Id);

        context.Users.Remove(persistedUser);

        await AssertDatabaseErrorAsync(
            context,
            PostgresErrorCodes.RestrictViolation,
            "FK_organization_memberships_users_UserId");
    }

    [Fact]
    public async Task OrganizationWithMembership_CannotBeDeleted()
    {
        await using var context = TestDatabase.CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();

        var user = CreateUser();
        var organization = CreateOrganization();

        context.Users.Add(user);
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        context.OrganizationMemberships.Add(
            CreateMembership(user.Id, organization.Id));

        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        var persistedOrganization = await context.Organizations
            .SingleAsync(x => x.Id == organization.Id);

        context.Organizations.Remove(persistedOrganization);

        await AssertDatabaseErrorAsync(
            context,
            PostgresErrorCodes.RestrictViolation,
            "FK_organization_memberships_organizations_OrganizationId");
    }

    [Fact]
    public async Task UserCanHaveDifferentRolesAcrossOrganizations()
    {
        await using var context = TestDatabase.CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync();

        var user = CreateUser();
        var first = CreateOrganization();
        var second = CreateOrganization();

        context.Users.Add(user);
        context.Organizations.AddRange(first, second);
        await context.SaveChangesAsync();

        context.OrganizationMemberships.AddRange(
            CreateMembership(
                user.Id,
                first.Id,
                OrganizationRole.Owner),
            CreateMembership(
                user.Id,
                second.Id,
                OrganizationRole.Analyst));

        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        var memberships =
            await context.OrganizationMemberships
                .AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .ToListAsync();

        Assert.Equal(2, memberships.Count);

        Assert.Contains(
            memberships,
            x => x.OrganizationId == first.Id &&
                 x.Role == OrganizationRole.Owner);

        Assert.Contains(
            memberships,
            x => x.OrganizationId == second.Id &&
                 x.Role == OrganizationRole.Analyst);
    }
}

using System.Text.Json;
using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Domain.Models;
using HouseholdFinances.Infrastructure.Services;

namespace HouseholdFinances.Tests.Services;

/// <summary>
/// Verifies the User service's get/update behaviour, the current-user resolution, the error
/// convention, and that no password is ever surfaced.
/// </summary>
public class UserServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid HouseholdId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task GetCurrentUserProfileAsync_ReturnsNameEmailAndHouseholdAssociation()
    {
        var household = new Household { Id = HouseholdId, Name = "Brown Household" };
        var (service, _, _) = CreateService(CreateUser(), new[] { household });

        var profile = await service.GetCurrentUserProfileAsync();

        Assert.Equal(UserId, profile.Id);
        Assert.Equal("Alex Doe", profile.Name);
        Assert.Equal("alex@example.com", profile.Email);

        var association = Assert.Single(profile.Households);
        Assert.Equal(HouseholdId, association.Id);
        Assert.Equal("Brown Household", association.Name);
    }

    [Fact]
    public async Task GetCurrentUserProfileAsync_ReturnsEmptyHouseholdListWhenUserHasNoMembership()
    {
        var (service, _, _) = CreateService(CreateUser());

        var profile = await service.GetCurrentUserProfileAsync();

        Assert.NotNull(profile.Households);
        Assert.Empty(profile.Households);
    }

    [Fact]
    public async Task GetCurrentUserProfileAsync_NeverSurfacesThePassword()
    {
        var (service, _, _) = CreateService(CreateUser());

        var profile = await service.GetCurrentUserProfileAsync();

        // The read model has no password member, so it cannot be serialized even though the stored
        // user carries a hash.
        Assert.Null(typeof(UserProfile).GetProperty("Password"));

        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCurrentUserProfileAsync_ThrowsNotFoundWhenTheUserRecordDoesNotExist()
    {
        var (service, _, _) = CreateService(user: null);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.GetCurrentUserProfileAsync());

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(UserId, exception.Identifier);
        Assert.Equal($"NotFound {UserId}", exception.Message);
    }

    [Theory]
    [InlineData(false, "11111111-1111-1111-1111-111111111111")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, "not-a-guid")]
    public async Task GetCurrentUserProfileAsync_ThrowsUnauthorizedWhenTheCurrentUserCannotBeResolved(
        bool isAuthenticated,
        string? identifier)
    {
        var (service, _, currentUser) = CreateService(CreateUser());
        currentUser.IsAuthenticated = isAuthenticated;
        currentUser.UserIdentifier = identifier;

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.GetCurrentUserProfileAsync());

        Assert.Equal(ErrorCode.Unauthorized, exception.ErrorCode);
        Assert.Equal(UserService.CurrentUserIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task UpdateCurrentUserDisplayNameAsync_TrimsPersistsAndReturnsTheUpdatedProfile()
    {
        var user = CreateUser("Old Name");
        var household = new Household { Id = HouseholdId, Name = "Brown Household" };
        var (service, repository, _) = CreateService(user, new[] { household });

        var profile = await service.UpdateCurrentUserDisplayNameAsync("  Alexandria  ");

        Assert.Equal("Alexandria", profile.Name);
        Assert.Equal("Alexandria", user.Name);
        Assert.Equal(1, repository.SaveCount);
        Assert.Equal(UserId, profile.Id);
        Assert.Equal("alex@example.com", profile.Email);
        Assert.Single(profile.Households);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateCurrentUserDisplayNameAsync_RejectsABlankNameUsingTheErrorConvention(string? name)
    {
        var (service, repository, _) = CreateService(CreateUser());

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.UpdateCurrentUserDisplayNameAsync(name));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(UserService.NameIdentifier, exception.Identifier);
        Assert.Equal("InvalidInput Name", exception.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task UpdateCurrentUserDisplayNameAsync_ThrowsNotFoundWhenTheUserRecordDoesNotExist()
    {
        var (service, _, _) = CreateService(user: null);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.UpdateCurrentUserDisplayNameAsync("New Name"));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(UserId, exception.Identifier);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(
            () => new UserService(null!, new StubCurrentUserService()));
        Assert.Throws<ArgumentNullException>(
            () => new UserService(new FakeUserRepository(), null!));
    }

    private static User CreateUser(string name = "Alex Doe") => new()
    {
        Id = UserId,
        Name = name,
        Email = "alex@example.com",
        Password = "unused-password-hash"
    };

    private static (UserService Service, FakeUserRepository Repository, StubCurrentUserService CurrentUser)
        CreateService(User? user, IReadOnlyList<Household>? households = null)
    {
        var repository = new FakeUserRepository
        {
            User = user,
            Households = households?.ToList() ?? new List<Household>()
        };
        var currentUser = new StubCurrentUserService();

        return (new UserService(repository, currentUser), repository, currentUser);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? User { get; set; }

        public List<Household> Households { get; set; } = new();

        public int SaveCount { get; private set; }

        public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(User is not null && User.Id == userId ? User : null);

        public Task<User?> GetByGoogleSubjectAsync(
            string googleSubject,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                User is not null && User.GoogleSubject == googleSubject ? User : null);

        public void Add(User user) => User = user;

        public Task<IReadOnlyList<Household>> GetHouseholdsAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Household>>(Households);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        public bool IsAuthenticated { get; set; } = true;

        public string? UserIdentifier { get; set; } = UserId.ToString();
    }
}

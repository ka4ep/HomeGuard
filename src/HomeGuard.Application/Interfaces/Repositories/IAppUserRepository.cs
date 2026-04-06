using HomeGuard.Domain.Entities;

namespace HomeGuard.Application.Interfaces.Repositories;

public interface IAppUserRepository : IRepository<AppUser>
{
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct = default);

    Task<AppUser?> GetWithCredentialsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Find a passkey credential by its raw credential ID bytes.</summary>
    Task<(AppUser User, PasskeyCredential Credential)?> FindByCredentialIdAsync(
        byte[] credentialId, CancellationToken ct = default);
}

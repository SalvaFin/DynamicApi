using System.Security.Cryptography;
using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;

namespace Dynamic.Fidelity.Application.Services;

public class UserCodeDirectoryService : IUserCodeDirectoryService
{
    private readonly DynamicFidelityDbContext _dbContext;
    private readonly IUserCodeDirectoryRepository _userCodeDirectoryRepository;

    public UserCodeDirectoryService(
        DynamicFidelityDbContext dbContext,
        IUserCodeDirectoryRepository userCodeDirectoryRepository)
    {
        _dbContext = dbContext;
        _userCodeDirectoryRepository = userCodeDirectoryRepository;
    }

    public async Task<string> EnsureUserCodeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        UserCodeDirectoryEntry? existing = await _userCodeDirectoryRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is not null)
        {
            return existing.UserCode;
        }

        DateTime now = DateTime.UtcNow;
        string userCode;
        do
        {
            userCode = GenerateCode();
        }
        while (await _userCodeDirectoryRepository.GetByUserCodeAsync(userCode, cancellationToken) is not null);

        await _userCodeDirectoryRepository.AddAsync(
            new UserCodeDirectoryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserCode = userCode,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return userCode;
    }

    public async Task<string?> GetUserCodeAsync(Guid userId, CancellationToken cancellationToken = default)
        => (await _userCodeDirectoryRepository.GetByUserIdAsync(userId, cancellationToken))?.UserCode;

    public async Task<Guid?> ResolveUserIdAsync(string userCode, CancellationToken cancellationToken = default)
        => (await _userCodeDirectoryRepository.GetByUserCodeAsync(userCode.Trim().ToUpperInvariant(), cancellationToken))?.UserId;

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        char[] codeChars = new char[8];

        for (int index = 0; index < bytes.Length; index++)
        {
            codeChars[index] = chars[bytes[index] % chars.Length];
        }

        return $"USR{new string(codeChars)}";
    }
}

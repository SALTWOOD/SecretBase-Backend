using backend.Database.Entities;

namespace backend.Types.Response;

public readonly record struct UserBriefDto(string Username, int Role);

public readonly record struct AuthResponse(
    string Message,
    User User,
    DateTime? Expires
);

public readonly record struct TokenRenewResponse(
    string Message,
    User User,
    DateTime? Expires
);

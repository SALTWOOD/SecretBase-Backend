using backend.Tables;

namespace backend.Types;

public readonly record struct UserBriefDto(string Username, int Role);

public readonly record struct AuthResponse(
    string Message,
    UserTable User,
    DateTime Expires
);
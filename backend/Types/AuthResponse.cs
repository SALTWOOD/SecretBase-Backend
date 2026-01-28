namespace backend.Types;

public readonly record struct UserBriefDto(string Username, int Role);

public readonly record struct AuthResponse(
    string Message,
    UserBriefDto User,
    DateTime Expires
);
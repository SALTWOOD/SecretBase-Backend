using backend.Database;
using StackExchange.Redis;

namespace backend.Services;

public record BaseServices(
    AppDbContext Database,
    IConnectionMultiplexer Redis,
    SessionService Session
);
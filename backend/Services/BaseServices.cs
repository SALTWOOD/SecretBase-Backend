using backend.Database;
using StackExchange.Redis;

namespace backend.Services;

public record BaseServices(
    Supabase.Client Supa,
    IConnectionMultiplexer Redis
);
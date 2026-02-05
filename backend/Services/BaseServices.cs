using SqlSugar;
using StackExchange.Redis;

namespace backend.Services;

public record BaseServices(
    ISqlSugarClient Database,
    IConnectionMultiplexer Redis,
    SessionService Session,
    SettingService Setting
);
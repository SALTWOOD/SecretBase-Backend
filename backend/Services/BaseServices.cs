using SqlSugar;

namespace backend.Services;

public record BaseServices(
    ISqlSugarClient Db,
    SessionService Session,
    SettingService Setting
);
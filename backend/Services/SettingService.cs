using backend.Tables;
using Microsoft.AspNetCore.SignalR;
using SqlSugar;
using System.Threading.Tasks;

namespace backend.Services;

public class SettingService(ISqlSugarClient db)
{
    public async Task<T?> Get<T>(string key)
    {
        var setting = await db.Queryable<SettingTable>()
            .Where(it => it.Key == key)
            .FirstAsync();
        return setting.GetValue<T>();
    }
}

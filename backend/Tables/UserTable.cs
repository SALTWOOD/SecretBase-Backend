using SqlSugar;
using System.Text.Json.Serialization;

namespace backend.Tables
{
    [SugarTable("users")]
    public class UserTable
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        public required string Username { get; set; }

        public required string Email { get; set; }

        [JsonIgnore]
        public required string PasswordHash { get; set; }

        public UserRole Role { get; set; } = UserRole.User;

        public bool IsBanned { get; set; } = false;

        public DateTime RegisterTime { get; set; } = DateTime.Now;

        [SugarColumn(ColumnDataType = "varchar(max)", IsNullable = true)]
        public string? Avatar { get; set; }

        [JsonIgnore]
        [SugarColumn(IsJson = true)]
        public LastLogin? LastLoginInfo { get; set; }

        [JsonIgnore]
        public string? UsedInviteCode { get; set; }

        [Navigate(NavigateType.OneToMany, nameof(InviteTable.IssuedBy))]
        public List<InviteTable>? MyIssuedInvites { get; set; }
    }

    public class LastLogin
    {
        public DateTime Time { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
}
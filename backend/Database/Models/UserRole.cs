using System.Runtime.Serialization;

namespace backend.Database.Models;

public enum UserRole
{
    [EnumMember(Value = "user")]
    User,
    [EnumMember(Value = "admin")]
    Admin
}
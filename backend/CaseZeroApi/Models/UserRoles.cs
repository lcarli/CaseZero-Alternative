namespace CaseZeroApi.Models;

public static class UserRoles
{
    public const string Player = "PLAYER";
    public const string Admin = "ADMIN";

    public static readonly string[] All = [Player, Admin];
}

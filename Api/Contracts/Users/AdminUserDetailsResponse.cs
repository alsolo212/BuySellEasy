namespace Api.Contracts.Users;

public class AdminUserDetailsResponse : UserProfileResponse
{
    public string[] Roles { get; init; } = [];
}

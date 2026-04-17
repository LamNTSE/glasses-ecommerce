namespace OpticalStore.API.Requests.Users;

public sealed class UserUpdateRequest
{
    public string? Password { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateOnly? Dob { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }
}

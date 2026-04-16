namespace OpticalStore.API.Responses.Auth
{
    public class UserResponse
    {
        public string Id { get; set; } = null!;
        public DateTime? Dob { get; set; }
        public string Email { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string? Phone { get; set; }
        public string? ImageUrl { get; set; }
        public string Status { get; set; } = null!;
    }
}

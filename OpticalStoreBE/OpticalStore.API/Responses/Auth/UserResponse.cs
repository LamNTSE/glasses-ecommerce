namespace OpticalStore.API.Responses.Auth
{
    public class UserResponse
    {
        public long Id { get; set; }
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = null!;
    }
}

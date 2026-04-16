using System;

namespace OpticalStore.API.Requests.Auth
{
    public class RegisterRequest
    {
        public DateTime? Dob { get; set; }
        public string Email { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? Phone { get; set; }
        public string? ImageUrl { get; set; }
    }
}

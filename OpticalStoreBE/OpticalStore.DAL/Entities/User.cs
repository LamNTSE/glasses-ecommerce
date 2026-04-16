using System;

namespace OpticalStore.DAL.Entities
{
    public class User
    {
        public string Id { get; set; } = null!;
        public DateTime? Dob { get; set; }
        public string Email { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? Phone { get; set; }
        public string? ImageUrl { get; set; }
        public string Status { get; set; } = null!;

        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
    }
}

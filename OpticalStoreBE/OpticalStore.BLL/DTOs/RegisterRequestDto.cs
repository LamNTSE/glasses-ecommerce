using System;

namespace OpticalStore.BLL.DTOs
{
    public class RegisterRequestDto
    {
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string Password { get; set; } = null!;
        public string? Address { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }
}

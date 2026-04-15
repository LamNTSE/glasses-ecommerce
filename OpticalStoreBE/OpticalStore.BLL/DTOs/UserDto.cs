namespace OpticalStore.BLL.DTOs
{
    public class UserDto
    {
        public long Id { get; set; }
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = null!;
    }
}

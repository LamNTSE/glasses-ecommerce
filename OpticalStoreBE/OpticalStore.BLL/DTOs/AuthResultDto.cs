namespace OpticalStore.BLL.DTOs
{
    public class AuthResultDto
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public int ExpiresIn { get; set; }
        public UserDto? User { get; set; }
    }
}

using StockControl.Enums;
using StockControl.Models;

namespace StockControl.Dtos
{
    public class UserDto{
        public int ID { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public RoleType? Role { get; set; }
        public bool IsActive { get; set; }

        public UserDto(string username, string password, RoleType role, bool isActive)
        {
            Username = username;
            Password = password;
            Role = role;
            IsActive = isActive;
        }
        public UserDto(){}

        public static UserDto FromModel(User user)
        {
            return new UserDto
            {
                ID = user.ID,
                Username = user.Username,
                Password = user.Password,
                Role = user.Role,
                IsActive = user.IsActive
            };
        }
    }
}
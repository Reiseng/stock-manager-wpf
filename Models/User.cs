using System.Windows.Documents.DocumentStructures;
using StockControl.Enums;

namespace StockControl.Models
{
    public class User{
        public int ID { get; set;}
        public string Username { get; set; }
        public string Password { get; set; }
        public RoleType Role { get; set; }
        public bool IsActive { get; set; }

        public User(string username, string password, RoleType role, bool isActive)
        {
            Username = username;
            Password = password;
            Role = role;
            IsActive = isActive;
        }
        public User(){}
    }
}
using StockControl.Data;
using StockControl.Enums;
using StockControl.Models;
using StockControl.Utils.PasswordHelper;

namespace StockControl.Services
{
    public class UserService
    {
        private readonly UserPersistence _repository;

        public UserService(UserPersistence repository)
        {
            _repository = repository;
        }
        public IReadOnlyList<User> GetUsers(bool includeInactive)
        {
            return _repository.GetAll(includeInactive: includeInactive);
        }
        public UserDto GetUserByID(int id, bool includeInactive)
        {
            if (id <= 0)
                throw new Exception("Id inválida");
            User? user = _repository.GetByID(id, includeInactive: includeInactive);
            if (user == null)
                throw new Exception("El usuario no existe");
            UserDto userDto = UserDto.FromModel(user);
            return userDto;
        }
        public UserDto GetByUsername(string username, bool includeInactive)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("La casilla no debe estar vacia");
            User? user = _repository.GetByUsername(username, includeInactive);
            if (user == null)
                throw new Exception("Usuario no encontrado");
            UserDto userDto = UserDto.FromModel(user);
            return userDto;
        }
        public void AddUser(UserDto userDto)
        {
            if (string.IsNullOrWhiteSpace(userDto.Username))
                throw new Exception("La casilla no debe estar vacia");
            if (_repository.GetByUsername(userDto.Username, false) != null)
                throw new Exception("Usuario ya registrado");
            if (userDto.Password == null || userDto.Password.Length < 8)
                throw new Exception("La contraseña debe contener al menos 8 caracteres");
            var existing = _repository.GetByUsername(userDto.Username, true);
            if (existing != null)
            {
                existing.Username = userDto.Username;
                existing.Password = PasswordHelper.HashPassword(userDto.Password);
                existing.Role = (RoleType)userDto.Role!.Value;
                existing.IsActive = true;

                _repository.Update(existing);
            }
            else
            {
                var user = new User(
                    userDto.Username,
                    PasswordHelper.HashPassword(userDto.Password),
                    (RoleType)userDto.Role!.Value,
                    true
                );

                _repository.Add(user);
            }
        }
        public void Update(UserDto userDto)
        {
            User? user = _repository.GetByID(userDto.ID, false);
            var existing = _repository.GetByUsername(userDto.Username, true);
            if (existing != null && existing.ID != user.ID)
                throw new Exception("Nombre de usuario ya existente");
            if (user == null)
                throw new Exception("El usuario no existe");
            if (userDto.Username != null)
            {
                user.Username = userDto.Username;
            }
            if (userDto.Password != null)
            {
                user.Password = PasswordHelper.HashPassword(userDto.Password);
            }
            if (userDto.Role.HasValue)
            {
                user.Role = userDto.Role.Value;
            }
            _repository.Update(user);
        }
        public bool Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                    return false;
            if (string.IsNullOrWhiteSpace(password))
                    return false;
            var user = _repository.GetByUsername(username, false);

            if (user != null && !user.IsActive )
                return false;
            if (user == null)
                return false;
            return PasswordHelper.Verify(password, user.Password);
        }
    }
}
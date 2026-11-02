using StockControl.Data;
using StockControl.Dtos;
using StockControl.Enums;
using StockControl.Models;
using StockControl.Utils;
using StockControl.Utils.PasswordHelper;
using System.Security.Cryptography;
using System.Text;

namespace StockControl.Services
{
    public class UserService
    {
        private readonly UserPersistence _repository;
        private readonly string _databasePath;
        // A saved session expires if it isn't used for this long
        private static readonly TimeSpan SessionIdleLimit = TimeSpan.FromDays(30);
        public User CurrentUser { get; set; }
        public UserService(UserPersistence repository, string databasePath)
        {
            _repository = repository;
            _databasePath = databasePath;
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
        public void Remove(UserDto userDto)
        {
            User? user = _repository.GetByID(userDto.ID, false);
            if (user == null)
                throw new Exception("Usuario no encontrado");
            if (CurrentUser != null && user.ID == CurrentUser.ID)
                throw new Exception("No puedes eliminar tu propio usuario");
            if (user.Role == RoleType.Admin && CountActiveAdmins() <= 1)
                throw new Exception("Debe haber al menos un usuario administrador activo");
            _repository.Remove(user.ID);
            _repository.DeleteSessionsForUser(user.ID);
        }
        public void Update(UserDto userDto)
        {
            User? user = _repository.GetByID(userDto.ID, false);
            if (user == null)
                throw new Exception("El usuario no existe");
            bool passwordChanged = false;
            if (!string.IsNullOrWhiteSpace(userDto.Username))
            {
                var existing = _repository.GetByUsername(userDto.Username, true);
                if (existing != null && existing.ID != user.ID)
                    throw new Exception("Nombre de usuario ya existente");
                user.Username = userDto.Username;
            }
            // When editing, an empty password means "keep the current one".
            // Before, an empty string was hashed and the user got locked out.
            if (!string.IsNullOrWhiteSpace(userDto.Password))
            {
                if (userDto.Password.Length < 8)
                    throw new Exception("La contraseña debe contener al menos 8 caracteres");
                user.Password = PasswordHelper.HashPassword(userDto.Password);
                passwordChanged = true;
            }
            if (userDto.Role.HasValue && userDto.Role.Value != user.Role)
            {
                // Same rule as Remove: never leave the system without an active admin
                if (user.Role == RoleType.Admin && CountActiveAdmins() <= 1)
                    throw new Exception("Debe haber al menos un usuario administrador activo");
                user.Role = userDto.Role.Value;
            }
            _repository.Update(user);
            // A new password closes every "keep me signed in" session of that user
            if (passwordChanged)
                _repository.DeleteSessionsForUser(user.ID);
            if (CurrentUser != null && CurrentUser.ID == user.ID)
                CurrentUser = user;
        }
        private int CountActiveAdmins()
            => _repository.GetAll(includeInactive: false).Count(u => u.Role == RoleType.Admin);
        public User Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                    throw new Exception("El usuario no puede estar vacio");
            if (string.IsNullOrWhiteSpace(password))
                    throw new Exception("La contraseña no puede estar vacia");
            var user = _repository.GetByUsername(username, false);

            if (user == null || (user != null && !user.IsActive))
                throw new Exception("El usuario no existe");
            if(PasswordHelper.Verify(password, user.Password))
            {
                if (PasswordHelper.NeedsRehash(user.Password))
                {
                    user.Password = PasswordHelper.HashPassword(password);
                    _repository.Update(user);
                }
                CurrentUser = user;
                return CurrentUser;
            }
            else
            {
                throw new Exception("Contraseña invalida");
            }
        }
        public void Loggout()
        {
            // Logging out on purpose also forgets the saved session on this PC
            ForgetSavedSession();
            CurrentUser = null;
        }

        // ---- "Mantener sesión iniciada" ----

        // Called right after a successful Login when the checkbox is checked
        public void RememberCurrentUser()
        {
            if (CurrentUser == null)
                return;
            ForgetSavedSession();
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            _repository.AddSession(CurrentUser.ID, HashToken(token), Environment.MachineName, DateTime.Now);
            SessionStore.Save(new SavedSession(CurrentUser.ID, token, _databasePath));
        }

        // On startup: returns the user if there is a valid saved session, otherwise null
        public User? TryAutoLogin()
        {
            var saved = SessionStore.Load();
            if (saved == null)
                return null;

            // A session saved against another database (config changed, imported DB...) is ignored
            if (!string.Equals(saved.DatabasePath, _databasePath, StringComparison.OrdinalIgnoreCase))
            {
                SessionStore.Clear();
                return null;
            }

            var session = _repository.GetSessionByTokenHash(HashToken(saved.Token));
            if (session == null || session.UserId != saved.UserId ||
                DateTime.Now - session.LastUsedAt > SessionIdleLimit)
            {
                ForgetSavedSession();
                return null;
            }

            var user = _repository.GetByID(saved.UserId, false); // inactive users can't come back in
            if (user == null)
            {
                ForgetSavedSession();
                return null;
            }

            _repository.TouchSession(session.Id, DateTime.Now);
            CurrentUser = user;
            return user;
        }

        public void ForgetSavedSession()
        {
            var saved = SessionStore.Load();
            if (saved != null)
                _repository.DeleteSessionByTokenHash(HashToken(saved.Token));
            SessionStore.Clear();
        }

        private static string HashToken(string token)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
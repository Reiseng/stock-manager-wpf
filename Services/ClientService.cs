using StockControl.Data;
using StockControl.Models;
using StockControl.Dtos;

namespace StockControl.Services{
    public class ClientService
    {
        private const string FinalConsumerDni = "00000000";
        private readonly ClientPersistence _repository;

        // DNI: 7 or 8 digits (older DNIs have 7). CUIL/CUIT: 11 digits.
        private static void ValidateDni(string? dni)
        {
            if (string.IsNullOrWhiteSpace(dni))
                throw new Exception("Dni inválido");
            if (!dni.All(char.IsDigit))
                throw new Exception("El DNI debe contener solo números (sin puntos ni guiones)");
            if (dni.Length != 7 && dni.Length != 8 && dni.Length != 11)
                throw new Exception("DNI o CUIL inválido");
        }

        public ClientService(ClientPersistence repository)
        {
            _repository = repository;
        }

        public IReadOnlyList<Client> GetClients(bool includeInactive = false)
        {
            return _repository.GetAll(includeInactive: includeInactive);
        }
        public Client GetClientByDni(string dni)
        {
            ValidateDni(dni);
            Client? client = _repository.GetByDni(dni, includeInactive: false);
            if (client == null)
                throw new Exception("El cliente no existe");
            return client;
        }
        public Client GetClientByID(int id, bool includeInactive = false)
        {
            if (id <= 0)
                throw new Exception("Id inválida");
            Client? client = _repository.GetByID(id, includeInactive: includeInactive);
            if (client == null)
                throw new Exception("El cliente no existe");
            return client;
        }
        public void AddClient(ClientDto clientDto)
        {
            // Validated BEFORE saving: before, an invalid DNI was saved and then the
            // window showed an error and stayed open
            ValidateDni(clientDto.Dni);
            if (string.IsNullOrWhiteSpace(clientDto.Name))
                throw new Exception("Nombre inválido");
            if (string.IsNullOrWhiteSpace(clientDto.LastName))
                throw new Exception("Apellido inválido");
            if (_repository.GetByDni(clientDto.Dni) != null)
                throw new Exception("Cliente ya registrado");

            var existing = _repository.GetByDni(clientDto.Dni, includeInactive: true);
            if (existing != null)
            {
                existing.Name = clientDto.Name;
                existing.LastName = clientDto.LastName;
                existing.Phone = clientDto.Phone;
                existing.Email = clientDto.Email;
                existing.Address = clientDto.Address;
                existing.IsActive = true;

                _repository.Update(existing);
            }
            else
            {
                var client = new Client(
                    clientDto.Dni,
                    clientDto.Name,
                    clientDto.LastName,
                    clientDto.Phone,
                    clientDto.Email,
                    clientDto.Address,
                    true
                );

                _repository.Add(client);
            }
        }
        public void RemoveClient(int id)
        {
            if (id <= 0)
                throw new Exception("Id inválido");

            var client = _repository.GetByID(id);
            if (client == null)
                throw new Exception("El cliente no existe");
            if (client.Dni == FinalConsumerDni)
                throw new InvalidOperationException("No se puede eliminar Consumidor Final");

            _repository.Remove(client);
        }
        public void UpdateClient(ClientDto _client)
        {
            var client = _repository.GetByID(_client.ID);
            if (client == null)
                throw new Exception("Cliente no encontrado");
            if (client.Dni == FinalConsumerDni)
                throw new Exception("No se puede modificar Consumidor Final");
            if (_client.Dni != null && _client.Dni != client.Dni)
            {
                ValidateDni(_client.Dni);
                var other = _repository.GetByDni(_client.Dni, includeInactive: true);
                if (other != null && other.ID != client.ID)
                    throw new Exception("Ya existe un cliente con ese DNI");
                client.Dni = _client.Dni;
            }

            if (_client.Name != null)
                client.Name = _client.Name;

            if (_client.LastName != null)
                client.LastName = _client.LastName;
                
            if (_client.Email != null)
                client.Email = _client.Email;

            if (_client.Phone != null)
                client.Phone = _client.Phone;

            if (_client.Address != null)
                client.Address = _client.Address;

            _repository.Update(client);
        }
        public Client GetFinalConsumer()
        {
            var client = _repository.GetByDni(FinalConsumerDni, includeInactive: true);
            if (client != null)
            {
                if (!client.IsActive)
                {
                    client.IsActive = true;
                    _repository.Update(client);
                }
                return client;
            }

            // if it doesn't exist, create it (it used to be created inactive and then not found)
            client = new Client(FinalConsumerDni, "Consumidor", "Final", null, null, null, true);
            _repository.Add(client);
            return _repository.GetByDni(FinalConsumerDni)!;
        }
    }

}
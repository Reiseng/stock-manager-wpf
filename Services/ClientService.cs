using StockControl.Data;
using StockControl.Models;
using StockControl.Dtos;

namespace StockControl.Services{
    public class ClientService
    {
        private readonly ClientPersistence _repository;

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
            if (string.IsNullOrWhiteSpace(dni))
                throw new Exception("Dni inválido");
            if (!dni.All(char.IsDigit))
                throw new Exception("El DNI debe contener solo números");

            if (dni.Length != 8 && dni.Length != 11)
                throw new Exception("DNI o CUIL inválido");
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
            if (string.IsNullOrWhiteSpace(clientDto.Dni))
                throw new Exception("Dni inválido");
            if (string.IsNullOrWhiteSpace(clientDto.Name))
                throw new Exception("Nombre inválido");
            if (string.IsNullOrWhiteSpace(clientDto.LastName))
                throw new Exception("Apellido inválido");
            if (_repository.GetByDni(clientDto.Dni) != null && _repository.GetByDni(clientDto.Dni).IsActive)
                throw new Exception("Usuario ya registrado");

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
            if (client.Dni == "00000000")
                throw new InvalidOperationException("No se puede eliminar Consumidor Final");
            if (client == null)
                throw new Exception("El cliente no existe");

            _repository.Remove(client);
        }
        public void UpdateClient(ClientDto _client)
        {
            var client = _repository.GetByID(_client.ID);
            if (client == null)
                throw new Exception("Cliente no encontrado");
            if (_client.Dni != null)
                client.Dni = _client.Dni;

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
            var client = _repository.GetByDni("00000000");
            if (client != null)
                return client;

            // if don't exist, create it
            client = new Client
            {
                Dni = "00000000",
                Name = "Consumidor",
                LastName = "Final",
            };

            _repository.Add(client);
            return _repository.GetByDni("00000000")!;
        }
    }

}
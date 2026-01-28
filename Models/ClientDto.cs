namespace StockControl.Models
{
    public class ClientDto
    {
        public int ID { get; set; }
        public string? Dni { get; set; }
        public string? Name { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public ClientDto(int id, string dni, string name, string lastName, string email, string phone, string address){
            ID = id;
            Dni = dni;
            Name = name;
            LastName = lastName;
            Email = email;
            Phone = phone;
            Address = address;
        }
        public ClientDto(){}
        public static ClientDto FromModel(Client client)
        {
            return new ClientDto
            {
                ID = client.ID,
                Name = client.Name,
                LastName = client.LastName,
                Dni = client.Dni,
                Email = client.Email,
                Phone = client.Phone,
                Address = client.Address
            };
        }
    }
}
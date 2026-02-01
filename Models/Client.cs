namespace StockControl.Models
{
    public class Client{
    public int ID { get; set; }
    public string Dni { get; set; }
    public string Name { get; set; }
    public string LastName { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Address { get; set; }
    public bool IsActive { get; set; }

    public override string ToString()
        => $"{Name} {LastName}";
        //Constructors

        public Client(string dni, string name, string lastName, string email, string phone, string address, bool isActive){
            Dni = dni;
            Name = name;
            LastName = lastName;
            Email = email;
            Phone = phone;
            Address = address;
            IsActive = isActive;
        }
        public Client(){}
}
}
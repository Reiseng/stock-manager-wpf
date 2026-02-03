using System.ComponentModel;
using System.Runtime.CompilerServices;
using StockControl.Enums;

public class Company: INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public int Id { get; set; }
        public string? CUIT { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public TaxCondition taxCondition { get; set; }
        private decimal _tax;
        public decimal tax         {
            get => _tax;
            set
            {
                _tax = value;
                OnPropertyChanged();
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    public Company(string? cuit, string? name, string? address, string? phone, string? email, TaxCondition taxCondition, decimal tax)
        {
            CUIT = cuit;
            Name = name;
            Address = address;
            Phone = phone;
            Email = email;
            this.taxCondition = taxCondition;
            this.tax = tax;
        }
    }
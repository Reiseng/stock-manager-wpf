using StockControl.Data;
using StockControl.Models;
namespace StockControl.Services
{
    public class CompanyService
    {
        private readonly CompanyPersistence _repository;

        public CompanyService(CompanyPersistence repository)
        {
            _repository = repository;
        }

        public Company GetCompanyInfo()
        {
            Company? company = _repository.GetCompany();
            if (company == null)
                throw new Exception("La información de la empresa no está configurada.");
            return company;
        }
        public void SetCompanyInfo(Company company)
        {
            if (string.IsNullOrWhiteSpace(company.CUIT))
                throw new Exception("CUIT inválido");
            if (string.IsNullOrWhiteSpace(company.Name))
                throw new Exception("Nombre inválido");

            Company? existingCompany = _repository.GetCompany();
            if (existingCompany == null)
            {
                var newCompany = new Company(
                    company.CUIT,
                    company.Name,
                    company.Address,
                    company.Phone,
                    company.Email,
                    company.taxCondition
                );
                _repository.AddCompany(newCompany);
            }
            else
            {
                existingCompany.CUIT = company.CUIT;
                existingCompany.Name = company.Name;
                existingCompany.Address = company.Address;
                existingCompany.Phone = company.Phone;
                existingCompany.Email = company.Email;
                existingCompany.taxCondition = company.taxCondition;

                _repository.UpdateCompany(existingCompany);
            }
        }
    }
}
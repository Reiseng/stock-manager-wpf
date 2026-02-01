using StockControl.Data;
using StockControl.Enums;
using StockControl.Models;

namespace StockControl.Services
{
    public class ProductService
    {
        private readonly ProductPersistence _repository;

        public ProductService(ProductPersistence repository)
        {
            _repository = repository;
        }

        public IReadOnlyList<Product> GetProducts(bool includeInactive)
        {
            return _repository.GetAll(includeInactive: includeInactive);
        }
        public Product GetProductByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                throw new Exception("Código inválido");
            Product? product = _repository.GetByBarcode(barcode, false);
            if (product == null)
                throw new Exception("El producto no existe");
            return product;
        }

        public Product GetProductByID(int id, bool includeInactive)
        {
            if (id <= 0)
                throw new Exception("Id inválida");
            Product? product = _repository.GetByID(id, includeInactive: includeInactive);
            if (product == null)
                throw new Exception("El producto no existe");
            return product;
        }
        public List<Product> SearchByName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<Product>();

            text = text.ToLower();

            return _repository.GetAll(false)
                .Where(p =>
                    p.Name.ToLower().Contains(text) ||
                    p.Brand.ToLower().Contains(text))
                .ToList();
        }
        public void AddProduct(ProductDto _product)
        {
            if (string.IsNullOrWhiteSpace(_product.Barcode))
                throw new Exception("Código inválido");
            if (_repository.GetByBarcode(_product.Barcode, true) != null)
                throw new Exception("Producto ya registrado");
            if (_product.Price <= 0)
                throw new Exception("Precio inválido");
            var existing = _repository.GetByBarcode(_product.Barcode, true);
            if (existing != null)
            {
                existing.Name = _product.Name;
                existing.Brand = _product.Brand;
                existing.Price = _product.Price.Value;
                existing.Stock = _product.Stock.Value;
                existing.Unit = (UnitType)_product.Unit!.Value;
                existing.IsActive = true;

                _repository.Update(existing);
            }
            else
            {
                var product = new Product(
                    _product.Barcode,
                    _product.Brand,
                    _product.Name,
                    _product.Price.Value,
                    _product.Stock.Value,
                    (UnitType)_product.Unit!.Value,
                    true
                );

                _repository.Add(product);
            }

        }
        public void UpdateProduct(ProductDto _product)
        {
            Product? product = _repository.GetByID(_product.Id, false);
            if (product == null)
                throw new Exception("El producto no existe");
            if (_product.Name != null)
            {
                product.Name = _product.Name;
            }
            if (_product.Barcode != null)
            {
                product.Barcode = _product.Barcode;
            }
            if (_product.Brand != null)
            {
                product.Brand = _product.Brand;
            }
            if (_product.Price.HasValue)
            {
                product.Price = _product.Price.Value;
            }
            if (_product.Stock.HasValue)
            {
                product.Stock = _product.Stock.Value;
            }
            if (_product.Unit.HasValue)
            {
                product.Unit = _product.Unit.Value;
            }
            _repository.Update(product);
        }
        public void RemoveProduct(int id)
        {
            if (id <= 0)
                throw new Exception("ID inválido");
            var product = _repository.GetByID(id, false);
            if (product == null)
                throw new Exception("Producto no encontrado");
            _repository.Remove(product);
        }
        // Intended for business operations such as returns or credit notes
        public void DecreaseStock(int id, decimal quantity)
        {
            if (id <= 0)
                throw new Exception("ID inválido");

            if (quantity <= 0)
                throw new Exception("Cantidad inválida");

            var product = _repository.GetByID(id, false);

            if (product == null)
                throw new Exception("Producto no encontrado");
            // Reduce stock only after validating availability to avoid negative values
            if (product.Stock < quantity)
                throw new Exception("Stock insuficiente");
            _repository.UpdateStock(id, product.Stock - quantity);
        }
        // Intended for business operations such as returns or credit notes
        public void IncreaseStock(int id, decimal quantity)
        {
            if (id <= 0)
                throw new Exception("ID inválido");
            // Increase stock only after validating input value to avoid reduction of stock
            if (quantity <= 0)
                throw new Exception("Cantidad inválida");

            var product = _repository.GetByID(id, false);

            if (product == null)
                throw new Exception("Producto no encontrado");

            _repository.UpdateStock(id, product.Stock + quantity);
        }
    }
}
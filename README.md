Stock Manager (WPF)

Desktop application for stock, sales, and basic business management, developed in C# using WPF (.NET 9).

This project simulates a small business management system including inventory control, user roles, and sales operations.

🛠️ Technologies Used

- C#
- .NET 9 (WPF)
- SQLite
- MVVM Pattern
- LINQ
- Layered Architecture
- Git

📌 Features

- Inventory Management
- Create, update and deactivate products
- Stock validation (no negative values)
- Price management
- Sales & Operations
- Sales
- Credit Notes
- Debit Notes
- Budgets
- Operation type validation
- Related invoice validation for credit notes
- Authentication & Security
- Login system
- Role-based access (Admin / User)
- Password hashing
- Automatic admin bootstrap if no active admin exists
- Prevent self-deletion of admin
- Prevent deletion if only one active admin remains
- Data Persistence
- SQLite database
- Automatic database initialization
- Layered architecture (Repository + Service)

🧩 Architecture Overview

The application follows a layered architecture:

- UI Layer (WPF / MVVM)
  Responsible for user interaction and data binding.
- Service Layer
  Contains business rules and validations (stock checks, role validation, operation logic).
- Repository Layer
  Manages data access using SQLite.
  
This structure allows future scalability and maintainability.

🚀 How to Run

Option 1 – Run from source
- Clone the repository
- Open the solution in Visual Studio
- Build and run

Option 2 – Download Release
- Download the latest release from the GitHub Releases section and run the executable.

🔮 Planned Improvements

- Ticket / invoice PDF generation
- Reporting module
- Search and filtering improvements
- UI/UX polishing
- Automated tests
- Versioning system
- Backup & restore functionality

📚 Purpose of the Project

- This project demonstrates:
- Clean architecture principles
- Business rule implementation
- Authentication and role-based validation
- Defensive programming
- Desktop application development with WPF
- Real-world inventory and billing logic

📄 License

- MIT License.

---

## 🤝 Contributions
Contributions are welcome.  
Feel free to open issues or submit pull requests.

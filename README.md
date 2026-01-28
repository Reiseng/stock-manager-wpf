# Stock Manager (WPF)

Desktop application for basic stock and product management, developed in **C# using WPF**.

This project was created as a practical exercise to apply good programming practices, layered architecture, and basic business rules related to inventory control.

---

## 🛠️ Technologies Used

- C#
- .NET
- WPF
- LINQ
- In-memory persistence
- Git

---

## 📌 Features

- Create, update and delete products
- Increase and decrease stock with validation
- Prevent negative stock values
- Separation of concerns using Repository and Service layers
- Input validation and basic error handling

---

## 🧩 Architecture Overview

The application follows a simple layered architecture:

- **UI Layer**  
  WPF views responsible for user interaction.

- **Service Layer**  
  Contains business logic and validations (e.g. stock availability, input checks).

- **Repository Layer**  
  Handles data access and persistence (currently in-memory).

This structure allows easy future replacement of the persistence layer with a database.

---

## 🚀 How to Run

1. Clone the repository  
2. Open the solution in Visual Studio  
3. Run the project  

No additional configuration is required.

---

## 🔮 Possible Improvements

- Replace in-memory storage with a database (SQL Server / SQLite)
- Add authentication and user roles
- Improve UI/UX
- Add unit tests

---

## 📚 Purpose of the Project

This project is intended to demonstrate:

- Object-oriented programming
- Separation of responsibilities
- Clean and readable code
- Basic inventory business logic

---

## 📄 License
This project is licensed under the MIT License.

---

## 🤝 Contributions
Contributions are welcome.  
Feel free to open issues or submit pull requests.

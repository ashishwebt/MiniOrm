# MiniOrm
[日本語](./README.ja.md)

A lightweight, minimal Object-Relational Mapper (ORM) for .NET, built to keep the layer between your C# objects and your database as thin and predictable as possible.

## Features

- Simple, minimal API surface for CRUD operations
- No heavy runtime dependencies or complex configuration
- Designed for projects that want SQL-level control without hand-writing every query
- Built as a standard .NET solution (`MiniOrm.sln`) with shared build settings via `Directory.Build.props` / `Directory.Build.targets`

## Getting Started

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (version pinned in `global.json`)

### Clone the repository

```bash
git clone https://github.com/ashishwebt/MiniOrm.git
cd MiniOrm
```

### Build

```bash
dotnet restore
dotnet build
```

### Run tests

```bash
dotnet test
```

## Project Structure

```
MiniOrm/
├── src/                        # Library source code
├── Directory.Build.props       # Shared MSBuild properties for all projects
├── Directory.Build.targets     # Shared MSBuild targets for all projects
├── global.json                 # Pinned .NET SDK version
├── MiniOrm.sln                 # Visual Studio solution file
└── LICENSE                     # MIT License
```

## Usage

Add a reference to the MiniOrm project/library from your application, then use it to map your C# classes to database tables and perform basic operations (insert, update, delete, and query) without writing boilerplate ADO.NET code.

```csharp
// Example usage — adjust to match the actual public API in src/
var connection = new MiniOrmConnection(connectionString);

var user = new User { Name = "Ashish", Email = "ashish@example.com" };
connection.Insert(user);

var users = connection.Query<User>("SELECT * FROM Users");
```

> Note: Update this section with the real API once the public classes/methods in `src/` are finalized — the snippet above is a placeholder to be replaced with actual usage examples.

## Contributing

Contributions are welcome!

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes
4. Push to the branch and open a Pull Request

## License

This project is licensed under the [MIT License](LICENSE).

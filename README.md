# FLMTest


# 1) Prerequisites (Windows)

.NET 8 SDK (or just Visual Studio 2022/2025 with “.NET desktop dev”)

One SQL option:

Docker Desktop or

Local SQL Server (Developer/Express)

# 2) Get the code

Unzip the solution folder (e.g., FLMTest).

Open FLMDesktop.sln in Visual Studio.

3) Configure the database connection

Open appsettings.json (in the WPF project).

Set the connection string. Two easy choices:

# A) Using Docker (recommended)

"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1435;Database=BranchProductDb;User ID=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
}

Then start SQL Server in Docker:

docker run -d --name bp-mssql 
  -e "ACCEPT_EULA=Y" 
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" 
  -p 1435:1433 
  mcr.microsoft.com/mssql/server:2022-latest


# B) Using local SQL Server

"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=BranchProductDb;Trusted_Connection=True;TrustServerCertificate=True"
}

# 4) Create the database/tables (one of these)

Option 1 (quickest): run the provided SQL snippet (I have created a built-in EnsureCreated this should help us create the database if connection string is given)
In SQL Server Object Explorer → New Query against your server:

IF DB_ID('BranchProductDb') IS NULL CREATE DATABASE BranchProductDb;
GO
USE BranchProductDb;
GO
IF OBJECT_ID('dbo.Branch','U') IS NULL
CREATE TABLE Branch (Id INT IDENTITY PRIMARY KEY, Name varchar(100) NOT NULL, TelephoneNumber varchar(10) NULL, OpenDate datetime NULL);
IF OBJECT_ID('dbo.Product','U') IS NULL
CREATE TABLE Product (Id INT IDENTITY PRIMARY KEY, Name varchar(100) NOT NULL, WeightedItem bit NOT NULL DEFAULT 0, SuggestedSellingPrice decimal(14,2) NOT NULL DEFAULT 0);
IF OBJECT_ID('dbo.BranchProduct','U') IS NULL
CREATE TABLE BranchProduct (BranchId INT NOT NULL, ProductId INT NOT NULL,
  CONSTRAINT PK_BranchProduct PRIMARY KEY(BranchId, ProductId),
  CONSTRAINT FK_BP_B FOREIGN KEY(BranchId) REFERENCES Branch(Id) ON DELETE CASCADE,
  CONSTRAINT FK_BP_P FOREIGN KEY(ProductId) REFERENCES Product(Id) ON DELETE CASCADE);


# Option 2 (if migrations are present):

dotnet tool update -g dotnet-ef
dotnet ef database update --project .\FLMDesktop\FLMDesktop.csproj --connection "<your connection string>"

# 5) Build & run

In Visual Studio: Build → Rebuild Solution, then Start.

First run creates/uses BranchProductDb. If empty, the Branches page shows no rows.

# 6) Using the app (what to click)

Start screen: big cards:

Branches → CRUD Branches

Products & Assignments → manage Products and assign them to a selected Branch

Branches page

Top left: New / Edit / Delete / Search

Import / Export buttons to load/save CSV / JSON / XML "Feature could not be cemented as time was finished but it was a nice epic journey"

Products & Assignments

Middle grid (All Products): New / Edit / Delete / Import / Export

Right grid (Assigned): Assign → / ← Unassign

Import/Export Mappings to load/save Branch–Product links (CSV/JSON/XML)

Sample files for import (put anywhere, then choose in the file dialogs):

Branch.csv / .json / .xml  - This will not work for this iteration Effort was taken regardless.

# 7) Logs (optional) - This was the last on the list and did not make it to our project

Serilog writes to Logs\log-<date>.txt beside the exe (if configured).
Helpful if something fails (connection string, import parse, etc).

# 8. nuget Installs
Make Sure you have the CsvHelper  Installed; 

# 9) Common issues I Faced (fast fixes)

“ConnectionString has not been initialized” → ensure appsettings.json exists in output (csproj includes CopyToOutputDirectory=Always), and DefaultConnection is valid.

Docker port in use → change -p 1435:1433 to another host port (e.g., 1436) and update the connection string.

Import format → CSV must have headers. JSON/XML must match sample schema (ID, Name, WeightedItem, SuggestedSellingPrice etc.).

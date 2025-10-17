# Database Atomicity Demo - Money Transfer

This ASP.NET Core MVC application demonstrates database transaction atomicity using raw ADO.NET and PostgreSQL.

## 🎯 Purpose

Shows how database transactions ensure **all-or-nothing** operations - either all steps in a money transfer succeed, or all are rolled back, preventing partial/inconsistent states.

## 📋 Prerequisites

1. **PostgreSQL Server** running on `localhost:5432`
2. Database named `mydatabase`
3. User `postgres` with password `mypassword`
4. .NET 8.0 SDK

## 🗄️ Database Setup

Connect to PostgreSQL and run:

```sql
-- Create database (if not exists)
CREATE DATABASE mydatabase;

-- Connect to mydatabase, then create table
CREATE TABLE accounts (
    account_id VARCHAR(50) PRIMARY KEY,
    balance DECIMAL(10,2) NOT NULL
);

-- Insert test data
INSERT INTO accounts (account_id, balance) VALUES 
('alice', 1000.00),
('bob', 500.00);
```

## 🚀 Running the Application

1. **Build and run:**
   ```powershell
   dotnet run
   ```

2. **Open browser:** Navigate to `https://localhost:5001` or `http://localhost:5000`

## 🧪 Testing Scenarios

### Normal Transfer
1. Click "Transfer $100 from Alice to Bob"
2. Should see success message
3. Check database: `SELECT * FROM accounts WHERE account_id IN ('alice', 'bob');`
4. Alice should have $900, Bob should have $600

### Atomicity Test (Crash Simulation)
1. **Edit** `Controllers/TransferController.cs`
2. **Uncomment** line ~52: `// Environment.Exit(1);`
3. **Run transfer** - application will crash after debiting Alice
4. **Restart application**
5. **Check database** - balances should be unchanged (transaction rolled back)
6. **Comment out** the crash line and test normal operation again

## 🔍 Key Learning Points

- **Transaction Boundaries:** `BeginTransaction()` → operations → `Commit()` or `Rollback()`
- **Atomicity:** Either all operations succeed or all fail
- **Error Handling:** Any exception triggers rollback
- **Consistency:** Database never shows partial transfer states
- **Raw ADO.NET:** Direct SQL control without ORM abstraction

## 📁 Project Structure

```
DB-Internals-Demo/
├── Controllers/
│   └── TransferController.cs    # Main demo logic
├── Views/
│   └── Transfer/
│       └── Index.cshtml         # UI with transfer form
├── Program.cs                   # App configuration
├── DB-Internals-Demo.csproj    # Project file with Npgsql
└── README.md                    # This file
```

## 🔧 Configuration


Already using docker I have created a database
```sql
docker exec -it my-postgres psql -U postgres -d mydatabase
```

```sql
BEGIN;

UPDATE accounts SET balance = 1000.00 WHERE account_id = 'Virat';
UPDATE accounts SET balance = 500.00 WHERE account_id = 'Rohit';

COMMIT;
```

Connection string in `TransferController.cs`:
```csharp
"Host=localhost;Port=5432;Database=mydatabase;Username=postgres;Password=mypassword"
```

Modify as needed for your PostgreSQL setup.

## 💡 Extensions

Try these experiments:
- Add validation for negative balances
- Implement transfer amount as parameter
- Add logging to track transaction steps
- Test with concurrent transfers
- Add more accounts for complex scenarios
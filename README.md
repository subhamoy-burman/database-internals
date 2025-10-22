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

### **Start the Application:**
1. **Build and run:**
   ```powershell
   dotnet run
   ```

2. **Open browser:** Navigate to `http://localhost:5000`
   - **Atomicity Demo:** `http://localhost:5000/Transfer` (default)
   - **Isolation Levels Demo:** `http://localhost:5000/Booking`

### **Stop the Application:**
If the application gets stuck or you need to restart:

```powershell
# Stop all dotnet processes
taskkill /F /IM dotnet.exe

# Or stop specific process if you know the PID
taskkill /F /PID <process_id>
```

### **Debug Mode (VS Code):**
1. **Press F5** to start with debugger
2. **Set breakpoints** in controller methods
3. **Step through code** to see transaction behavior

### **Development Mode:**
```powershell
# Run with detailed logging
$env:ASPNETCORE_ENVIRONMENT="Development"; dotnet run

# Or use watch mode (auto-restart on changes)
dotnet watch run
```

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

## 🎭 Isolation Levels Demo Setup

For the concert seat booking demo, also create:

```sql
-- Create seats table for isolation level demo
CREATE TABLE seats (
    seat_id VARCHAR(10) PRIMARY KEY,
    status VARCHAR(20) DEFAULT 'available', -- 'available', 'reserving', 'booked'
    price DECIMAL(8,2) DEFAULT 100.00,
    booked_by VARCHAR(100),
    reserved_by VARCHAR(100),  -- For intermediate reservation state
    booked_at TIMESTAMPTZ,
    reserved_at TIMESTAMPTZ
);

-- Insert test seats
INSERT INTO seats (seat_id, status, price) VALUES 
('A1', 'available', 150.00),
('A2', 'available', 150.00),
('A3', 'available', 150.00),
('B1', 'available', 120.00),
('B2', 'available', 120.00);
```

## 🧪 Testing Isolation Levels

### **Demo URLs:**
- **Atomicity Demo:** `http://localhost:5000/Transfer` or `http://localhost:5000/`
- **Isolation Levels Demo:** `http://localhost:5000/Booking`

### **How to Test Isolation Levels:**

#### **Scenario 1: Single Browser, Multiple Tabs**
1. Navigate to `http://localhost:5000/Booking`
2. Select "Repeatable Read" isolation level
3. Click "Book Seat A1" - you'll see "Processing payment... ⏳ (15 second delay)"
4. **Quickly** open another tab to the same URL during the 15-second delay
5. In the new tab, select "Read Committed" and try to book the same seat
6. **Observe the different behaviors** in the application logs

#### **Scenario 2: Multiple Browsers**
1. Open **Chrome:** `http://localhost:5000/Booking`
2. Open **Firefox:** `http://localhost:5000/Booking`
3. In Chrome: Select "Serializable", click "Book Seat A1"
4. **During the 15-second delay**, switch to Firefox
5. In Firefox: Select "Read Committed", click "Book Seat A1"
6. Watch the terminal logs to see isolation behavior

#### **Expected Behaviors:**

| Isolation Level | First Session | Second Session (during delay) |
|---|---|---|
| **Read Uncommitted** | Books successfully | ⚠️ Same as Read Committed (PostgreSQL limitation) |
| **Read Committed** | Books successfully | Sees "reserving" status, booking fails |
| **Repeatable Read** | Books successfully | Sees original "available" status throughout |
| **Serializable** | Books successfully | May get serialization error or wait |

#### **Debug Logs to Watch For:**
```
SESSION 12ab34cd: 🔍 INITIAL READ - Seat A1 status: 'available'
SESSION 12ab34cd: STEP 3 - Processing payment... ⏳ (15 second delay)
SESSION 12ab34cd: 🚨 DEBUG POINT: During this delay, try booking from another browser!
SESSION ef56789a: 🔍 INITIAL READ - Seat A1 status: 'reserving' (Read Committed)
SESSION ef56789a: 🔍 INITIAL READ - Seat A1 status: 'available' (Repeatable Read)
```

### **📚 Educational Notes:**

#### **PostgreSQL vs Other Databases:**
- **PostgreSQL:** Treats `READ UNCOMMITTED` same as `READ COMMITTED` for safety
- **SQL Server/MySQL:** Actually implement dirty reads in `READ UNCOMMITTED`
- **This demo:** Shows PostgreSQL's behavior - use it to explain the differences

#### **Why PostgreSQL Does This:**
- **Safety first:** Prevents reading corrupt/partial data
- **MVCC architecture:** Uses snapshots instead of locks
- **Consistency:** Maintains ACID properties even at lowest isolation level

### **Reset Demo:**
```sql
-- Quick reset via application UI
-- Click "🔄 Reset Demo" button to make seat A1 available again

-- Manual SQL reset
UPDATE seats SET status = 'available', booked_by = NULL, reserved_by = NULL WHERE seat_id = 'A1';

-- Complete data reset (if needed)
DELETE FROM seats;
INSERT INTO seats (seat_id, status, price) VALUES 
('A1', 'available', 150.00),
('A2', 'available', 150.00),
('A3', 'available', 150.00),
('B1', 'available', 120.00),
('B2', 'available', 120.00);
```

## 🔧 Configuration

Connection string in `TransferController.cs` and `BookingController.cs`:
```csharp
"Host=localhost;Port=5432;Database=mydatabase;Username=postgres;Password=mypassword"
```

Modify as needed for your PostgreSQL setup.

## 🛠️ Troubleshooting

### **Application Won't Start:**
```powershell
# Kill any stuck dotnet processes
taskkill /F /IM dotnet.exe

# Clean build artifacts
dotnet clean
dotnet build

# Try running again
dotnet run
```

### **Database Connection Issues:**
```sql
-- Test Docker container
docker ps

-- Test database connection
docker exec -it my-postgres psql -U postgres -d mydatabase

-- Verify tables exist
\dt
SELECT * FROM accounts LIMIT 1;
SELECT * FROM seats LIMIT 1;
```

### **Port Already in Use:**
```powershell
# Find process using port 5000
netstat -ano | findstr :5000

# Kill the specific process (replace PID)
taskkill /F /PID <process_id>
```

### **Reset Everything:**
```sql
-- Reset all demo data
BEGIN;
UPDATE seats SET status = 'available', booked_by = NULL, reserved_by = NULL;
UPDATE accounts SET balance = 1000.00 WHERE account_id = 'Virat';
UPDATE accounts SET balance = 500.00 WHERE account_id = 'Rohit';
COMMIT;
```

## 💡 Extensions

Try these experiments:
- Add validation for negative balances
- Implement transfer amount as parameter
- Add logging to track transaction steps
- Test with concurrent transfers
- Add more accounts for complex scenarios
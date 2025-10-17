using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;

namespace DB_Internals_Demo.Controllers
{
    public class TransferController : Controller
    {
        private readonly string _connectionString = "Host=localhost;Port=5432;Database=mydatabase;Username=postgres;Password=mypassword";
        private readonly ILogger<TransferController> _logger;

        public TransferController(ILogger<TransferController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            _logger.LogInformation("Index action called");
            return View();
        }

        [HttpPost]
        public IActionResult ExecuteTransfer()
        {
            _logger.LogInformation("=== TRANSFER DEBUG: ExecuteTransfer action called - POST request received ===");
            _logger.LogInformation("Request Method: {Method}, Content-Type: {ContentType}", 
                Request.Method, Request.ContentType);
            
            try
            {
                _logger.LogInformation("Attempting to connect to database with connection string: {ConnectionString}", 
                    _connectionString.Replace("Password=mypassword", "Password=***"));
                
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    _logger.LogInformation("Database connection opened successfully");

                    // Begin transaction for atomicity
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Step 1: Check if Virat has at least $100
                            using (var checkCmd = new NpgsqlCommand("SELECT balance FROM accounts WHERE account_id = 'Virat'", connection, transaction))
                            {
                                var viratBalance = (decimal?)checkCmd.ExecuteScalar();
                                
                                if (viratBalance == null)
                                {
                                    throw new Exception("Account 'Virat' not found");
                                }
                                
                                if (viratBalance < 100)
                                {
                                    throw new Exception($"Insufficient funds. Virat's balance: ${viratBalance:F2}");
                                }
                            }

                            // Step 2: Deduct $100 from Virat
                            using (var debitCmd = new NpgsqlCommand("UPDATE accounts SET balance = balance - 100 WHERE account_id = 'Virat'", connection, transaction))
                            {
                                int rowsAffected = debitCmd.ExecuteNonQuery();
                                if (rowsAffected == 0)
                                {
                                    throw new Exception("Failed to debit Virat's account");
                                }
                            }

                            // Step 3: [OPTIONAL CRASH SIMULATION] - Uncomment the next line to test atomicity
                            //Environment.Exit(1);  // Simulates application crash - transaction will be rolled back

                            // Step 4: Add $100 to Rohit
                            using (var creditCmd = new NpgsqlCommand("UPDATE accounts SET balance = balance + 100 WHERE account_id = 'Rohit'", connection, transaction))
                            {
                                int rowsAffected = creditCmd.ExecuteNonQuery();
                                if (rowsAffected == 0)
                                {
                                    throw new Exception("Failed to credit Rohit's account");
                                }
                            }

                            // Step 5: Commit the transaction - all operations succeed together
                            transaction.Commit();
                            
                            ViewBag.Message = "✅ SUCCESS: Transfer completed! $100 transferred from Virat to Rohit.";
                            ViewBag.MessageType = "success";
                        }
                        catch (Exception)
                        {
                            // Step 6: Rollback on any error - ensures atomicity
                            transaction.Rollback();
                            throw; // Re-throw to be caught by outer catch
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transfer failed with error: {ErrorMessage}", ex.Message);
                ViewBag.Message = $"❌ ERROR: Transfer failed - {ex.Message}";
                ViewBag.MessageType = "error";
                
                // Additional debug info for connection issues
                if (ex is NpgsqlException || ex.Message.Contains("connection") || ex.Message.Contains("server"))
                {
                    ViewBag.Message += $" | Connection Details: Host=localhost, Port=5432, Database=mydatabase";
                }
            }

            return View("Index");
        }

        // Test database connection
        public IActionResult TestConnection()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    using (var cmd = new NpgsqlCommand("SELECT version()", connection))
                    {
                        var version = cmd.ExecuteScalar()?.ToString();
                        return Json(new { success = true, version = version, connectionString = _connectionString.Replace("Password=mypassword", "Password=***") });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message, innerException = ex.InnerException?.Message });
            }
        }

        // Helper action to get current balances (for demonstration)
        public IActionResult GetBalances()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    var balances = new Dictionary<string, decimal>();
                    
                    using (var cmd = new NpgsqlCommand("SELECT account_id, balance FROM accounts WHERE account_id IN ('Virat', 'Rohit') ORDER BY account_id", connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            balances[reader.GetString(0)] = reader.GetDecimal(1);
                        }
                    }
                    
                    return Json(balances);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
    }
}
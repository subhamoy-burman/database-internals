using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

namespace DB_Internals_Demo.Controllers
{
    public class BookingController : Controller
    {
        private readonly string _connectionString = "Host=localhost;Port=5432;Database=mydatabase;Username=postgres;Password=mypassword";
        private readonly ILogger<BookingController> _logger;

        public BookingController(ILogger<BookingController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            var sessionId = Guid.NewGuid().ToString()[..8]; // Short session ID for display
            ViewBag.SessionId = sessionId;
            
            _logger.LogInformation($"=== SESSION {sessionId}: Index page loaded ===");

            // Get current seat status
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var cmd = new NpgsqlCommand("SELECT status, price, booked_by, reserved_by FROM seats WHERE seat_id = 'A1'", connection);
                using var reader = cmd.ExecuteReader();
                
                if (reader.Read())
                {
                    ViewBag.SeatStatus = reader.GetString("status");
                    ViewBag.SeatPrice = reader.GetDecimal("price");
                    ViewBag.BookedBy = reader.IsDBNull("booked_by") ? null : reader.GetString("booked_by");
                    ViewBag.ReservedBy = reader.IsDBNull("reserved_by") ? null : reader.GetString("reserved_by");
                    
                    _logger.LogInformation($"SESSION {sessionId}: Seat A1 status = {ViewBag.SeatStatus}, booked_by = {ViewBag.BookedBy ?? "null"}, reserved_by = {ViewBag.ReservedBy ?? "null"}");
                }
                else
                {
                    ViewBag.SeatStatus = "not_found";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"SESSION {sessionId}: Index page error: {ex.Message}");
                ViewBag.SeatStatus = "error";
                ViewBag.Message = $"Error: {ex.Message}";
            }

            return View();
        }

        [HttpPost]
        public IActionResult Book(string isolationLevel)
        {
            var sessionId = Guid.NewGuid().ToString()[..8];
            ViewBag.SessionId = sessionId;
            
            _logger.LogInformation($"=== SESSION {sessionId}: Starting booking with isolation level: {isolationLevel} ===");

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                // Set isolation level and begin transaction
                using var transaction = connection.BeginTransaction();
                
                // Set the isolation level
                var isolationCommand = isolationLevel switch
                {
                    "ReadUncommitted" => "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED",
                    "ReadCommitted" => "SET TRANSACTION ISOLATION LEVEL READ COMMITTED",
                    "RepeatableRead" => "SET TRANSACTION ISOLATION LEVEL REPEATABLE READ", 
                    "Serializable" => "SET TRANSACTION ISOLATION LEVEL SERIALIZABLE",
                    _ => "SET TRANSACTION ISOLATION LEVEL READ COMMITTED"
                };

                using (var setIsolationCmd = new NpgsqlCommand(isolationCommand, connection, transaction))
                {
                    setIsolationCmd.ExecuteNonQuery();
                    _logger.LogInformation($"SESSION {sessionId}: Set isolation level to {isolationLevel}");
                    
                    if (isolationLevel == "ReadUncommitted")
                    {
                        _logger.LogWarning($"SESSION {sessionId}: ⚠️  PostgreSQL Note: READ UNCOMMITTED behaves same as READ COMMITTED (PostgreSQL safety feature)");
                    }
                }

                // STEP 1: Check if seat is available
                _logger.LogInformation($"SESSION {sessionId}: STEP 1 - Checking seat availability...");
                
                string currentStatus;
                decimal currentPrice;
                string? currentBookedBy;
                string? currentReservedBy;
                
                using (var checkCmd = new NpgsqlCommand("SELECT status, price, booked_by, reserved_by FROM seats WHERE seat_id = 'A1'", connection, transaction))
                using (var reader = checkCmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        transaction.Rollback();
                        ViewBag.Message = "❌ ERROR: Seat A1 not found!";
                        _logger.LogError("SESSION {SessionId}: Seat A1 not found in database", sessionId);
                        return View("Index");
                    }

                    currentStatus = reader.GetString("status");
                    currentPrice = reader.GetDecimal("price");
                    currentBookedBy = reader.IsDBNull("booked_by") ? null : reader.GetString("booked_by");
                    currentReservedBy = reader.IsDBNull("reserved_by") ? null : reader.GetString("reserved_by");
                }

                _logger.LogInformation($"SESSION {sessionId}: 🔍 INITIAL READ - Seat A1 status: '{currentStatus}', booked_by: '{currentBookedBy ?? "null"}', reserved_by: '{currentReservedBy ?? "null"}', price: ${currentPrice}");

                if (currentStatus != "available")
                {
                    transaction.Rollback();
                    ViewBag.Message = $"❌ ERROR: Seat is {currentStatus}" + 
                        (currentBookedBy != null ? $" by {currentBookedBy}" : "") +
                        (currentReservedBy != null ? $" (reserved by {currentReservedBy})" : "");
                    
                    _logger.LogWarning($"SESSION {sessionId}: Seat not available for booking");
                    return View("Index");
                }

                // STEP 2: Mark as reserving (intermediate state)
                _logger.LogInformation($"SESSION {sessionId}: STEP 2 - Reserving seat (intermediate state)...");
                
                using (var reserveCmd = new NpgsqlCommand(
                    "UPDATE seats SET status = 'reserving', reserved_by = @sessionId, reserved_at = NOW() WHERE seat_id = 'A1' AND status = 'available'", 
                    connection, transaction))
                {
                    reserveCmd.Parameters.AddWithValue("sessionId", sessionId);
                    int reserveRows = reserveCmd.ExecuteNonQuery();
                    
                    if (reserveRows == 0)
                    {
                        transaction.Rollback();
                        ViewBag.Message = "❌ FAILED: Someone else reserved the seat during our check!";
                        _logger.LogWarning($"SESSION {sessionId}: Failed to reserve seat - someone else got it first");
                        return View("Index");
                    }
                    
                    _logger.LogInformation($"SESSION {sessionId}: ✅ Successfully marked seat as 'reserving'");
                }

                // STEP 3: Extended delay for concurrent testing (15 seconds)
                _logger.LogInformation($"SESSION {sessionId}: STEP 3 - Processing payment... ⏳ (15 second delay)");
                _logger.LogInformation($"SESSION {sessionId}: 🚨 DEBUG POINT: During this delay, try booking from another browser with different isolation levels!");
                
                Thread.Sleep(15000); // 15 seconds delay

                // STEP 4: Double-check seat status after delay (demonstrates isolation level behavior)
                _logger.LogInformation($"SESSION {sessionId}: STEP 4 - Double-checking seat status after payment processing...");
                
                using (var recheckCmd = new NpgsqlCommand("SELECT status, booked_by, reserved_by FROM seats WHERE seat_id = 'A1'", connection, transaction))
                using (var reader = recheckCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var recheckStatus = reader.GetString("status");
                        var recheckBookedBy = reader.IsDBNull("booked_by") ? null : reader.GetString("booked_by");
                        var recheckReservedBy = reader.IsDBNull("reserved_by") ? null : reader.GetString("reserved_by");
                        
                        _logger.LogInformation($"SESSION {sessionId}: 🔍 RECHECK READ - Seat A1 status: '{recheckStatus}', booked_by: '{recheckBookedBy ?? "null"}', reserved_by: '{recheckReservedBy ?? "null"}'");
                    }
                }

                // STEP 5: Final booking
                _logger.LogInformation($"SESSION {sessionId}: STEP 5 - Finalizing booking...");
                
                using (var bookCmd = new NpgsqlCommand(
                    "UPDATE seats SET status = 'booked', booked_by = @sessionId, booked_at = NOW(), reserved_by = NULL, reserved_at = NULL WHERE seat_id = 'A1' AND reserved_by = @sessionId", 
                    connection, transaction))
                {
                    bookCmd.Parameters.AddWithValue("sessionId", sessionId);
                    int bookRows = bookCmd.ExecuteNonQuery();
                    
                    if (bookRows == 0)
                    {
                        transaction.Rollback();
                        ViewBag.Message = "❌ FAILED: Booking conflict - seat reservation was lost!";
                        _logger.LogError($"SESSION {sessionId}: Failed to finalize booking - reservation was lost");
                        return View("Index");
                    }
                }

                // Commit the transaction
                transaction.Commit();
                ViewBag.Message = $"✅ SUCCESS: Seat A1 booked by session {sessionId}! (Isolation: {isolationLevel})";
                _logger.LogInformation($"SESSION {sessionId}: ✅ Successfully booked seat A1 with {isolationLevel} isolation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"SESSION {sessionId}: Booking failed with error: {ex.Message}");
                ViewBag.Message = $"❌ ERROR: {ex.Message}";
                
                // Additional debug info for isolation level issues
                if (ex.Message.Contains("serialization") || ex.Message.Contains("deadlock"))
                {
                    ViewBag.Message += $" | This is expected behavior for {isolationLevel} isolation level during concurrent access.";
                }
            }

            return View("Index");
        }

        // Reset demo - clear all bookings
        [HttpPost]
        public IActionResult Reset()
        {
            var sessionId = Guid.NewGuid().ToString()[..8];
            ViewBag.SessionId = sessionId;
            
            _logger.LogInformation($"=== SESSION {sessionId}: Resetting demo ===");

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var cmd = new NpgsqlCommand(
                    "UPDATE seats SET status = 'available', booked_by = NULL, reserved_by = NULL, booked_at = NULL, reserved_at = NULL WHERE seat_id = 'A1'", 
                    connection);
                
                cmd.ExecuteNonQuery();
                ViewBag.Message = "🔄 Demo reset! Seat A1 is now available.";
                _logger.LogInformation($"SESSION {sessionId}: Demo reset completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"SESSION {sessionId}: Reset failed: {ex.Message}");
                ViewBag.Message = $"❌ Reset failed: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // Get current seat status (for AJAX polling)
        public IActionResult GetSeatStatus()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var cmd = new NpgsqlCommand("SELECT status, booked_by, reserved_by, booked_at, reserved_at FROM seats WHERE seat_id = 'A1'", connection);
                using var reader = cmd.ExecuteReader();
                
                if (reader.Read())
                {
                    return Json(new
                    {
                        status = reader.GetString("status"),
                        bookedBy = reader.IsDBNull("booked_by") ? null : reader.GetString("booked_by"),
                        reservedBy = reader.IsDBNull("reserved_by") ? null : reader.GetString("reserved_by"),
                        bookedAt = reader.IsDBNull("booked_at") ? null : reader.GetDateTime("booked_at").ToString("HH:mm:ss"),
                        reservedAt = reader.IsDBNull("reserved_at") ? null : reader.GetDateTime("reserved_at").ToString("HH:mm:ss")
                    });
                }
                
                return Json(new { status = "not_found" });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
    }
}
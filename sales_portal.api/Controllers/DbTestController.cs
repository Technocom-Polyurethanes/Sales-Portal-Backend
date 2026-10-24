using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace sales_portal.api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DbTestController : ControllerBase
    {
        [HttpGet("ping")]
        public async Task<IActionResult> PingDatabase()
        {
            var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return BadRequest(new { connected = false, error = "DB_CONNECTION_STRING is empty or .env was not loaded." });
            }

            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                await using var cmd = new NpgsqlCommand("SELECT version(), current_database(), now();", conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();

                return Ok(new
                {
                    connected = true,
                    message = "Successfully connected to Aiven PostgreSQL!",
                    postgresVersion = reader.GetString(0),
                    databaseName = reader.GetString(1),
                    serverTimeUtc = reader.GetDateTime(2)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    errorType = ex.GetType().Name,
                    errorMessage = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }
        }
    }
}

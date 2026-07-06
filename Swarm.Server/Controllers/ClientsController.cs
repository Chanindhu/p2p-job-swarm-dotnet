using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swarm.Server.Data;
using Swarm.Server.Dtos;
using Swarm.Server.Entities;
using System.Net.Sockets;

namespace Swarm.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientsController : ControllerBase
    {
        private const int OnlineTtlSeconds = 60;

        private readonly AppDbContext _db;
        public ClientsController(AppDbContext db) => _db = db;

        // POST /api/clients/register
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (dto is null) return BadRequest("Body required.");
            if (string.IsNullOrWhiteSpace(dto.IpOrHost) || dto.Port <= 0)
                return BadRequest("IpOrHost and Port are required.");

            var host = NormalizeHost(dto.IpOrHost);

            var existing = await _db.Clients
                .SingleOrDefaultAsync(c => c.IpOrHost == host && c.Port == dto.Port);

            if (existing is null)
            {
                _db.Clients.Add(new Client
                {
                    IpOrHost = host,
                    Port = dto.Port,
                    DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? null : dto.DisplayName.Trim(),
                    LastSeenUtc = DateTime.UtcNow,
                    TotalJobsDone = 0
                });
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(dto.DisplayName))
                    existing.DisplayName = dto.DisplayName.Trim();

                existing.LastSeenUtc = DateTime.UtcNow;
            }

            try
            {
                await _db.SaveChangesAsync();
                return Ok(new { ok = true });
            }
            catch (DbUpdateException ex)
            {
                // Handles possible unique (IpOrHost, Port) race if you have an index
                return Conflict(new { ok = false, message = "Another client is already registered with this host/port.", detail = ex.Message });
            }
        }

        // POST /api/clients/heartbeat
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat([FromBody] HeartbeatDto dto)
        {
            if (dto is null) return BadRequest("Body required.");
            if (string.IsNullOrWhiteSpace(dto.IpOrHost) || dto.Port <= 0)
                return BadRequest("IpOrHost and Port are required.");

            var host = NormalizeHost(dto.IpOrHost);

            var existing = await _db.Clients
                .SingleOrDefaultAsync(c => c.IpOrHost == host && c.Port == dto.Port);

            if (existing is null)
            {
                // Heartbeat before register → auto-create (covers startup races)
                _db.Clients.Add(new Client
                {
                    IpOrHost = host,
                    Port = dto.Port,
                    DisplayName = null,
                    LastSeenUtc = DateTime.UtcNow,
                    TotalJobsDone = 0
                });
            }
            else
            {
                existing.LastSeenUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        // POST /api/clients/offline
        [HttpPost("offline")]
        public async Task<IActionResult> Offline([FromBody] OfflineDto dto)
        {
            if (dto is null) return BadRequest("Body required.");
            if (string.IsNullOrWhiteSpace(dto.IpOrHost) || dto.Port <= 0)
                return BadRequest("IpOrHost and Port are required.");

            var host = NormalizeHost(dto.IpOrHost);

            var existing = await _db.Clients
                .SingleOrDefaultAsync(c => c.IpOrHost == host && c.Port == dto.Port);

            if (existing is null)
                return Ok(new { ok = true }); // idempotent; nothing to do

            // Push last-seen far back so IsOnline becomes false immediately
            existing.LastSeenUtc = DateTime.UtcNow.AddHours(-1);
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        // GET /api/clients
        [HttpGet]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<ActionResult<IEnumerable<ClientDto>>> List()
        {
            Response.Headers.CacheControl = "no-store";

            var now = DateTime.UtcNow;
            var rows = await _db.Clients
                .AsNoTracking()
                .OrderByDescending(c => c.LastSeenUtc)
                .ToListAsync();

            var result = rows.Select(c => new ClientDto(
                c.Id,
                c.IpOrHost,
                c.Port,
                c.DisplayName,
                DateTime.SpecifyKind(c.LastSeenUtc, DateTimeKind.Utc),
                c.TotalJobsDone,
                (now - c.LastSeenUtc).TotalSeconds <= OnlineTtlSeconds // TTL = 60s
            ));

            return Ok(result);
        }

        private string NormalizeHost(string h)
        {
            if (string.IsNullOrWhiteSpace(h)) return "";
            if (string.Equals(h, "localhost", StringComparison.OrdinalIgnoreCase) || h == "127.0.0.1" || h == "::1")
            {
                var ip = HttpContext?.Connection?.RemoteIpAddress;
                if (ip != null)
                {
                    var v4 = ip.AddressFamily == AddressFamily.InterNetworkV6 ? ip.MapToIPv4() : ip;
                    return v4.ToString();
                }
            }
            return h.Trim();
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swarm.Server.Data;
using Swarm.Server.Dtos;
using Swarm.Server.Entities;

namespace Swarm.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public JobsController(AppDbContext db) => _db = db;

        // POST /api/jobs/complete
        [HttpPost("complete")]
        public async Task<IActionResult> Complete([FromBody] CompleteDto dto)
        {
            if (dto is null) return BadRequest("Body required.");
            if (dto.ClientId <= 0) return BadRequest("ClientId required.");
            if (string.IsNullOrWhiteSpace(dto.PythonB64)) return BadRequest("PythonB64 is required.");
            if (string.IsNullOrWhiteSpace(dto.Sha256Hex)) return BadRequest("Sha256Hex is required.");

            var client = await _db.Clients.SingleOrDefaultAsync(c => c.Id == dto.ClientId);
            if (client is null) return NotFound($"Client {dto.ClientId} not found.");

            var completion = new JobCompletion
            {
                ClientId = dto.ClientId,
                PythonB64 = dto.PythonB64.Trim(),
                Sha256Hex = dto.Sha256Hex.Trim(),
                ResultB64 = string.IsNullOrWhiteSpace(dto.ResultB64) ? null : dto.ResultB64.Trim(),
                OwnerClientId = dto.OwnerClientId,
                FinishedUtc = DateTime.UtcNow
            };

            _db.JobCompletions.Add(completion);
            client.TotalJobsDone += 1;

            await _db.SaveChangesAsync();
            return Ok(new { ok = true, completionId = completion.Id });
        }
    }
}

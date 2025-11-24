using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Контроллер управления сотрудниками партнера
/// Соответствует /api/v1/partner/employees из Python API
/// </summary>
[ApiController]
[Route("api/v1/partner/employees")]
[Tags("Partner Employees")]
[Authorize]
public class PartnerEmployeesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PartnerEmployeesController> _logger;

    public PartnerEmployeesController(
        ApplicationDbContext context,
        ILogger<PartnerEmployeesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Получить список сотрудников партнера
    /// GET /api/v1/partner/employees
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetEmployees()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "Неверный токен" });
            }

            var partner = await GetCurrentPartnerAsync(userId.Value);
            if (partner == null)
            {
                return Forbid("Пользователь не является партнером");
            }

            var employees = await _context.PartnerEmployees
                .Where(pe => pe.PartnerId == partner.Id)
                .Include(pe => pe.User)
                .Select(pe => new
                {
                    id = pe.Id,
                    user_id = pe.UserId,
                    partner_id = pe.PartnerId,
                    position = pe.Position,
                    phone = pe.User.Phone,
                    email = pe.User.Email,
                    first_name = pe.User.FirstName,
                    last_name = pe.User.LastName,
                    hired_at = pe.HiredAt
                })
                .ToListAsync();

            return Ok(new { data = employees });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения сотрудников");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Добавить сотрудника партнера
    /// POST /api/v1/partner/employees
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> CreateEmployee([FromBody] CreateEmployeeRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "Неверный токен" });
            }

            var partner = await GetCurrentPartnerAsync(userId.Value);
            if (partner == null)
            {
                return Forbid("Пользователь не является партнером");
            }

            // Найти пользователя по телефону или email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Phone == request.Phone || u.Email == request.Email);

            if (user == null)
            {
                return BadRequest(new { error = "Пользователь не найден" });
            }

            // Проверить, не является ли уже сотрудником
            var existingEmployee = await _context.PartnerEmployees
                .FirstOrDefaultAsync(pe => pe.PartnerId == partner.Id && pe.UserId == user.Id);

            if (existingEmployee != null)
            {
                return BadRequest(new { error = "Пользователь уже является сотрудником" });
            }

            var employee = new Domain.Entities.PartnerEmployee
            {
                PartnerId = partner.Id,
                UserId = user.Id,
                Position = request.Role ?? "employee",
                HiredAt = DateTime.UtcNow
            };

            _context.PartnerEmployees.Add(employee);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEmployees), new { }, new
            {
                data = new
                {
                    id = employee.Id,
                    user_id = employee.UserId,
                    partner_id = employee.PartnerId,
                    position = employee.Position
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания сотрудника");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Обновить сотрудника партнера
    /// PUT /api/v1/partner/employees/{id}
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> UpdateEmployee([FromRoute] int id, [FromBody] UpdateEmployeeRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "Неверный токен" });
            }

            var partner = await GetCurrentPartnerAsync(userId.Value);
            if (partner == null)
            {
                return Forbid("Пользователь не является партнером");
            }

            var employee = await _context.PartnerEmployees
                .FirstOrDefaultAsync(pe => pe.Id == id && pe.PartnerId == partner.Id);

            if (employee == null)
            {
                return NotFound(new { error = "Сотрудник не найден" });
            }

            if (!string.IsNullOrEmpty(request.Role))
            {
                employee.Position = request.Role;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                data = new
                {
                    id = employee.Id,
                    user_id = employee.UserId,
                    partner_id = employee.PartnerId,
                    position = employee.Position
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления сотрудника");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Удалить сотрудника партнера
    /// DELETE /api/v1/partner/employees/{id}
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteEmployee([FromRoute] int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "Неверный токен" });
            }

            var partner = await GetCurrentPartnerAsync(userId.Value);
            if (partner == null)
            {
                return Forbid("Пользователь не является партнером");
            }

            var employee = await _context.PartnerEmployees
                .FirstOrDefaultAsync(pe => pe.Id == id && pe.PartnerId == partner.Id);

            if (employee == null)
            {
                return NotFound(new { error = "Сотрудник не найден" });
            }

            _context.PartnerEmployees.Remove(employee);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Сотрудник удален" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления сотрудника");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }

    private async Task<Domain.Entities.Partner?> GetCurrentPartnerAsync(int userId)
    {
        var partnerEmployee = await _context.PartnerEmployees
            .FirstOrDefaultAsync(pe => pe.UserId == userId);

        if (partnerEmployee != null)
        {
            return await _context.Partners
                .FirstOrDefaultAsync(p => p.Id == partnerEmployee.PartnerId);
        }

        return await _context.Partners
            .FirstOrDefaultAsync(p => p.OwnerId == userId);
    }

    public class CreateEmployeeRequest
    {
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
    }

    public class UpdateEmployeeRequest
    {
        public string? Role { get; set; }
        public bool? IsActive { get; set; }
    }
}


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using YessBackend.Application.DTOs.Order;
using YessBackend.Application.Services;
using AutoMapper;

namespace YessBackend.Api.Controllers.v1;

[ApiController]
[Route("api/v1/orders")]
[Tags("Orders")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IMapper _mapper;
    private readonly ILogger<OrderController> _logger;

    public OrderController(IOrderService orderService, IMapper mapper, ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> CreateOrder([FromBody] OrderCreateRequestDto request)
    {
        // ЭТОТ ЛОГ МЫ УВИДИМ В DOCKER LOGS
        Console.WriteLine($"[DEBUG] CreateOrder called. PartnerId: {request.PartnerId}, Method: {request.PaymentMethod}");
        
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "Неверный токен" });

            var order = await _orderService.CreateOrderAsync(userId.Value, request);
            var response = _mapper.Map<OrderResponseDto>(order);

            return CreatedAtAction(nameof(GetOrder), new { order_id = order.Id }, response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Order creation failed: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{order_id}")]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(int order_id)
    {
        var userId = GetCurrentUserId();
        var order = await _orderService.GetOrderByIdAsync(order_id, userId);
        if (order == null) return NotFound();
        return Ok(_mapper.Map<OrderResponseDto>(order));
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

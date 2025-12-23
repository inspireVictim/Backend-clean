
using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using YessBackend.Application.DTOs.Auth;

using YessBackend.Application.Services;

using AutoMapper;

using Microsoft.Extensions.Configuration;

using System.Text;

using Microsoft.IdentityModel.Tokens;

using System.IdentityModel.Tokens.Jwt;

using System.Text.Json.Serialization;

using System.Security.Claims;



namespace YessBackend.Api.Controllers.v1;



[ApiController]

[Route("api/v1/auth")]

[Tags("Authentication")]

public class AuthController : ControllerBase

{

    private readonly IAuthService _authService;

    private readonly IMapper _mapper;

    private readonly ILogger<AuthController> _logger;

    private readonly IConfiguration _configuration;



    public AuthController(IAuthService authService, IMapper mapper, ILogger<AuthController> logger, IConfiguration configuration)

    {

        _authService = authService;

        _mapper = mapper;

        _logger = logger;

        _configuration = configuration;

    }



    [AllowAnonymous]

    [HttpPost("register")]

    public async Task<ActionResult<UserResponseDto>> Register([FromBody] UserRegisterDto registerDto)

    {

        try {

            var user = await _authService.RegisterUserAsync(registerDto);

            return Ok(_mapper.Map<UserResponseDto>(user));

        } catch (Exception ex) { return BadRequest(new { error = ex.Message }); }

    }



    [AllowAnonymous]

    [HttpPost("login")]

    [Consumes("application/json")]

    public async Task<ActionResult<TokenResponseDto>> Login([FromBody] UserLoginDto loginDto)

    {

        try {

            var tokenResponse = await _authService.LoginAsync(loginDto);

            return Ok(tokenResponse);

        } catch (Exception ex) { return Unauthorized(new { error = ex.Message }); }

    }



    [AllowAnonymous]

    [HttpPost("login/json")]

    [Consumes("application/json")]

    public async Task<ActionResult<TokenResponseDto>> LoginJson([FromBody] UserLoginDto loginDto) => await Login(loginDto);



    [AllowAnonymous]

    [HttpPost("refresh")]

    [Consumes("application/json")]

    public async Task<ActionResult<TokenResponseDto>> Refresh([FromBody] RefreshTokenRequestDto request)

    {

        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return BadRequest(new { error = "refresh_token is required" });

        try {

            var jwtSection = _configuration.GetSection("Jwt");

            var key = Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!);

            var tokenHandler = new JwtSecurityTokenHandler();

            var principal = tokenHandler.ValidateToken(request.RefreshToken, new TokenValidationParameters {

                ValidateIssuerSigningKey = true,

                IssuerSigningKey = new SymmetricSecurityKey(key),

                ValidateIssuer = true,

                ValidIssuer = jwtSection["Issuer"],

                ValidateAudience = true,

                ValidAudience = jwtSection["Audience"],

                ValidateLifetime = true,

                ClockSkew = TimeSpan.Zero

            }, out _);



            var phone = principal.FindFirst("phone")?.Value ?? principal.Identity?.Name;

            var user = await _authService.GetUserByPhoneAsync(phone!);

            if (user == null || !user.IsActive) return Unauthorized(new { error = "Unauthorized" });



            return Ok(new TokenResponseDto {

                AccessToken = _authService.CreateAccessToken(user),

                RefreshToken = _authService.CreateRefreshToken(user),

                TokenType = "bearer",

                ExpiresIn = jwtSection.GetValue<int>("AccessTokenExpireMinutes", 60) * 60

            });

        } catch { return Unauthorized(new { error = "Invalid refresh token" }); }

    }



    [HttpGet("me")]

    [Authorize]

    public async Task<ActionResult<UserResponseDto>> GetMe()

    {

        var userIdStr = User.FindFirst("user_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized(new { error = "Invalid token" });

        var user = await _authService.GetUserByIdAsync(int.Parse(userIdStr));

        return user != null ? Ok(_mapper.Map<UserResponseDto>(user)) : NotFound();

    }



    [AllowAnonymous]

    [HttpPost("send-verification-code")]

    [HttpPost("send-code")]

    public async Task<ActionResult> SendVerificationCode([FromBody] SendVerificationCodeRequestDto request)

    {

        try {

            var code = await _authService.SendVerificationCodeAsync(request.PhoneNumber);

            return Ok(new { phone_number = request.PhoneNumber, message = "Код отправлен", success = true, code = _configuration.GetValue<bool>("DevelopmentMode", true) ? code : null });

        } catch (Exception ex) { return BadRequest(new { error = ex.Message }); }

    }



    [AllowAnonymous]

    [HttpPost("verify-code")]

    public async Task<ActionResult<UserResponseDto>> VerifyCodeAndRegister([FromBody] VerifyCodeAndRegisterRequestDto request)

    {

        try {

            var user = await _authService.VerifyCodeAndRegisterAsync(request);

            return Ok(_mapper.Map<UserResponseDto>(user));

        } catch (Exception ex) { return BadRequest(new { error = ex.Message }); }

    }



    [HttpGet("referral-stats")]

    [Authorize]

    public async Task<ActionResult<ReferralStatsResponseDto>> GetReferralStats()

    {

        var userIdStr = User.FindFirst("user_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var stats = await _authService.GetReferralStatsAsync(int.Parse(userIdStr!));

        return Ok(stats);

    }



    [HttpPut("me")]

    [HttpPatch("me")]

    [Authorize]

    public async Task<ActionResult<UserResponseDto>> UpdateMe([FromBody] UpdateProfileRequestDto request)

    {

        var userIdStr = User.FindFirst("user_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var updatedUser = await _authService.UpdateUserAsync(userId, request);

        return updatedUser != null ? Ok(_mapper.Map<UserResponseDto>(updatedUser)) : NotFound();

    }



    public class RefreshTokenRequestDto { [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty; }

    public class SendVerificationCodeRequestDto { [JsonPropertyName("phone_number")] public string PhoneNumber { get; set; } = string.Empty; }

}


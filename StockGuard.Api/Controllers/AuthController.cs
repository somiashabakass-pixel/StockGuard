using StockGuard.Api.Filters;
using Microsoft.AspNetCore.Mvc;
using StockGuard.Api.Dtos.Auth;
using StockGuard.Api.Services.Auth;

namespace StockGuard.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly RegistrationService _registrationService;

    private readonly IWebHostEnvironment _environment;

    public AuthController(
        RegistrationService registrationService,
        IWebHostEnvironment environment)
    {
        _registrationService = registrationService;
        _environment = environment;
    }

    [DevelopmentOnly]
    [HttpPost("register")]
    [RequestSizeLimit(16_384)]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() &&
            !_environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        var result = await _registrationService.RegisterAsync(
            request,
            cancellationToken);

        if (result.Succeeded ||
            result.ErrorCode == "DuplicateAccount")
        {
            return Accepted(new
            {
                message = "Registration request received."
            });
        }

        if (result.ErrorCode == "InvalidRegistration")
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid registration information."
            });
        }

        throw new InvalidOperationException(
            "Unexpected registration result.");
    }
}
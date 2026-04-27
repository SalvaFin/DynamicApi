using Dynamic.Negocios.Application.Common;
using Dynamic.Negocios.Application.Contracts.Services;
using Dynamic.Negocios.Application.DTOs.Requests;
using Dynamic.Negocios.Application.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Negocios.Controllers;

[ApiController]
[Authorize(Policy = "AdminAuth")]
[Route("api/negocios")]
public class NegociosController : ControllerBase
{
    private readonly INegocioService _negocioService;

    public NegociosController(INegocioService negocioService)
    {
        _negocioService = negocioService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        ServiceResult<IReadOnlyCollection<NegocioResponse>> result = await _negocioService.GetAllAsync(cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        ServiceResult<NegocioResponse> result = await _negocioService.GetByIdAsync(id, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearNegocioRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<NegocioResponse> result = await _negocioService.CreateAsync(request, cancellationToken);
        return ToActionResult(result, data => StatusCode(StatusCodes.Status201Created, data));
    }

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(Guid id, [FromForm] ActualizarNegocioMultipartRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<NegocioResponse> result = await _negocioService.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        ServiceResult result = await _negocioService.DeleteAsync(id, cancellationToken);
        return ToActionResult(result, () => NoContent());
    }

    private IActionResult ToActionResult(ServiceResult result, Func<IActionResult> onSuccess)
    {
        if (result.Succeeded)
        {
            return onSuccess();
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.Succeeded && result.Data is not null)
        {
            return onSuccess(result.Data);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    private IActionResult MapFailure(string? errorCode, string? errorMessage)
        => errorCode switch
        {
            "validation_error" => BadRequest(new { message = errorMessage }),
            "conflict" => Conflict(new { message = errorMessage }),
            "not_found" => NotFound(new { message = errorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = errorMessage ?? "Error interno del servidor." })
        };
}

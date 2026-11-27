using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace StockGuard.Api.Filters;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = true)]
public sealed class DevelopmentOnlyAttribute
    : Attribute, IResourceFilter
{
    public void OnResourceExecuting(
        ResourceExecutingContext context)
    {
        var environment = context.HttpContext
            .RequestServices
            .GetRequiredService<IWebHostEnvironment>();

        if (!environment.IsDevelopment() &&
            !environment.IsEnvironment("Testing"))
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnResourceExecuted(
        ResourceExecutedContext context)
    {
    }
}
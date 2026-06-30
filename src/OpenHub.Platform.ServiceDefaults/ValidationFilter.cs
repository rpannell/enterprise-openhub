using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var validator = ctx.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
            return await next(ctx);

        var argument = ctx.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
            return await next(ctx);

        var result = await validator.ValidateAsync(argument, ctx.HttpContext.RequestAborted);
        if (!result.IsValid)
            return Results.ValidationProblem(result.ToDictionary());

        return await next(ctx);
    }
}

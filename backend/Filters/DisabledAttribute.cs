using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace backend.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class DisabledAttribute : ActionFilterAttribute
{
    private readonly bool _productionOnly;
    private readonly string _message;

    public DisabledAttribute(bool productionOnly = false, string message = "This endpoint is currently disabled.")
    {
        _message = message;
        _productionOnly = productionOnly;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (_productionOnly)
        {
            var env = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();

            if (!env.IsProduction())
            {
                base.OnActionExecuting(context);
                return;
            }
        }

        context.Result = new ObjectResult(new
        {
            message = _message
        })
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable
        };
    }
}
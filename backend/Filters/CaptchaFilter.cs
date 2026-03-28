using backend.Services;
using backend.Types.Request;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace backend.Filters;

public class CaptchaFilter : IAsyncActionFilter
{
    private readonly ICapValidateService _capService;

    public CaptchaFilter(ICapValidateService capService)
    {
        _capService = capService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isSkipped = context.ActionDescriptor.EndpointMetadata.Any(em => em is AllowAnonymousCaptchaAttribute);
        if (isSkipped)
        {
            await next();
            return;
        }

        var hasAttribute = context.ActionDescriptor.EndpointMetadata.Any(em => em is ValidateCaptchaAttribute)
                           || context.Controller.GetType().GetCustomAttributes(typeof(ValidateCaptchaAttribute), true)
                               .Any();

        if (hasAttribute)
        {
            var requestModel = context.ActionArguments.Values
                .OfType<ICaptchaRequest>()
                .FirstOrDefault();

            if (requestModel == null)
            {
                context.Result = new BadRequestObjectResult(new
                    { message = "Bad request: missing \"captcha_token\". Have you completed CAPTCHA?" });
                return;
            }

            var isValid = await _capService.ValidateAsync(requestModel);
            if (!isValid)
            {
                context.Result =
                    new BadRequestObjectResult(new { message = "CAPTCHA validation failed. Please try again." });
                return;
            }
        }

        await next();
    }
}
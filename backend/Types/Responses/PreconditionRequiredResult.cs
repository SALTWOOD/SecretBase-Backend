using Microsoft.AspNetCore.Mvc;

namespace backend.Types.Response;

public class PreconditionRequiredResult : ObjectResult
{
    public PreconditionRequiredResult(string type)
        : base(new
        {
            type = type
        })
    {
        StatusCode = StatusCodes.Status428PreconditionRequired;
    }
}
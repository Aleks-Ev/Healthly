using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HealthlyApi.Middleware;

// [Produces("application/json")] на контроллерах (нужен для OpenAPI) подменяет тип ответа и у ошибок.
// Этот фильтр выполняется последним и возвращает ошибкам из Problem()/ValidationProblem() правильный тип
// application/problem+json — такой же, как у ответов ExceptionHandlingMiddleware и UseStatusCodePages.
public class ProblemDetailsContentTypeFilter : IResultFilter, IOrderedFilter
{
    public int Order => int.MaxValue;

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: ProblemDetails } result)
        {
            result.ContentTypes.Clear();
            result.ContentTypes.Add("application/problem+json");
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}

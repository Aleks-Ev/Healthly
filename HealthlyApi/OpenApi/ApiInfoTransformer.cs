using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace HealthlyApi.OpenApi;

// Заголовок и краткая инструкция в начале документации Scalar (Markdown, на английском)
public class ApiInfoTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Healthly API",
            Version = "v1",
            Description = """
                REST API of the Healthly platform: patient accounts, medical documents, test results and health reports.

                **Authentication.** Call `POST /api/auth/login` (or `/api/auth/register`) and send the returned `token` on every
                other request as `Authorization: Bearer <token>`. The access token lives 15 minutes; when a request returns `401`,
                exchange the `refreshToken` (valid 30 days) for a new pair with `POST /api/auth/refresh` and save both new tokens.
                `POST /api/auth/logout` revokes the refresh token.
                In this UI, paste the token into the **Bearer Token** field on the right (without the word `Bearer`).

                **Roles.** `Patient` sees only their own data. `Doctor` and `Admin` can see every user's records;
                only `Admin` can create, delete or change the role of users.

                **Formats.** JSON with camelCase fields. Timestamps are UTC ISO 8601 (`2026-10-23T09:15:00Z`),
                dates of birth are `YYYY-MM-DD`.

                **Errors.** Every error is `application/problem+json` (RFC 9457 ProblemDetails):
                `{ "title": "Not Found", "status": 404, "detail": "User not found", "instance": "GET /api/users/42", "traceId": "..." }`.
                Show `detail` to the user. Validation errors (`400`) also contain `errors` with messages per field:
                `{ "errors": { "Email": ["..."] } }`. `500` hides internal details outside Development.
                """,
        };

        return Task.CompletedTask;
    }
}

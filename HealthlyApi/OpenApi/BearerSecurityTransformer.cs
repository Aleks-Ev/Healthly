using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace HealthlyApi.OpenApi;

// Добавляет в OpenAPI схему "Bearer" и помечает ею эндпоинты с [Authorize],
// чтобы в Scalar появилась кнопка авторизации и замочки у защищённых методов.
public class BearerSecurityTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    public const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token (JWT) from POST /api/auth/login or /api/auth/refresh",
        };

        return Task.CompletedTask;
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var requiresAuth = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();

        if (!requiresAuth)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
        });

        // 401 возможен у любого защищённого эндпоинта — добавляем автоматически, а не атрибутом на каждом методе
        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "The Authorization header is missing, or the access token is invalid or expired. Get a new one with POST /api/auth/refresh.",
        });

        // Эндпоинты с [Authorize(Roles = ...)] отвечают 403 пользователям с другой ролью
        var roles = metadata.OfType<IAuthorizeData>()
            .Where(a => !string.IsNullOrEmpty(a.Roles))
            .Select(a => a.Roles!.Replace(",", " or "))
            .LastOrDefault();
        if (roles != null)
        {
            var roleMessage = $"Your role is not allowed here. Requires: {roles}.";
            if (operation.Responses.TryGetValue("403", out var existing) && existing is OpenApiResponse response)
            {
                response.Description = $"{roleMessage} {response.Description}";
            }
            else
            {
                operation.Responses["403"] = new OpenApiResponse { Description = roleMessage };
            }
        }

        return Task.CompletedTask;
    }
}

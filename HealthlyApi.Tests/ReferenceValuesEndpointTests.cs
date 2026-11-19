using HealthlyApi.Dtos;
using HealthlyApi.Models;

namespace HealthlyApi.Tests;

// Ответ GET /api/reference-values: пустой ReviewedBy — черновик, псевдонимы — массив без пустых строк
public class ReferenceValuesEndpointTests
{
    [Fact]
    public void Row_without_a_reviewer_is_a_draft()
    {
        var response = ReferenceValueResponse.From(new ReferenceMedicalValue { MetricName = "Glucose", ReviewedBy = "  " });

        Assert.False(response.IsReviewed);
        Assert.Null(response.ReviewedBy);
    }

    [Fact]
    public void Reviewed_row_keeps_the_reviewer_and_splits_aliases()
    {
        var response = ReferenceValueResponse.From(new ReferenceMedicalValue
        {
            MetricName = "Hemoglobin",
            Aliases = "HGB| Hb ||Haemoglobin",
            ReviewedBy = "Dr. Example",
            ReviewedAt = new DateOnly(2026, 11, 1),
        });

        Assert.True(response.IsReviewed);
        Assert.Equal("Dr. Example", response.ReviewedBy);
        Assert.Equal(["HGB", "Hb", "Haemoglobin"], response.Aliases);
    }
}

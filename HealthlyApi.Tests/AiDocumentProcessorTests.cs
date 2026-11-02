using System.Net;
using System.Text;
using System.Text.Json;
using HealthlyApi.Models;
using HealthlyApi.Services.Ai;
using HealthlyApi.Services.Ai.Ocr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace HealthlyApi.Tests;

// Тесты ИИ-процессоров без настоящих запросов: HTTP подменяется FakeHandler, PDF генерируется в памяти
public class AiDocumentProcessorTests
{
    private const string PatientName = "Elif Kaya";

    private static readonly IOptions<AiOptions> Options = Microsoft.Extensions.Options.Options.Create(new AiOptions
    {
        Gemini = new AiProviderOptions { ApiKey = "gemini-test-key", Model = "gemini-test" },
        Groq = new AiProviderOptions { ApiKey = "groq-test-key", Model = "groq-test" },
    });

    private const string ModelJson =
        """{"indicators":[{"testName":"Glucose","value":"5.4","unit":"mmol/L","referenceRange":"3.9-5.6","confidence":0.97}],"summary":"A blood test.","recommendations":["Discuss the report with your doctor."]}""";

    [Fact]
    public async Task Falls_back_to_groq_when_gemini_is_rate_limited_and_sends_only_anonymized_text()
    {
        var gemini = new FakeHandler(HttpStatusCode.TooManyRequests, """{"error":{"status":"RESOURCE_EXHAUSTED"}}""");
        var groq = new FakeHandler(HttpStatusCode.OK, GroqResponse(ModelJson));
        var processor = CreateFallback(gemini, groq);

        await using var pdf = new MemoryStream(BuildLabReportPdf());
        var result = await processor.AnalyzeAsync(CreateInput(pdf), CancellationToken.None);

        Assert.Equal("groq:groq-test", result.Model);
        Assert.False(result.IsMock);
        var glucose = Assert.Single(result.Indicators);
        Assert.Equal("Glucose", glucose.TestName);
        Assert.Equal("5.4", glucose.Value);

        // Оба провайдера получили текст документа, но без персональных данных
        foreach (var body in new[] { gemini.LastRequestBody, groq.LastRequestBody })
        {
            Assert.NotNull(body);
            Assert.Contains("Glucose", body);
            Assert.DoesNotContain("Elif", body);
            Assert.DoesNotContain("Kaya", body);
            Assert.DoesNotContain("532", body);
        }
        Assert.DoesNotContain("Kaya", result.AnonymizedText);
    }

    [Fact]
    public async Task Uses_gemini_when_it_answers()
    {
        var gemini = new FakeHandler(HttpStatusCode.OK, GeminiResponse(ModelJson));
        var groq = new FakeHandler(HttpStatusCode.OK, GroqResponse(ModelJson));

        await using var pdf = new MemoryStream(BuildLabReportPdf());
        var result = await CreateFallback(gemini, groq).AnalyzeAsync(CreateInput(pdf), CancellationToken.None);

        Assert.Equal("gemini:gemini-test", result.Model);
        Assert.Null(groq.LastRequestBody);
    }

    [Fact]
    public async Task Returns_503_when_all_providers_fail()
    {
        var gemini = new FakeHandler(HttpStatusCode.InternalServerError, "{}");
        var groq = new FakeHandler(HttpStatusCode.OK, GroqResponse("not json"));

        await using var pdf = new MemoryStream(BuildLabReportPdf());
        var exception = await Assert.ThrowsAsync<AiAnalysisException>(
            () => CreateFallback(gemini, groq).AnalyzeAsync(CreateInput(pdf), CancellationToken.None));

        Assert.Equal(503, exception.StatusCode);
    }

    [Fact]
    public async Task Photo_is_recognized_once_even_when_falling_back()
    {
        var gemini = new FakeHandler(HttpStatusCode.ServiceUnavailable, "{}");
        var groq = new FakeHandler(HttpStatusCode.OK, GroqResponse(ModelJson));
        var ocr = new FakeOcr(new OcrResult("Patient: E1if Kaya\nGlucose 5.4 mmol/L 3.9-5.6", 91, []));

        await using var photo = new MemoryStream(TestImages.Png(["any picture"]));
        var result = await CreateFallback(gemini, groq, ocr).AnalyzeAsync(CreateInput(photo, "image/png"), CancellationToken.None);

        Assert.Equal("groq:groq-test", result.Model);
        Assert.Equal(1, ocr.Calls); // результат OCR запомнен на время запроса
        // Ошибка OCR в имени («E1if») не помогает имени утечь
        Assert.DoesNotContain("E1if", groq.LastRequestBody);
        Assert.DoesNotContain("Kaya", groq.LastRequestBody);
    }

    [Theory]
    [InlineData("Glucose 5.4 mmol/L 3.9-5.6", 30)]   // уверенность ниже порога — размытое фото
    [InlineData("~ ,. ::", 95)]                        // почти нет букв и цифр
    public async Task Unreadable_photo_is_rejected_with_422_and_never_sent(string text, double confidence)
    {
        var gemini = new FakeHandler(HttpStatusCode.OK, GeminiResponse(ModelJson));
        var groq = new FakeHandler(HttpStatusCode.OK, GroqResponse(ModelJson));

        await using var photo = new MemoryStream(TestImages.Png(["any picture"]));
        var exception = await Assert.ThrowsAsync<AiAnalysisException>(() => CreateFallback(gemini, groq, new FakeOcr(new OcrResult(text, confidence, [])))
            .AnalyzeAsync(CreateInput(photo, "image/jpeg"), CancellationToken.None));

        Assert.Equal(422, exception.StatusCode);
        Assert.Null(gemini.LastRequestBody);
        Assert.Null(groq.LastRequestBody);
    }

    [Fact]
    public async Task Broken_image_is_rejected_with_422()
    {
        await using var image = new MemoryStream([0xFF, 0xD8, 0xFF]);
        var exception = await Assert.ThrowsAsync<AiAnalysisException>(() => CreateFallback(
                new FakeHandler(HttpStatusCode.OK, "{}"), new FakeHandler(HttpStatusCode.OK, "{}"))
            .AnalyzeAsync(CreateInput(image, "image/jpeg"), CancellationToken.None));

        Assert.Equal(422, exception.StatusCode);
    }

    [Fact]
    public async Task Values_that_ocr_read_uncertainly_get_low_confidence_even_if_the_model_is_sure()
    {
        // OCR прочитал «7.1» как «7а» (кириллица) с низкой уверенностью; модель вернула «7a» латиницей и confidence 0.9
        const string modelJson =
            """{"indicators":[{"testName":"Glucose","value":"5.4","unit":"mmol/L","confidence":0.97},{"testName":"Leukocytes","value":"7a","unit":"10^9/L","confidence":0.9}],"summary":"A blood test."}""";
        var ocr = new FakeOcr(new OcrResult("Glucose 5.4 mmol/L 3.9-5.6\nLeukocytes 7а 10^9/L 4.0-9.0", 88, ["7а"]));

        await using var photo = new MemoryStream(TestImages.Png(["any picture"]));
        var result = await CreateFallback(new FakeHandler(HttpStatusCode.OK, GeminiResponse(modelJson)), new FakeHandler(HttpStatusCode.OK, "{}"), ocr)
            .AnalyzeAsync(CreateInput(photo, "image/png"), CancellationToken.None);

        Assert.Equal(0.97, result.Indicators.Single(i => i.TestName == "Glucose").Confidence);
        Assert.Equal(0.4, result.Indicators.Single(i => i.TestName == "Leukocytes").Confidence);
    }

    [Fact]
    public void Tesseract_tsv_is_joined_into_lines_with_average_confidence()
    {
        const string tsv = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n" +
                           "4\t1\t1\t1\t1\t0\t0\t0\t0\t0\t-1\t\n" +
                           "5\t1\t1\t1\t1\t1\t0\t0\t0\t0\t90\tGlucose\n" +
                           "5\t1\t1\t1\t1\t2\t0\t0\t0\t0\t80\t5.4\n" +
                           "5\t1\t1\t1\t2\t1\t0\t0\t0\t0\t70\tHemoglobin\n";

        var result = TesseractCliOcrEngine.ParseTsv(tsv, uncertainBelow: 75);

        Assert.Equal("Glucose 5.4\nHemoglobin", result.Text);
        Assert.Equal(80, result.Confidence);
        Assert.Equal(["Hemoglobin"], result.UncertainWords);
    }

    [Fact]
    public void Parses_numbers_code_fences_and_clamps_confidence()
    {
        var (indicators, summary, _) = LlmDocumentProcessor.ParseResponse(
            "```json\n{\"indicators\":[{\"testName\":\"Hemoglobin\",\"value\":135,\"unit\":\"g/L\",\"confidence\":7},{\"testName\":\"\",\"value\":\"1\"}],\"summary\":\"CBC.\"}\n```",
            "test");

        var hemoglobin = Assert.Single(indicators); // показатель без названия отброшен
        Assert.Equal("135", hemoglobin.Value);
        Assert.Equal(string.Empty, hemoglobin.ReferenceRange);
        Assert.Equal(1, hemoglobin.Confidence);
        Assert.Equal("CBC.", summary);
    }

    [Fact]
    public void Invalid_json_is_a_provider_failure()
    {
        Assert.Throws<AiProviderException>(() => LlmDocumentProcessor.ParseResponse("Sorry, I cannot help.", "test"));
    }

    private static FallbackAiDocumentProcessor CreateFallback(FakeHandler gemini, FakeHandler groq, IOcrEngine? ocr = null)
    {
        // Один экстрактор на «запрос» — как Scoped в Program.cs
        var extractor = new DocumentTextExtractor(ocr ?? new FakeOcr(new OcrResult("", 0, [])), OcrTestOptions.Default, NullLogger<DocumentTextExtractor>.Instance);
        return new FallbackAiDocumentProcessor(
            [new GeminiAiDocumentProcessor(new HttpClient(gemini), Options, extractor), new GroqAiDocumentProcessor(new HttpClient(groq), Options, extractor)],
            NullLogger<FallbackAiDocumentProcessor>.Instance);
    }

    private static AiDocumentInput CreateInput(Stream content, string contentType = "application/pdf") => new(
        1,
        "report.pdf",
        contentType,
        content,
        new AiPatientContext(PatientName, "Female", 33),
        [new ReferenceMedicalValue { MetricName = "Glucose", Unit = "mmol/L", MinNormal = 3.9m, MaxNormal = 5.6m }]);

    // Бланк анализа с персональными данными — как настоящий PDF из лаборатории
    private static byte[] BuildLabReportPdf()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        string[] lines =
        [
            "CITY LAB - BIOCHEMISTRY",
            $"Patient: {PatientName}",
            "Phone: +90 532 123 45 67",
            "Glucose 5.4 mmol/L 3.9-5.6",
        ];
        for (var i = 0; i < lines.Length; i++)
        {
            page.AddText(lines[i], 12, new UglyToad.PdfPig.Core.PdfPoint(50, 780 - i * 20), font);
        }
        return builder.Build();
    }

    private static string GeminiResponse(string text) => JsonSerializer.Serialize(new
    {
        candidates = new[] { new { content = new { parts = new[] { new { text } } }, finishReason = "STOP" } },
    });

    private static string GroqResponse(string content) => JsonSerializer.Serialize(new
    {
        choices = new[] { new { message = new { content }, finish_reason = "stop" } },
    });

    private sealed class FakeOcr(OcrResult result) : IOcrEngine
    {
        public int Calls { get; private set; }

        public Task<OcrResult> RecognizeAsync(byte[] png, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}

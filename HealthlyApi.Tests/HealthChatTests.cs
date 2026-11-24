using HealthlyApi.Models;
using HealthlyApi.Services;
using HealthlyApi.Services.Ai;
using HealthlyApi.Services.Ai.Chat;
using Microsoft.Extensions.Logging.Abstractions;

namespace HealthlyApi.Tests;

// ИИ-чат: что уходит в модель (без PII), что подставляется в контекст, переключение провайдеров, лимиты, заглушка
public class HealthChatPrivacyTests
{
    // Выдуманный пациент: всё, что не должно попасть в LLM
    private static readonly User Patient = new()
    {
        Id = 31,
        FullName = "Elif Kaya",
        Email = "elif.kaya@example.com",
        PhoneNumber = "+905321112233",
        DateOfBirth = new DateOnly(1992, 4, 18),
        City = "Konya",
        PatientCode = "HL-7K3P9Q",
        Gender = Genders.Female,
        Height = 165,
        Weight = 60,
        Role = Roles.Patient,
    };

    private static readonly TestResult[] ResultsNewestFirst =
    [
        new() { TestName = "Glucose", Value = "6.1", Unit = "mmol/L", ReferenceRange = "3.9-5.6", IsAbnormal = true },
        new() { TestName = "Potassium", Value = "6.8", Unit = "mmol/L", ReferenceRange = "3.5-5.1", IsAbnormal = true, IsCritical = true },
        new() { TestName = "glucose", Value = "5.0", Unit = "mmol/L", ReferenceRange = "3.9-5.6" }, // старое значение — не берём
        new() { TestName = "Hemoglobin", Value = "131", Unit = "g/L", ReferenceRange = "120-155" },
    ];

    private static readonly Laboratory[] Laboratories =
    [
        new() { Name = "Bosphorus Diagnostics", City = "Istanbul", Address = "Barbaros Blvd. 45", OpeningHours = "Mon–Fri 07:00–19:00" },
        new() { Name = "Closed Lab", City = "Istanbul", AcceptingPatients = false },
    ];

    private static readonly User[] Doctors =
    [
        new() { FullName = "Dr. Ayşe Demir", Role = Roles.Doctor, Specialty = "Endocrinologist", Workplace = "Moda Medical Center", City = "Istanbul" },
        new() { FullName = "Dr. Busy Doctor", Role = Roles.Doctor, AcceptingPatients = false },
        new() { FullName = "Not A Doctor", Role = Roles.Patient },
    ];

    private static HealthChatContext Context() => HealthChatContextBuilder.Build(Patient, ResultsNewestFirst, Laboratories, Doctors);

    private static HealthChatPrompt.PatientIdentity Identity() => new(Patient.FullName, Patient.Email, Patient.PatientCode);

    // Всё, что видит провайдер: системный промпт и все реплики
    private static string Outgoing(ChatCompletionRequest request) =>
        request.SystemPrompt + "\n" + string.Join("\n", request.Messages.Select(m => m.Content));

    [Fact]
    public void Nothing_identifying_the_patient_reaches_the_model()
    {
        var history = new List<ChatTurn>
        {
            new(ChatRoles.User, "Hi, I'm Elif Kaya, my email is elif.kaya@example.com"),
            new(ChatRoles.Assistant, "Hello! How can I help?"),
        };
        var request = HealthChatPrompt.Build(Context(), history,
            "Call me at +90 532 111 22 33. My Healthly ID is HL-7K3P9Q and I was born 18.04.1992. — Elif", Identity());

        var outgoing = Outgoing(request);
        Assert.DoesNotContain("Elif", outgoing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kaya", outgoing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("elif.kaya@example.com", outgoing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("532", outgoing);
        Assert.DoesNotContain("7K3P9Q", outgoing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1992", outgoing);
        Assert.DoesNotContain("Konya", outgoing, StringComparison.OrdinalIgnoreCase); // город пациента
    }

    [Fact]
    public void Profile_contains_only_sex_age_height_and_weight()
    {
        var context = Context();
        var section = HealthChatPrompt.BuildPatientSection(context, Identity());

        Assert.Equal(DocumentAnalysisService.GetAge(Patient.DateOfBirth), context.Profile.Age);
        Assert.Contains("Sex: Female", section);
        Assert.Contains($"Age: {context.Profile.Age} years", section);
        Assert.Contains("Height: 165 cm", section);
        Assert.Contains("Weight: 60 kg", section);
        Assert.Contains("Blood type: not provided", section);
    }

    [Fact]
    public void Latest_confirmed_results_are_in_the_context_with_their_status()
    {
        var context = Context();
        var section = HealthChatPrompt.BuildPatientSection(context, Identity());

        Assert.Equal(3, context.Results.Count); // одна строка на показатель — последняя
        Assert.Contains("Glucose: 6.1 mmol/L (reference 3.9-5.6) — out of range", section);
        Assert.Contains("Potassium: 6.8 mmol/L (reference 3.5-5.1) — CRITICAL", section);
        Assert.Contains("Hemoglobin: 131 g/L (reference 120-155) — within range", section);
        Assert.DoesNotContain("5.0 mmol/L", section);
    }

    [Fact]
    public void Partner_laboratories_and_doctors_are_in_the_system_prompt()
    {
        var request = HealthChatPrompt.Build(Context(), [], "Which labs are in Istanbul?", Identity());

        Assert.Contains("Bosphorus Diagnostics — Istanbul (Barbaros Blvd. 45, Mon–Fri 07:00–19:00)", request.SystemPrompt);
        Assert.Contains("Dr. Ayşe Demir — Endocrinologist, Moda Medical Center, Istanbul", request.SystemPrompt);
        // Не принимают пациентов / не врачи — не предлагаются
        Assert.DoesNotContain("Closed Lab", request.SystemPrompt);
        Assert.DoesNotContain("Dr. Busy Doctor", request.SystemPrompt);
        Assert.DoesNotContain("Not A Doctor", request.SystemPrompt);
    }

    [Fact]
    public void Doctor_names_from_the_directory_survive_anonymization_in_messages()
    {
        var request = HealthChatPrompt.Build(Context(), [], "Elif here — can I book Dr. Ayşe Demir?", Identity());

        var message = request.Messages[^1].Content;
        Assert.Contains("Dr. Ayşe Demir", message);
        Assert.DoesNotContain("Elif", message);
        Assert.Contains(PiiAnonymizer.NameMask, message);
    }

    [Fact]
    public void Only_the_last_messages_are_sent()
    {
        var history = Enumerable.Range(1, 30)
            .Select(i => new ChatTurn(i % 2 == 1 ? ChatRoles.User : ChatRoles.Assistant, $"message {i}"))
            .ToList();

        var request = HealthChatPrompt.Build(Context(), history, "newest", Identity());

        // Последние 11 реплик истории начинаются с ответа ассистента — он отбрасывается, окно начинается с вопроса пользователя
        Assert.Equal(HealthChatPrompt.HistoryWindow - 1, request.Messages.Count);
        Assert.Equal(ChatRoles.User, request.Messages[0].Role);
        Assert.Equal("newest", request.Messages[^1].Content);
        Assert.Equal(ChatRoles.User, request.Messages[^1].Role);
        Assert.DoesNotContain(request.Messages, m => m.Content == "message 1");
    }

    [Fact]
    public void Prompt_has_the_ten_off_topic_replies_and_the_medical_rules()
    {
        var prompt = HealthChatPrompt.Build(Context(), [], "hi", Identity()).SystemPrompt;

        Assert.Equal(10, HealthChatPrompt.OffTopicReplies.Count);
        Assert.All(HealthChatPrompt.OffTopicReplies.Values, reply => Assert.Contains(reply, prompt));
        Assert.Contains("FDA", prompt);
        Assert.Contains("TİTCK", prompt);
        Assert.Contains("WHO", prompt);
        Assert.Contains("112", prompt);
        Assert.Contains("Never diagnose", prompt);
    }
}

public class ChatFallbackTests
{
    private static readonly ChatCompletionRequest Request = new("system", [new ChatTurn(ChatRoles.User, "hi")],
        new HealthChatContext(new ChatPatientProfile(null, null, null, null, null), [], [], []));

    private sealed class FakeClient(Func<ChatCompletionResult> answer) : IChatCompletionClient
    {
        public int Calls { get; private set; }

        public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(answer());
        }
    }

    private static FallbackChatClient Chain(params IChatCompletionClient[] clients) =>
        new(clients, NullLogger<FallbackChatClient>.Instance);

    [Fact]
    public async Task Gemini_failure_falls_back_to_groq()
    {
        var gemini = new FakeClient(() => throw new AiProviderException("gemini: HTTP 429"));
        var groq = new FakeClient(() => new ChatCompletionResult("Hello from Groq", "groq:model", false));

        var result = await Chain(gemini, groq).CompleteAsync(Request, CancellationToken.None);

        Assert.Equal("Hello from Groq", result.Content);
        Assert.Equal("groq:model", result.Model);
        Assert.Equal(1, gemini.Calls);
        Assert.Equal(1, groq.Calls);
    }

    [Fact]
    public async Task First_working_provider_answers_and_the_rest_are_not_called()
    {
        var gemini = new FakeClient(() => new ChatCompletionResult("Hello from Gemini", "gemini:model", false));
        var groq = new FakeClient(() => new ChatCompletionResult("unused", "groq:model", false));

        var result = await Chain(gemini, groq).CompleteAsync(Request, CancellationToken.None);

        Assert.Equal("gemini:model", result.Model);
        Assert.Equal(0, groq.Calls);
    }

    [Fact]
    public async Task All_providers_down_is_a_503()
    {
        var gemini = new FakeClient(() => throw new AiProviderException("gemini: timeout"));
        var groq = new FakeClient(() => throw new AiProviderException("groq: HTTP 500"));

        var error = await Assert.ThrowsAsync<AiAnalysisException>(() => Chain(gemini, groq).CompleteAsync(Request, CancellationToken.None));

        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    public async Task Cancellation_is_not_treated_as_a_provider_failure()
    {
        var gemini = new FakeClient(() => throw new OperationCanceledException());
        var groq = new FakeClient(() => new ChatCompletionResult("unused", "groq:model", false));

        await Assert.ThrowsAsync<OperationCanceledException>(() => Chain(gemini, groq).CompleteAsync(Request, CancellationToken.None));
        Assert.Equal(0, groq.Calls);
    }
}

public class ChatRateLimitTests
{
    [Fact]
    public async Task Eleventh_message_in_a_minute_is_rejected()
    {
        using var limiter = RateLimiting.CreateChatLimiter();

        for (var i = 0; i < RateLimiting.ChatPerMinute; i++)
        {
            using var lease = await limiter.AcquireAsync();
            Assert.True(lease.IsAcquired);
        }

        using var rejected = await limiter.AcquireAsync();
        Assert.False(rejected.IsAcquired);
    }

    [Fact]
    public async Task Hourly_limit_applies_even_when_the_minute_window_has_room()
    {
        using var limiter = RateLimiting.CreateChatLimiter(perMinute: 100, perHour: 3);

        for (var i = 0; i < 3; i++)
        {
            using var lease = await limiter.AcquireAsync();
            Assert.True(lease.IsAcquired);
        }

        using var rejected = await limiter.AcquireAsync();
        Assert.False(rejected.IsAcquired);
    }

    [Fact]
    public async Task Minute_window_frees_up_again()
    {
        using var limiter = RateLimiting.CreateChatLimiter(perMinute: 1, perHour: 100, minuteWindow: TimeSpan.FromMilliseconds(150));

        using (var first = await limiter.AcquireAsync())
        {
            Assert.True(first.IsAcquired);
        }
        using (var second = await limiter.AcquireAsync())
        {
            Assert.False(second.IsAcquired);
        }

        // Окно освобождает таймер ограничителя; под нагрузкой (параллельные тесты с PBKDF2) он срабатывает позже 150 мс,
        // поэтому ждём освобождения до 5 с, а не фиксированные 400 мс — иначе тест «мигал»
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var acquired = false;
        while (!acquired && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            using var third = await limiter.AcquireAsync();
            acquired = third.IsAcquired;
        }
        Assert.True(acquired);
    }
}

public class MockChatTests
{
    private static readonly HealthChatContext Context = new(
        new ChatPatientProfile("Female", 34, 165, 60, null),
        [new ChatLabResult("Glucose", "6.1", "mmol/L", "3.9-5.6", IsAbnormal: true, IsCritical: false)],
        [new ChatLaboratoryInfo("Bosphorus Diagnostics", "Istanbul", "Barbaros Blvd. 45", ""), new ChatLaboratoryInfo("Aegean Lab", "Izmir", "", "")],
        [new ChatDoctorInfo("Dr. Ayşe Demir", "Endocrinologist", "", "Istanbul")]);

    [Theory]
    [InlineData("Write a python script that sorts a list", "programming")]
    [InlineData("Give me a recipe for chocolate cake", "cooking")]
    [InlineData("Who will win the election?", "politics")]
    [InlineData("Recommend a good movie", "entertainment")]
    [InlineData("I have chest pain and can't breathe", "emergency")]
    [InlineData("I burned my hand on the stove", "symptom")]
    [InlineData("How many calories should I eat?", "nutrition")]
    [InlineData("What a great day", "other")] // «eat» внутри «great» — не питание
    [InlineData("Which labs are in Istanbul?", "directory")]
    [InlineData("How do I upload my results?", "platform")]
    public void Classifies_requests(string message, string expected)
    {
        Assert.Equal(expected, MockChatClient.Classify(message));
    }

    [Fact]
    public void Off_topic_request_gets_a_predefined_refusal()
    {
        Assert.Equal(HealthChatPrompt.OffTopicReplies["programming"], MockChatClient.Reply("Fix this JavaScript bug", Context));
    }

    [Fact]
    public void Red_flags_send_to_the_emergency_department()
    {
        var reply = MockChatClient.Reply("My father has chest pain", Context);
        Assert.Contains("112", reply);
        Assert.Contains("Emergency Department", reply);
    }

    [Fact]
    public void Burn_advice_says_what_not_to_do()
    {
        var reply = MockChatClient.Reply("I burned my arm", Context);
        Assert.Contains("Don't put ice, butter, oil or toothpaste", reply);
    }

    [Fact]
    public void Nutrition_uses_the_anonymized_profile_and_flags_abnormal_results()
    {
        var reply = MockChatClient.Reply("How much protein should I eat?", Context);
        Assert.Contains("1300 kcal/day", reply); // Mifflin–St Jeor: 10·60 + 6.25·165 − 5·34 − 161 = 1300.25
        Assert.Contains("48 g/day", reply);
        Assert.Contains("Glucose", reply);
    }

    [Fact]
    public void Directory_answer_lists_partners_in_the_asked_city()
    {
        var reply = MockChatClient.Reply("Which labs are in Istanbul?", Context);
        Assert.Contains("Bosphorus Diagnostics", reply);
        Assert.Contains("Dr. Ayşe Demir", reply);
        Assert.DoesNotContain("Aegean Lab", reply);
    }
}

// HTTP-клиенты чата: формат запроса к провайдеру и обработка оборванных ответов
public class ProviderChatClientTests
{
    private static readonly Microsoft.Extensions.Options.IOptions<AiOptions> Options = Microsoft.Extensions.Options.Options.Create(new AiOptions
    {
        Gemini = new AiProviderOptions { ApiKey = "test-key", Model = "gemini-test" },
        Groq = new AiProviderOptions { ApiKey = "test-key", Model = "groq-test" },
    });

    private static readonly ChatCompletionRequest Request = new("system rules",
        [new ChatTurn(ChatRoles.User, "hello"), new ChatTurn(ChatRoles.Assistant, "hi!"), new ChatTurn(ChatRoles.User, "my question")],
        new HealthChatContext(new ChatPatientProfile(null, null, null, null, null), [], [], []));

    private sealed class FakeHandler(string response) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(response) };
        }
    }

    [Fact]
    public async Task Gemini_gets_the_history_with_its_own_role_names()
    {
        var handler = new FakeHandler("""{"candidates":[{"content":{"parts":[{"text":"Answer"}]},"finishReason":"STOP"}]}""");
        var client = new GeminiChatClient(new HttpClient(handler), Options);

        var result = await client.CompleteAsync(Request, CancellationToken.None);

        Assert.Equal("Answer", result.Content);
        Assert.Equal("gemini:gemini-test", result.Model);
        Assert.Contains("\"role\":\"model\"", handler.RequestBody);
        Assert.DoesNotContain("\"role\":\"assistant\"", handler.RequestBody);
        Assert.Contains("system rules", handler.RequestBody);
    }

    [Fact]
    public async Task Cut_off_gemini_answer_is_a_provider_failure()
    {
        var handler = new FakeHandler("""{"candidates":[{"content":{"parts":[{"text":"**Sodium (400 mmol/L"}]},"finishReason":"MAX_TOKENS"}]}""");
        var client = new GeminiChatClient(new HttpClient(handler), Options);

        await Assert.ThrowsAsync<AiProviderException>(() => client.CompleteAsync(Request, CancellationToken.None));
    }

    [Fact]
    public async Task Cut_off_groq_answer_is_a_provider_failure()
    {
        var handler = new FakeHandler("""{"choices":[{"message":{"content":"Half an ans"},"finish_reason":"length"}]}""");
        var client = new GroqChatClient(new HttpClient(handler), Options);

        await Assert.ThrowsAsync<AiProviderException>(() => client.CompleteAsync(Request, CancellationToken.None));
    }

    [Fact]
    public async Task Groq_gets_a_system_message_and_the_history()
    {
        var handler = new FakeHandler("""{"choices":[{"message":{"content":"Answer"},"finish_reason":"stop"}]}""");
        var client = new GroqChatClient(new HttpClient(handler), Options);

        var result = await client.CompleteAsync(Request, CancellationToken.None);

        Assert.Equal("Answer", result.Content);
        Assert.Contains("\"role\":\"system\"", handler.RequestBody);
        Assert.Contains("\"role\":\"assistant\"", handler.RequestBody);
        Assert.Contains("my question", handler.RequestBody);
    }
}

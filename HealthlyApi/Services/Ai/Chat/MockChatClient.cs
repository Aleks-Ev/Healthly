using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HealthlyApi.Models;

namespace HealthlyApi.Services.Ai.Chat;

// Заглушка чата без внешнего ИИ — когда в конфигурации нет ни одного ключа (Ai:Gemini:ApiKey / Ai:Groq:ApiKey).
// Нужна, чтобы проект работал у всех: отвечает по тем же правилам, что и промпт, но по простым ключевым словам (EN/TR):
//   красные флаги → «звоните 112 / Acil Servis»; «сыграй врача / поставь диагноз» → отказ от роли и триаж;
//   лекарства и дозы → «решает врач или фармацевт»; симптом (и «народное средство») → уточняющие вопросы и профильный врач;
//   питание → оценка калорий по Mifflin–St Jeor из обезличенного профиля; результаты → сводка отклонений;
//   платформа → как загрузить, Healthly ID, команда здоровья, лаборатории и врачи из каталога; иначе — один из 10 отказов.
// Ответы помечаются IsMock = true, сайт показывает бейдж «Demo mode».
public class MockChatClient : IChatCompletionClient
{
    public const string ModelName = "mock-chat-v1";

    public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        var message = request.Messages.LastOrDefault(m => m.Role == ChatRoles.User)?.Content ?? string.Empty;
        var reply = Reply(message, request.Context);
        return Task.FromResult(new ChatCompletionResult(reply, ModelName, IsMock: true));
    }

    // Тип запроса по ключевым словам — public для тестов
    public static string Classify(string message)
    {
        var text = message.ToLower(CultureInfo.InvariantCulture);

        if (ContainsAny(text, "chest pain", "can't breathe", "cannot breathe", "difficulty breathing", "unconscious", "fainted", "stroke",
                "severe bleeding", "suicid", "kill myself", "overdose", "poison", "göğüs ağrısı", "nefes alamıyorum", "bayıldı", "intihar", "zehirlen"))
        {
            return "emergency";
        }

        // «Притворись моим врачом», «поставь диагноз» — роль врача не берём (раньше симптомов: «diagnose my headache»)
        if (ContainsAny(text, "pretend", "act as", "role play", "roleplay", "be my doctor", "you are my doctor", "you are a doctor",
                "diagnos", "teşhis", "tanı koy", "doktorum ol", "doktor gibi"))
        {
            return "roleplay";
        }

        // Лекарства и дозы — никаких названий и дозировок, только «к врачу или фармацевту»
        if (ContainsAny(text, "dose", "dosage", "how many mg", "mg of", "antibiotic", "prescri", "painkiller", "ibuprofen", "paracetamol",
                "amoxicillin", "insulin", "pill ", "pills", "medicine", "medication", "doz", "antibiyotik", "ilaç", "reçete", "ağrı kesici"))
        {
            return "medication";
        }

        if (ContainsAny(text, "folk", "home remed", "natural remed", "herbal", "remedy", "kocakarı", "bitkisel", "doğal tedavi",
                "burn", "pain", "hurts", "ache", "fever", "cough", "rash", "nausea", "vomit", "diarrh", "dizzy", "headache",
                "injur", "bleed", "swelling", "yanık", "ağrı", "ateş", "öksürük", "kaşıntı", "bulantı", "kusma", "baş dönmesi", "şişlik"))
        {
            return "symptom";
        }

        // Вопрос о сервисе («upload my results») — раньше, чем «результаты» и «питание»
        if (ContainsAny(text, "upload", "healthly id", "password", "chart", "trend", "care team", "share", "sharing", "account", "the app",
                "change my doctor", "yükle", "şifre", "grafik", "hesap"))
        {
            return "platform";
        }

        // Диетический рецепт — это питание, а не «кулинария»
        if (ContainsAny(text, "calorie", "diet", "nutrition", "protein", "eat", "food", "weight", "exercise", "workout", "sport", "running",
                "kalori", "diyet", "beslenme", "spor", "egzersiz", "kilo"))
        {
            return "nutrition";
        }

        if (ContainsAny(text, "result", "glucose", "cholesterol", "hemoglobin", "vitamin", "blood test", "lab test", "out of range",
                "sonuç", "tahlil", "kolesterol", "şeker"))
        {
            return "results";
        }

        if (ContainsAny(text, "laborator", "lab", "labs", "doctor", "clinic", "laboratuvar", "doktor"))
        {
            return "directory";
        }

        if (ContainsAny(text, "code", "python", "javascript", "program", "sql", "bug ", "bugs", "html", "react", "c#")) return "programming";
        if (ContainsAny(text, "recipe", "cake", "cook", "bake", "pasta", "tarif", "yemek")) return "cooking";
        if (ContainsAny(text, "homework", "math", "equation", "solve", "exam", "ödev", "matematik")) return "study";
        if (ContainsAny(text, "essay", "poem", "story", "write a", "letter", "şiir", "hikaye")) return "writing";
        if (ContainsAny(text, "movie", "film", "series", "music", "song", "game", "football", "maç", "dizi", "şarkı")) return "entertainment";
        if (ContainsAny(text, "politic", "election", "president", "government", "war ", "seçim", "siyaset")) return "politics";
        if (ContainsAny(text, "bitcoin", "crypto", "stock", "invest", "money", "borsa", "yatırım")) return "finance";
        if (ContainsAny(text, "travel", "hotel", "flight", "vacation", "tatil", "otel", "uçak")) return "travel";
        if (ContainsAny(text, "lawyer", "legal", "contract", "lawsuit", "court", "avukat", "dava")) return "legal";

        if (ContainsAny(text, "hello", "hi ", "hey ", "merhaba", "selam", "who are you", "what can you")) return "greeting";
        return "other";
    }

    public static string Reply(string message, HealthChatContext context)
    {
        var topic = Classify(message);
        return topic switch
        {
            "emergency" =>
                "This may be an emergency. Please call 112 (Türkiye) or go to the nearest Emergency Department (Acil Servis) right now. " +
                "Don't drive yourself if you feel faint or have chest pain or trouble breathing — ask someone to take you or wait for the ambulance. " +
                "Don't eat, drink or take any medicine unless the emergency operator tells you to.",
            "roleplay" =>
                "I'm an AI assistant, not a doctor, so I can't take on that role or give you a diagnosis — only a doctor who examines you can. " +
                "What I can do is help you understand how urgent this is and which specialist to see. Tell me:\n" +
                "1. What exactly bothers you, and where?\n2. Since when, and how strong is it (0–10)?\n" +
                "3. Any other symptoms, chronic conditions or pregnancy?\n" +
                "If you have chest pain, trouble breathing, signs of a stroke or heavy bleeding — call 112 now.",
            "medication" =>
                "I can't recommend specific medicines or doses — only a doctor or pharmacist who knows your history can decide that safely. " +
                "Antibiotics in particular should be taken only on a doctor's prescription: many infections don't need them, and self-treatment causes resistance and side effects. " +
                "If you already have a prescription, follow it and the package leaflet, and ask your pharmacist if anything is unclear. " +
                "Tell me about your symptoms and I'll help you find the right specialist; for severe or sudden symptoms call 112.",
            "symptom" => SymptomReply(message),
            "nutrition" => NutritionReply(context),
            "results" => ResultsReply(context),
            "directory" => DirectoryReply(message, context),
            "platform" =>
                "Here's how Healthly works:\n" +
                "• Upload results: My health → Documents → \"Upload document\" (PDF, JPG or PNG up to 10 MB) → \"Analyze with AI\" → check the values → \"Confirm & save\".\n" +
                "• Healthly ID (HL-…) is in your profile card — give it to a partner laboratory so they can add new results.\n" +
                "• Care team: choose a doctor and a laboratory in your city; after they accept, you decide whether to share your records. To change your doctor, use \"Stop sharing\" and send a new request.\n" +
                "• Charts: My health → Health Trends (needs two measurements of the same test).",
            "greeting" =>
                "Hi, I'm Healthly AI. I can help you understand your lab results, think through symptoms and where to get care, give evidence-based nutrition and activity guidance, and explain how Healthly works. What would you like to know?",
            _ => HealthChatPrompt.OffTopicReplies.TryGetValue(topic, out var refusal) ? refusal : HealthChatPrompt.OffTopicReplies["other"],
        };
    }

    private static string SymptomReply(string message)
    {
        var text = message.ToLower(CultureInfo.InvariantCulture);
        var builder = new StringBuilder();
        if (ContainsAny(text, "folk", "home remed", "natural remed", "herbal", "remedy", "kocakarı", "bitkisel", "doğal tedavi"))
        {
            // Народные средства не советуем (правило 2 промпта) — только проверенная первая помощь
            builder.Append("I can't recommend folk, herbal or home remedies — they aren't proven to work and some make things worse. ");
        }
        builder.Append("I'm sorry you're dealing with this. To point you to the right care, could you tell me:\n");
        builder.Append("1. Where exactly is it and how strong is it (0–10)?\n");
        builder.Append("2. When did it start, and is it getting better or worse?\n");
        builder.Append("3. Any other symptoms — fever, vomiting, dizziness, bleeding? Are you pregnant or do you have a chronic condition?\n\n");

        if (text.Contains("burn") || text.Contains("yanık"))
        {
            builder.Append("Meanwhile: cool the burn under cool (not ice-cold) running water for 20 minutes, remove rings or tight items nearby, and cover it loosely with a clean non-fluffy cloth. ")
                .Append("Don't put ice, butter, oil or toothpaste on it and don't pop blisters. ")
                .Append("Large, deep, blistered or facial, hand, genital or chemical/electrical burns need the Emergency Department (Acil Servis) now.");
        }
        else if (text.Contains("abdominal") || text.Contains("stomach") || text.Contains("karın"))
        {
            builder.Append("Meanwhile: don't take painkillers or laxatives before a doctor examines you, and don't eat heavy food. ")
                .Append("Sudden severe pain, fever, vomiting blood, a hard belly or pain in pregnancy — go to the Emergency Department now. ")
                .Append("Otherwise a General Practitioner or a Gastroenterologist is the right first step.");
        }
        else
        {
            builder.Append("If symptoms are severe, sudden or come with chest pain, trouble breathing or confusion — call 112. ")
                .Append("Otherwise a General Practitioner (Family Medicine) is a good first step; they will refer you to a specialist if needed.");
        }
        return builder.ToString();
    }

    private static string NutritionReply(HealthChatContext context)
    {
        var profile = context.Profile;
        var builder = new StringBuilder();
        if (profile.Age is { } age && profile.HeightCm is { } height && profile.WeightKg is { } weight)
        {
            // Mifflin–St Jeor; для «Other» — середина между мужской и женской формулой
            var basal = 10 * weight + 6.25 * height - 5 * age;
            var bmr = profile.Sex switch { "Male" => basal + 5, "Female" => basal - 161, _ => basal - 78 };
            builder.Append(CultureInfo.InvariantCulture, $"Based on your profile (age {age}, {height:0} cm, {weight:0} kg), your resting energy need is about {bmr:0} kcal/day (Mifflin–St Jeor). ")
                .Append(CultureInfo.InvariantCulture, $"With light activity that's roughly {bmr * 1.375:0} kcal/day, with moderate activity about {bmr * 1.55:0}. ")
                .Append(CultureInfo.InvariantCulture, $"A general protein target for healthy adults is about {0.8 * weight:0} g/day (0.8 g/kg, WHO/FAO). ");
        }
        else
        {
            builder.Append("To estimate your calorie and protein needs I'd need your age, height and weight — you can add them in your profile. ");
        }
        builder.Append("WHO recommends 150–300 minutes of moderate activity per week plus muscle-strengthening twice a week. ");

        var abnormal = context.Results.Where(r => r.IsAbnormal || r.IsCritical).Select(r => r.TestName).ToList();
        if (abnormal.Count > 0)
        {
            builder.Append($"Some of your latest results are out of range ({string.Join(", ", abnormal)}) — please discuss diet or exercise changes with your doctor first. ");
        }
        builder.Append("These are general estimates, not a personal plan.");
        return builder.ToString();
    }

    private static string ResultsReply(HealthChatContext context)
    {
        if (context.Results.Count == 0)
        {
            return "I don't see confirmed lab results in your account yet. Upload a report in My health → Documents, analyze it and confirm the values — then I can help you understand them.";
        }
        var critical = context.Results.Where(r => r.IsCritical).ToList();
        var abnormal = context.Results.Where(r => r.IsAbnormal && !r.IsCritical).ToList();
        var builder = new StringBuilder($"You have {context.Results.Count} confirmed indicators. ");
        if (critical.Count > 0)
        {
            builder.Append($"Critical: {Describe(critical)}. Critical values need urgent medical attention — contact your doctor today or go to the Emergency Department. ");
        }
        if (abnormal.Count > 0)
        {
            builder.Append($"Out of range: {Describe(abnormal)}. ");
        }
        if (critical.Count == 0 && abnormal.Count == 0)
        {
            builder.Append("All of them are within Healthly's reference ranges. ");
        }
        builder.Append("A General Practitioner or Internal Medicine doctor can review them with you and refer you to a specialist if needed.");
        return builder.ToString();
    }

    private static string DirectoryReply(string message, HealthChatContext context)
    {
        // Город из вопроса пациента («labs in Istanbul»), иначе — весь каталог
        var city = context.Laboratories.Select(l => l.City)
            .Concat(context.Doctors.Select(d => d.City))
            .FirstOrDefault(c => !string.IsNullOrEmpty(c) && message.Contains(c, StringComparison.OrdinalIgnoreCase));
        var labs = context.Laboratories.Where(l => city == null || l.City == city).Take(5).ToList();
        var doctors = context.Doctors.Where(d => city == null || d.City == city).Take(5).ToList();

        var builder = new StringBuilder(city == null ? "Here's who is on Healthly:\n" : $"In {city}:\n");
        foreach (var lab in labs)
        {
            builder.Append($"• Laboratory: {lab.Name}{(string.IsNullOrEmpty(lab.Address) ? string.Empty : $", {lab.Address}")}\n");
        }
        foreach (var doctor in doctors)
        {
            builder.Append($"• Doctor: {doctor.FullName}{(string.IsNullOrEmpty(doctor.Specialty) ? string.Empty : $" — {doctor.Specialty}")}\n");
        }
        if (labs.Count == 0 && doctors.Count == 0)
        {
            builder.Append("No partner laboratories or doctors are listed there yet.\n");
        }
        builder.Append("You can send a request on the Care team page.");
        return builder.ToString();
    }

    private static string Describe(IEnumerable<ChatLabResult> results) =>
        string.Join(", ", results.Select(r => $"{r.TestName} {r.Value} {r.Unit}".Trim()));

    // Слово (или начало слова) в тексте: «eat» найдётся в «eating», но не в «great»; «war » с пробелом — только целое слово.
    // Текст обрамляется пробелами, чтобы «hi » и «war » срабатывали и в конце фразы
    private static bool ContainsAny(string text, params string[] words)
    {
        var padded = $" {Regex.Replace(text, @"[^\p{L}\p{N}#']+", " ")} ";
        return words.Any(word => Regex.IsMatch(padded, $@"(?<!\p{{L}}){Regex.Escape(word)}", RegexOptions.CultureInvariant));
    }
}

using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using CentraSA.Application.DailyMeetings;
using CentraSA.Application.PendingTasks;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using CentraSA.Web.ViewModels.DailyMeetings;
using CentraSA.Web.ViewModels.Lookups;
using CentraSA.Web.ViewModels.PendingTasks;
using CentraSA.Web.ViewModels.Smuds;
using CentraSA.Web.ViewModels.SupportTickets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CentraSA.IntegrationTests;

public sealed class WorkItemFormFeedbackFlowTests
{
    [Fact]
    public void PresentationOnlyPropertiesAreExcludedFromPostValidation()
    {
        (Type Type, string Property)[] properties =
        [
            (typeof(PendingTaskFormViewModel), nameof(PendingTaskFormViewModel.Options)),
            (typeof(PendingTaskQuickCreateViewModel), nameof(PendingTaskQuickCreateViewModel.Areas)),
            (typeof(SupportTicketFormViewModel), nameof(SupportTicketFormViewModel.Options)),
            (typeof(SmudFormViewModel), nameof(SmudFormViewModel.Options)),
            (typeof(LookupFormViewModel), nameof(LookupFormViewModel.Areas)),
            (typeof(DailyMeetingBuilderItemViewModel), nameof(DailyMeetingBuilderItemViewModel.SourceLabel)),
            (typeof(DailyMeetingBuilderItemViewModel), nameof(DailyMeetingBuilderItemViewModel.Title)),
            (typeof(DailyMeetingBuilderItemViewModel), nameof(DailyMeetingBuilderItemViewModel.Status)),
            (typeof(DailyMeetingBuilderItemViewModel), nameof(DailyMeetingBuilderItemViewModel.DueDate)),
            (typeof(DailyMeetingBuilderItemViewModel), nameof(DailyMeetingBuilderItemViewModel.Responsible)),
            (typeof(DailyMeetingBuilderItemViewModel), nameof(DailyMeetingBuilderItemViewModel.RecommendedSection)),
            (typeof(DailyMeetingBuilderItemViewModel), nameof(DailyMeetingBuilderItemViewModel.SuggestionReason)),
        ];

        Assert.All(properties, item =>
        {
            PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(item.Type.GetProperty(item.Property));
            Assert.NotNull(property.GetCustomAttribute<ValidateNeverAttribute>());
        });
    }

    [Fact]
    public void PostFormsUseTagHelpersAndValidationSummariesAreComplete()
    {
        string repositoryRoot = FindRepositoryRoot();
        string viewsRoot = Path.Combine(repositoryRoot, "src", "CentraSA.Web", "Views");
        var postFormPattern = new Regex(
            "<form\\b(?=[^>]*\\bmethod\\s*=\\s*[\\\"']post[\\\"'])[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        string[] viewPaths = Directory.GetFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories);
        var postForms = new List<(string Path, string Tag)>();

        foreach (string path in viewPaths)
        {
            string content = File.ReadAllText(path);
            Assert.DoesNotContain("asp-validation-summary=\"ModelOnly\"", content, StringComparison.Ordinal);
            postForms.AddRange(postFormPattern.Matches(content)
                .Select(match => (path, match.Value)));
        }

        Assert.NotEmpty(postForms);
        Assert.All(postForms, form => Assert.Contains(
            "asp-action=",
            form.Tag,
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidSupportTicketCreateAndEditRedirectAndPersist()
    {
        await using TestSession session = await TestSession.CreateAsync();
        ReferenceIds references = await GetReferencesAsync(session.Factory.Services, WorkItemScope.SupportTicket);

        HttpResponseMessage createResponse = await PostSupportTicketAsync(
            session,
            "/chamados/novo",
            TicketForm(references, "24001", "Chamado criado pelo formulário"));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        string detailsPath = Assert.IsType<Uri>(createResponse.Headers.Location).OriginalString;
        Assert.StartsWith("/chamados/", detailsPath, StringComparison.Ordinal);

        SupportTicket created = await GetSupportTicketAsync(session.Factory.Services, "24001");
        Dictionary<string, string> editForm = TicketForm(
            references,
            created.TicketNumber,
            "Chamado atualizado pelo formulário");
        editForm["Id"] = created.Id.ToString();
        editForm["Version"] = created.Version.ToString(CultureInfo.InvariantCulture);

        HttpResponseMessage editResponse = await PostSupportTicketAsync(
            session,
            $"/chamados/{created.Id}/editar",
            editForm);

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
        Assert.Equal($"/chamados/{created.Id}", GetLocationPath(editResponse.Headers.Location));
        SupportTicket updated = await GetSupportTicketAsync(session.Factory.Services, "24001");
        Assert.Equal("Chamado atualizado pelo formulário", updated.Title);

        string detailsHtml = WebUtility.HtmlDecode(
            await session.Client.GetStringAsync(editResponse.Headers.Location!));
        Assert.Contains("role=\"status\"", detailsHtml, StringComparison.Ordinal);
        Assert.Contains("Chamado atualizado com sucesso.", detailsHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidAndDuplicateSupportTicketShowFieldFeedback()
    {
        await using TestSession session = await TestSession.CreateAsync();
        ReferenceIds references = await GetReferencesAsync(session.Factory.Services, WorkItemScope.SupportTicket);
        Dictionary<string, string> invalidForm = TicketForm(references, string.Empty, string.Empty);

        HttpResponseMessage invalidResponse = await PostSupportTicketAsync(
            session,
            "/chamados/novo",
            invalidForm);

        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
        string invalidHtml = WebUtility.HtmlDecode(await invalidResponse.Content.ReadAsStringAsync());
        AssertFieldError(invalidHtml, "Number", "Informe o número do chamado.");
        AssertFieldError(invalidHtml, "Title", "Informe o título do chamado.");
        Assert.Contains("data-valmsg-summary=\"true\"", invalidHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Options field", invalidHtml, StringComparison.OrdinalIgnoreCase);

        await PostSupportTicketAsync(
            session,
            "/chamados/novo",
            TicketForm(references, "24002", "Primeiro chamado"));
        HttpResponseMessage duplicateResponse = await PostSupportTicketAsync(
            session,
            "/chamados/novo",
            TicketForm(references, "24002", "Chamado duplicado"));

        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        string duplicateHtml = WebUtility.HtmlDecode(await duplicateResponse.Content.ReadAsStringAsync());
        AssertFieldError(duplicateHtml, "Number", "Já existe um chamado com esse número.");
    }

    [Fact]
    public async Task ValidSmudCreateAndEditRedirectAndPersist()
    {
        await using TestSession session = await TestSession.CreateAsync();
        ReferenceIds references = await GetReferencesAsync(session.Factory.Services, WorkItemScope.Smud);

        HttpResponseMessage createResponse = await PostSmudAsync(
            session,
            "/smuds/novo",
            SmudForm(references, "SMUD240", "SMUD criado pelo formulário"));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        string detailsPath = Assert.IsType<Uri>(createResponse.Headers.Location).OriginalString;
        Assert.StartsWith("/smuds/", detailsPath, StringComparison.Ordinal);

        Smud created = await GetSmudAsync(session.Factory.Services, "SMUD240");
        Dictionary<string, string> editForm = SmudForm(
            references,
            created.Code,
            "SMUD atualizado pelo formulário");
        editForm["Id"] = created.Id.ToString();
        editForm["Version"] = created.Version.ToString(CultureInfo.InvariantCulture);

        HttpResponseMessage editResponse = await PostSmudAsync(
            session,
            $"/smuds/{created.Id}/editar",
            editForm);

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
        Assert.Equal($"/smuds/{created.Id}", GetLocationPath(editResponse.Headers.Location));
        Smud updated = await GetSmudAsync(session.Factory.Services, "SMUD240");
        Assert.Equal("SMUD atualizado pelo formulário", updated.Title);

        string detailsHtml = WebUtility.HtmlDecode(
            await session.Client.GetStringAsync(editResponse.Headers.Location!));
        Assert.Contains("role=\"status\"", detailsHtml, StringComparison.Ordinal);
        Assert.Contains("SMUD atualizado com sucesso.", detailsHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidAndDuplicateSmudShowFieldFeedback()
    {
        await using TestSession session = await TestSession.CreateAsync();
        ReferenceIds references = await GetReferencesAsync(session.Factory.Services, WorkItemScope.Smud);

        HttpResponseMessage invalidResponse = await PostSmudAsync(
            session,
            "/smuds/novo",
            SmudForm(references, string.Empty, string.Empty));

        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
        string invalidHtml = WebUtility.HtmlDecode(await invalidResponse.Content.ReadAsStringAsync());
        AssertFieldError(invalidHtml, "Code", "Informe o código do SMUD.");
        AssertFieldError(invalidHtml, "Title", "Informe o título do SMUD.");
        Assert.Contains("data-valmsg-summary=\"true\"", invalidHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Options field", invalidHtml, StringComparison.OrdinalIgnoreCase);

        await PostSmudAsync(
            session,
            "/smuds/novo",
            SmudForm(references, "SMUD241", "Primeiro SMUD"));
        HttpResponseMessage duplicateResponse = await PostSmudAsync(
            session,
            "/smuds/novo",
            SmudForm(references, "SMUD241", "SMUD duplicado"));

        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        string duplicateHtml = WebUtility.HtmlDecode(await duplicateResponse.Content.ReadAsStringAsync());
        AssertFieldError(duplicateHtml, "Code", "Já existe um SMUD com esse código.");
    }

    [Fact]
    public async Task ValidLookupCreateAndEditRedirectAndPersist()
    {
        await using TestSession session = await TestSession.CreateAsync();
        Dictionary<string, string> createForm = LookupForm("Área criada pelo formulário");

        HttpResponseMessage createResponse = await session.PostFormAsync(
            "/cadastros/novo?kind=TeamArea",
            createForm);

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Equal("/cadastros", GetLocationPath(createResponse.Headers.Location));
        TeamArea created = await GetTeamAreaAsync(session.Factory.Services, "Área criada pelo formulário");

        Dictionary<string, string> editForm = LookupForm("Área atualizada pelo formulário");
        editForm["Id"] = created.Id.ToString();
        HttpResponseMessage editResponse = await session.PostFormAsync(
            $"/cadastros/TeamArea/{created.Id}/editar",
            editForm);

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
        Assert.Equal("/cadastros", GetLocationPath(editResponse.Headers.Location));
        TeamArea updated = await GetTeamAreaAsync(session.Factory.Services, "Área atualizada pelo formulário");
        Assert.Equal(created.Id, updated.Id);

        string indexHtml = WebUtility.HtmlDecode(
            await session.Client.GetStringAsync(editResponse.Headers.Location!));
        Assert.Contains("role=\"status\"", indexHtml, StringComparison.Ordinal);
        Assert.Contains("Cadastro auxiliar atualizado.", indexHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidLookupShowsSummaryAndFieldFeedback()
    {
        await using TestSession session = await TestSession.CreateAsync();

        HttpResponseMessage response = await session.PostFormAsync(
            "/cadastros/novo?kind=TeamArea",
            LookupForm(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("data-valmsg-summary=\"true\"", html, StringComparison.Ordinal);
        AssertFieldError(html, "Name", "Informe o nome.");
    }

    [Fact]
    public async Task ValidDailyMeetingCreateAndEditRedirectAndPersist()
    {
        await using TestSession session = await TestSession.CreateAsync();
        DailyMeetingBuilderData builder = await CreateMeetingSourceAndGetBuilderAsync(session.Factory.Services);
        DailyMeetingBuilderRow row = Assert.Single(builder.Rows);

        HttpResponseMessage createResponse = await session.PostFormAsync(
            "/reunioes/nova",
            MeetingForm(builder, row, "Roteiro criado pelo formulário"));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        DailyMeeting created = await GetDailyMeetingAsync(
            session.Factory.Services,
            "Roteiro criado pelo formulário");

        DailyMeetingBuilderData editBuilder = await GetMeetingBuilderAsync(
            session.Factory.Services,
            created.Id);
        DailyMeetingBuilderRow editRow = Assert.Single(editBuilder.Rows);
        HttpResponseMessage editResponse = await session.PostFormAsync(
            $"/reunioes/{created.Id}/preparar",
            MeetingForm(editBuilder, editRow, "Roteiro atualizado pelo formulário"));

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
        Assert.Equal($"/reunioes/{created.Id}", GetLocationPath(editResponse.Headers.Location));
        DailyMeeting updated = await GetDailyMeetingAsync(
            session.Factory.Services,
            "Roteiro atualizado pelo formulário");
        Assert.Equal(created.Id, updated.Id);

        string detailsHtml = WebUtility.HtmlDecode(
            await session.Client.GetStringAsync(editResponse.Headers.Location!));
        Assert.Contains("role=\"status\"", detailsHtml, StringComparison.Ordinal);
        Assert.Contains("Roteiro da reunião atualizado.", detailsHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidDailyMeetingItemShowsSummaryAndFieldFeedback()
    {
        await using TestSession session = await TestSession.CreateAsync();
        DailyMeetingBuilderData builder = await CreateMeetingSourceAndGetBuilderAsync(session.Factory.Services);
        DailyMeetingBuilderRow row = Assert.Single(builder.Rows);
        Dictionary<string, string> form = MeetingForm(builder, row, "Roteiro inválido");
        form["Items[0].SortOrder"] = "100001";

        HttpResponseMessage response = await session.PostFormAsync("/reunioes/nova", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("data-valmsg-summary=\"true\"", html, StringComparison.Ordinal);
        AssertFieldError(html, "Items[0].SortOrder", "A ordem deve estar entre 0 e 100.000.");
    }

    [Theory]
    [InlineData("/pendencias/nova")]
    [InlineData("/chamados/novo")]
    [InlineData("/smuds/novo")]
    [InlineData("/reunioes/nova")]
    [InlineData("/cadastros/novo?kind=TeamArea")]
    public async Task EditorFormsExposeAntiforgeryAndCompleteValidationSummary(string path)
    {
        await using TestSession session = await TestSession.CreateAsync();

        string html = await session.Client.GetStringAsync(new Uri(path, UriKind.Relative));

        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.Contains("data-valmsg-summary=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> PostSupportTicketAsync(
        TestSession session,
        string path,
        Dictionary<string, string> form) =>
        session.PostFormAsync(path, form);

    private static Task<HttpResponseMessage> PostSmudAsync(
        TestSession session,
        string path,
        Dictionary<string, string> form) =>
        session.PostFormAsync(path, form);

    private static Dictionary<string, string> TicketForm(
        ReferenceIds references,
        string number,
        string title) => new()
        {
            ["Id"] = string.Empty,
            ["Number"] = number,
            ["Title"] = title,
            ["Description"] = string.Empty,
            ["CategoryId"] = references.CategoryId!.Value.ToString(),
            ["ResponsibleAreaId"] = references.AreaId.ToString(),
            ["ResponsiblePersonId"] = string.Empty,
            ["StatusId"] = references.StatusId.ToString(),
            ["Priority"] = "Medium",
            ["OpenedOn"] = "2026-07-21",
            ["DueDate"] = string.Empty,
            ["PendingAction"] = string.Empty,
            ["Notes"] = string.Empty,
            ["Version"] = "1",
        };

    private static Dictionary<string, string> SmudForm(
        ReferenceIds references,
        string code,
        string title) => new()
        {
            ["Id"] = string.Empty,
            ["Code"] = code,
            ["Title"] = title,
            ["Description"] = string.Empty,
            ["ResponsibleAreaId"] = references.AreaId.ToString(),
            ["ResponsiblePersonId"] = string.Empty,
            ["StatusId"] = references.StatusId.ToString(),
            ["Priority"] = "Medium",
            ["OpenedOn"] = "2026-07-21",
            ["DueDate"] = string.Empty,
            ["RequiredAction"] = string.Empty,
            ["Notes"] = string.Empty,
            ["Version"] = "1",
        };

    private static Dictionary<string, string> LookupForm(string name) => new()
    {
        ["Id"] = string.Empty,
        ["Kind"] = "TeamArea",
        ["Name"] = name,
        ["Code"] = string.Empty,
        ["Scope"] = "PendingTask",
        ["AreaKind"] = "InternalArea",
        ["TeamAreaId"] = string.Empty,
        ["LifecycleState"] = "Active",
        ["ColorToken"] = "blue",
        ["SortOrder"] = "10",
    };

    private static Dictionary<string, string> MeetingForm(
        DailyMeetingBuilderData builder,
        DailyMeetingBuilderRow row,
        string notes) => new()
        {
            ["Id"] = builder.Id?.ToString() ?? string.Empty,
            ["MeetingDate"] = builder.MeetingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["GeneralNotes"] = notes,
            ["Version"] = builder.Version.ToString(CultureInfo.InvariantCulture),
            ["Items[0].ItemId"] = row.ItemId?.ToString() ?? string.Empty,
            ["Items[0].SourceType"] = row.SourceType.ToString(),
            ["Items[0].SourceId"] = row.SourceId.ToString(),
            ["Items[0].Selected"] = "true",
            ["Items[0].Section"] = row.RecommendedSection.ToString(),
            ["Items[0].SortOrder"] = "10",
            ["Items[0].PresentationNotes"] = string.Empty,
        };

    private static async Task<ReferenceIds> GetReferencesAsync(
        IServiceProvider services,
        WorkItemScope scope)
    {
        await using AsyncServiceScope serviceScope = services.CreateAsyncScope();
        CentraSaDbContext dbContext = serviceScope.ServiceProvider.GetRequiredService<CentraSaDbContext>();
        Guid areaId = await dbContext.TeamAreas.OrderBy(area => area.Name).Select(area => area.Id).FirstAsync();
        Guid statusId = await dbContext.StatusDefinitions
            .Where(status => status.Scope == scope && status.IsActive)
            .OrderBy(status => status.SortOrder)
            .Select(status => status.Id)
            .FirstAsync();
        Guid? categoryId = scope == WorkItemScope.SupportTicket
            ? await dbContext.Categories
                .Where(category => category.Scope == scope && category.IsActive)
                .OrderBy(category => category.SortOrder)
                .Select(category => (Guid?)category.Id)
                .FirstAsync()
            : null;
        return new ReferenceIds(areaId, statusId, categoryId);
    }

    private static async Task<SupportTicket> GetSupportTicketAsync(IServiceProvider services, string number)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        CentraSaDbContext dbContext = scope.ServiceProvider.GetRequiredService<CentraSaDbContext>();
        return await dbContext.SupportTickets.AsNoTracking().SingleAsync(ticket => ticket.TicketNumber == number);
    }

    private static async Task<Smud> GetSmudAsync(IServiceProvider services, string code)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        CentraSaDbContext dbContext = scope.ServiceProvider.GetRequiredService<CentraSaDbContext>();
        return await dbContext.Smuds.AsNoTracking().SingleAsync(smud => smud.Code == code);
    }

    private static async Task<TeamArea> GetTeamAreaAsync(IServiceProvider services, string name)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        CentraSaDbContext dbContext = scope.ServiceProvider.GetRequiredService<CentraSaDbContext>();
        return await dbContext.TeamAreas.AsNoTracking().SingleAsync(area => area.Name == name);
    }

    private static async Task<DailyMeetingBuilderData> CreateMeetingSourceAndGetBuilderAsync(
        IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IPendingTaskService pendingTaskService = scope.ServiceProvider.GetRequiredService<IPendingTaskService>();
        CentraSaDbContext dbContext = scope.ServiceProvider.GetRequiredService<CentraSaDbContext>();
        Guid areaId = await dbContext.TeamAreas.Select(area => area.Id).FirstAsync();
        PendingTaskOperationResult created = await pendingTaskService.QuickCreateAsync(
            new PendingTaskQuickInput("Item para roteiro HTTP", areaId, null),
            Guid.NewGuid());
        Assert.True(created.Succeeded);
        IDailyMeetingService meetingService = scope.ServiceProvider.GetRequiredService<IDailyMeetingService>();
        return await meetingService.GetCreateBuilderAsync();
    }

    private static async Task<DailyMeetingBuilderData> GetMeetingBuilderAsync(
        IServiceProvider services,
        Guid id)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IDailyMeetingService service = scope.ServiceProvider.GetRequiredService<IDailyMeetingService>();
        return Assert.IsType<DailyMeetingBuilderData>(await service.GetEditBuilderAsync(id));
    }

    private static async Task<DailyMeeting> GetDailyMeetingAsync(
        IServiceProvider services,
        string notes)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        CentraSaDbContext dbContext = scope.ServiceProvider.GetRequiredService<CentraSaDbContext>();
        return await dbContext.DailyMeetings
            .AsNoTracking()
            .SingleAsync(meeting => meeting.GeneralNotes == notes);
    }

    private static void AssertFieldError(string html, string fieldName, string message)
    {
        string pattern = $"data-valmsg-for=\"{Regex.Escape(fieldName)}\"[^>]*>\\s*{Regex.Escape(message)}";
        Assert.Matches(pattern, html);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        Match match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, "O formulário não contém token antiforgery.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string? GetLocationPath(Uri? location)
    {
        if (location is null)
        {
            return null;
        }

        string path = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        int queryStart = path.IndexOf('?', StringComparison.Ordinal);
        return queryStart >= 0 ? path[..queryStart] : path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CentraSA.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("A raiz do repositório não foi encontrada.");
    }

    private sealed record ReferenceIds(Guid AreaId, Guid StatusId, Guid? CategoryId);

    private sealed class TestSession : IAsyncDisposable
    {
        private TestSession(string storageRoot, WebApplicationFactory<Program> factory, HttpClient client)
        {
            StorageRoot = storageRoot;
            Factory = factory;
            Client = client;
        }

        private string StorageRoot { get; }

        public WebApplicationFactory<Program> Factory { get; }

        public HttpClient Client { get; }

        public static async Task<TestSession> CreateAsync()
        {
            string storageRoot = Path.Combine(
                Path.GetTempPath(),
                "CentraSA.WorkItemFormTests",
                Guid.NewGuid().ToString("N"));
            WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Development");
                    builder.UseSetting("Storage:DataDirectory", storageRoot);
                    builder.UseSetting("SeedDemoData", "false");
                });
            HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });

            try
            {
                string setupHtml = await client.GetStringAsync(
                    new Uri("/conta/configuracao-inicial", UriKind.Relative));
                const string password = "senha-formularios-2026";
                HttpResponseMessage setupResponse = await client.PostAsync(
                    new Uri("/conta/configuracao-inicial", UriKind.Relative),
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["UserName"] = "administrador",
                        ["Password"] = password,
                        ["ConfirmPassword"] = password,
                        ["__RequestVerificationToken"] = ExtractAntiforgeryToken(setupHtml),
                    }));
                Assert.Equal(HttpStatusCode.Redirect, setupResponse.StatusCode);

                string loginHtml = await client.GetStringAsync(new Uri("/conta/entrar", UriKind.Relative));
                HttpResponseMessage loginResponse = await client.PostAsync(
                    new Uri("/conta/entrar", UriKind.Relative),
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["UserName"] = "administrador",
                        ["Password"] = password,
                        ["RememberMe"] = "false",
                        ["__RequestVerificationToken"] = ExtractAntiforgeryToken(loginHtml),
                    }));
                Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
                return new TestSession(storageRoot, factory, client);
            }
            catch
            {
                client.Dispose();
                factory.Dispose();
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(storageRoot))
                {
                    Directory.Delete(storageRoot, recursive: true);
                }

                throw;
            }
        }

        public async Task<HttpResponseMessage> PostFormAsync(
            string path,
            Dictionary<string, string> form)
        {
            string html = await Client.GetStringAsync(new Uri(path, UriKind.Relative));
            form["__RequestVerificationToken"] = ExtractAntiforgeryToken(html);
            return await Client.PostAsync(
                new Uri(path, UriKind.Relative),
                new FormUrlEncodedContent(form));
        }

        public ValueTask DisposeAsync()
        {
            Client.Dispose();
            Factory.Dispose();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(StorageRoot))
            {
                Directory.Delete(StorageRoot, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}

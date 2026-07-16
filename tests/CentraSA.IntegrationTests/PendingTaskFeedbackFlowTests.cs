using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CentraSA.Application.PendingTasks;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CentraSA.IntegrationTests;

public sealed class PendingTaskFeedbackFlowTests
{
    [Fact]
    public async Task InvalidQuickCreateShowsInlineErrorsAndPreservesValues()
    {
        await using TestSession session = await TestSession.CreateAsync();
        string indexHtml = await session.Client.GetStringAsync(new Uri("/pendencias", UriKind.Relative));
        string token = ExtractAntiforgeryToken(indexHtml);
        const string dueDate = "2026-08-14";

        HttpResponseMessage missingFieldsResponse = await session.Client.PostAsync(
            new Uri("/pendencias/criacao-rapida", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Title"] = string.Empty,
                ["ResponsibleAreaId"] = string.Empty,
                ["DueDate"] = dueDate,
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.OK, missingFieldsResponse.StatusCode);
        string missingFieldsHtml = WebUtility.HtmlDecode(await missingFieldsResponse.Content.ReadAsStringAsync());
        AssertFieldError(missingFieldsHtml, "Title", "Informe o título da pendência.");
        AssertFieldError(missingFieldsHtml, "ResponsibleAreaId", "Selecione a área responsável.");
        AssertInputValue(missingFieldsHtml, "DueDate", dueDate);

        token = ExtractAntiforgeryToken(missingFieldsHtml);
        const string preservedTitle = "Revisar retorno da operação";
        HttpResponseMessage missingAreaResponse = await session.Client.PostAsync(
            new Uri("/pendencias/criacao-rapida", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Title"] = preservedTitle,
                ["ResponsibleAreaId"] = string.Empty,
                ["DueDate"] = dueDate,
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.OK, missingAreaResponse.StatusCode);
        string missingAreaHtml = WebUtility.HtmlDecode(await missingAreaResponse.Content.ReadAsStringAsync());
        AssertFieldError(missingAreaHtml, "ResponsibleAreaId", "Selecione a área responsável.");
        AssertInputValue(missingAreaHtml, "Title", preservedTitle);
        AssertInputValue(missingAreaHtml, "DueDate", dueDate);

        token = ExtractAntiforgeryToken(missingAreaHtml);
        HttpResponseMessage invalidAreaResponse = await session.Client.PostAsync(
            new Uri("/pendencias/criacao-rapida", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Title"] = preservedTitle,
                ["ResponsibleAreaId"] = Guid.NewGuid().ToString(),
                ["DueDate"] = dueDate,
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.OK, invalidAreaResponse.StatusCode);
        string invalidAreaHtml = WebUtility.HtmlDecode(await invalidAreaResponse.Content.ReadAsStringAsync());
        AssertFieldError(invalidAreaHtml, "ResponsibleAreaId", "Selecione uma área responsável válida.");
        AssertInputValue(invalidAreaHtml, "Title", preservedTitle);
        AssertInputValue(invalidAreaHtml, "DueDate", dueDate);

        await using AsyncServiceScope scope = session.Factory.Services.CreateAsyncScope();
        CentraSaDbContext dbContext = scope.ServiceProvider.GetRequiredService<CentraSaDbContext>();
        Assert.False(await dbContext.PendingTasks.AnyAsync());
    }

    [Fact]
    public async Task InvalidFullCreateShowsInlineErrorsAndPreservesValues()
    {
        await using TestSession session = await TestSession.CreateAsync();
        string createHtml = await session.Client.GetStringAsync(new Uri("/pendencias/nova", UriKind.Relative));
        string token = ExtractAntiforgeryToken(createHtml);

        HttpResponseMessage response = await session.Client.PostAsync(
            new Uri("/pendencias/nova", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Title"] = string.Empty,
                ["Description"] = string.Empty,
                ["ResponsibleAreaId"] = string.Empty,
                ["ResponsiblePersonId"] = string.Empty,
                ["StatusId"] = string.Empty,
                ["CategoryId"] = string.Empty,
                ["Priority"] = "Medium",
                ["DueDate"] = string.Empty,
                ["Origin"] = string.Empty,
                ["Notes"] = string.Empty,
                ["RelatedSmudId"] = string.Empty,
                ["RelatedSupportTicketId"] = string.Empty,
                ["Version"] = "1",
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        AssertFieldError(html, "Title", "Informe o título da pendência.");
        AssertFieldError(html, "ResponsibleAreaId", "Selecione a área responsável.");
        AssertFieldError(html, "StatusId", "Selecione o status.");
    }

    [Fact]
    public async Task ValidFullCreateRedirectsToDetailsAndPersistsTask()
    {
        await using TestSession session = await TestSession.CreateAsync();
        Guid areaId = await GetFirstAreaIdAsync(session.Factory.Services);
        Guid statusId = await GetFirstPendingTaskStatusIdAsync(session.Factory.Services);
        Guid categoryId = await GetFirstPendingTaskCategoryIdAsync(session.Factory.Services);
        string createHtml = await session.Client.GetStringAsync(new Uri("/pendencias/nova", UriKind.Relative));
        string token = ExtractAntiforgeryToken(createHtml);
        const string title = "Pendência criada pelo teste de integração";

        HttpResponseMessage response = await session.Client.PostAsync(
            new Uri("/pendencias/nova", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = string.Empty,
                ["Title"] = title,
                ["Description"] = string.Empty,
                ["ResponsibleAreaId"] = areaId.ToString(),
                ["ResponsiblePersonId"] = string.Empty,
                ["StatusId"] = statusId.ToString(),
                ["CategoryId"] = categoryId.ToString(),
                ["Priority"] = "Medium",
                ["DueDate"] = string.Empty,
                ["Origin"] = string.Empty,
                ["Notes"] = string.Empty,
                ["RelatedSmudId"] = string.Empty,
                ["RelatedSupportTicketId"] = string.Empty,
                ["Version"] = "1",
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/pendencias/", GetLocationPath(response.Headers.Location));

        await using AsyncServiceScope scope = session.Factory.Services.CreateAsyncScope();
        CentraSaDbContext dbContext = scope.ServiceProvider.GetRequiredService<CentraSaDbContext>();
        PendingTask task = await dbContext.PendingTasks
            .Include(item => item.StatusDefinition)
            .SingleAsync(item => item.Title == title);
        Assert.Equal(LifecycleState.Active, task.StatusDefinition.LifecycleState);
        Assert.Equal(areaId, task.ResponsibleAreaId);
        Assert.Equal(statusId, task.StatusDefinitionId);
        Assert.Equal(categoryId, task.CategoryId);
        Assert.Null(task.CompletedAtUtc);
        Assert.Null(task.ArchivedAtUtc);
    }

    [Fact]
    public async Task ValidQuickCreateShowsConfirmationAndPersistsActiveTask()
    {
        await using TestSession session = await TestSession.CreateAsync();
        Guid areaId = await GetFirstAreaIdAsync(session.Factory.Services);
        string indexHtml = await session.Client.GetStringAsync(new Uri("/pendencias", UriKind.Relative));
        string token = ExtractAntiforgeryToken(indexHtml);
        const string title = "Confirmar janela de manutenção";

        HttpResponseMessage response = await session.Client.PostAsync(
            new Uri("/pendencias/criacao-rapida", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Title"] = title,
                ["ResponsibleAreaId"] = areaId.ToString(),
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/pendencias", GetLocationPath(response.Headers.Location));

        string updatedHtml = WebUtility.HtmlDecode(
            await session.Client.GetStringAsync(new Uri("/pendencias", UriKind.Relative)));
        Assert.Contains("role=\"status\"", updatedHtml, StringComparison.Ordinal);
        Assert.Contains("Pendência criada e adicionada à lista.", updatedHtml, StringComparison.Ordinal);
        Assert.Contains(title, updatedHtml, StringComparison.Ordinal);

        await using AsyncServiceScope scope = session.Factory.Services.CreateAsyncScope();
        CentraSaDbContext dbContext = scope.ServiceProvider.GetRequiredService<CentraSaDbContext>();
        var task = await dbContext.PendingTasks
            .Include(item => item.StatusDefinition)
            .SingleAsync(item => item.Title == title);
        Assert.Equal(LifecycleState.Active, task.StatusDefinition.LifecycleState);
        Assert.Null(task.CompletedAtUtc);
        Assert.Null(task.ArchivedAtUtc);
    }

    [Fact]
    public async Task PresentationShowsCompletionErrorThenSuccessAndCreateAction()
    {
        await using TestSession session = await TestSession.CreateAsync();
        Guid taskId;
        long version;

        await using (AsyncServiceScope createScope = session.Factory.Services.CreateAsyncScope())
        {
            IPendingTaskService service = createScope.ServiceProvider.GetRequiredService<IPendingTaskService>();
            Guid areaId = await QueryFirstAreaIdAsync(createScope.ServiceProvider);
            PendingTaskOperationResult created = await service.QuickCreateAsync(
                new PendingTaskQuickInput("Pendência apresentada no teste", areaId, null),
                Guid.NewGuid());
            Assert.True(created.Succeeded);
            taskId = Assert.IsType<Guid>(created.Id);
            PendingTaskDetailsData details = Assert.IsType<PendingTaskDetailsData>(
                await service.GetDetailsAsync(taskId, includeArchived: false));
            version = details.Item.Version;
        }

        string presentationHtml = await session.Client.GetStringAsync(
            new Uri("/pendencias/apresentacao", UriKind.Relative));
        string token = ExtractAntiforgeryToken(presentationHtml);

        HttpResponseMessage conflictResponse = await CompleteAsync(
            session.Client,
            taskId,
            version + 1,
            token);
        Assert.Equal(HttpStatusCode.Redirect, conflictResponse.StatusCode);
        Assert.Equal("/pendencias/apresentacao", GetLocationPath(conflictResponse.Headers.Location));

        string conflictHtml = WebUtility.HtmlDecode(
            await session.Client.GetStringAsync(conflictResponse.Headers.Location!));
        Assert.Contains("role=\"alert\"", conflictHtml, StringComparison.Ordinal);
        Assert.Contains(
            "A pendência foi alterada em outra aba. Recarregue a página e tente novamente.",
            conflictHtml,
            StringComparison.Ordinal);

        token = ExtractAntiforgeryToken(conflictHtml);
        HttpResponseMessage successResponse = await CompleteAsync(
            session.Client,
            taskId,
            version,
            token);
        Assert.Equal(HttpStatusCode.Redirect, successResponse.StatusCode);

        string successHtml = WebUtility.HtmlDecode(
            await session.Client.GetStringAsync(successResponse.Headers.Location!));
        Assert.Contains("role=\"status\"", successHtml, StringComparison.Ordinal);
        Assert.Contains("Pendência concluída.", successHtml, StringComparison.Ordinal);
        Assert.Contains("Nenhuma pendência ativa para apresentar.", successHtml, StringComparison.Ordinal);
        Assert.Contains("href=\"/pendencias#quick-task\"", successHtml, StringComparison.Ordinal);
        Assert.Contains("Criar outra pendência", successHtml, StringComparison.Ordinal);

        await using AsyncServiceScope verificationScope = session.Factory.Services.CreateAsyncScope();
        CentraSaDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<CentraSaDbContext>();
        var task = await dbContext.PendingTasks
            .Include(item => item.StatusDefinition)
            .SingleAsync(item => item.Id == taskId);
        Assert.Equal(LifecycleState.Completed, task.StatusDefinition.LifecycleState);
        Assert.NotNull(task.CompletedAtUtc);
    }

    private static Task<HttpResponseMessage> CompleteAsync(
        HttpClient client,
        Guid taskId,
        long version,
        string token) =>
        client.PostAsync(
            new Uri($"/pendencias/{taskId}/concluir", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["version"] = version.ToString(CultureInfo.InvariantCulture),
                ["presentation"] = "true",
                ["__RequestVerificationToken"] = token,
            }));

    private static async Task<Guid> GetFirstAreaIdAsync(IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await QueryFirstAreaIdAsync(scope.ServiceProvider);
    }

    private static async Task<Guid> QueryFirstAreaIdAsync(IServiceProvider scopedServices)
    {
        CentraSaDbContext dbContext = scopedServices.GetRequiredService<CentraSaDbContext>();
        return await dbContext.TeamAreas.Select(area => area.Id).FirstAsync();
    }

    private static async Task<Guid> GetFirstPendingTaskStatusIdAsync(IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await QueryFirstPendingTaskStatusIdAsync(scope.ServiceProvider);
    }

    private static async Task<Guid> QueryFirstPendingTaskStatusIdAsync(IServiceProvider scopedServices)
    {
        CentraSaDbContext dbContext = scopedServices.GetRequiredService<CentraSaDbContext>();
        return await dbContext.StatusDefinitions
            .Where(status => status.Scope == WorkItemScope.PendingTask)
            .OrderBy(status => status.SortOrder)
            .Select(status => status.Id)
            .FirstAsync();
    }

    private static async Task<Guid> GetFirstPendingTaskCategoryIdAsync(IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await QueryFirstPendingTaskCategoryIdAsync(scope.ServiceProvider);
    }

    private static async Task<Guid> QueryFirstPendingTaskCategoryIdAsync(IServiceProvider scopedServices)
    {
        CentraSaDbContext dbContext = scopedServices.GetRequiredService<CentraSaDbContext>();
        return await dbContext.Categories
            .Where(category => category.Scope == WorkItemScope.PendingTask)
            .OrderBy(category => category.SortOrder)
            .Select(category => category.Id)
            .FirstAsync();
    }

    private static void AssertFieldError(string html, string fieldName, string message)
    {
        string pattern = $"data-valmsg-for=\"{Regex.Escape(fieldName)}\"[^>]*>\\s*{Regex.Escape(message)}";
        Assert.Matches(pattern, html);
    }

    private static void AssertInputValue(string html, string fieldName, string value)
    {
        string pattern = $"<(?=[^>]*name=\"{Regex.Escape(fieldName)}\")(?=[^>]*value=\"{Regex.Escape(value)}\")[^>]+>";
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

    private sealed class TestSession : IAsyncDisposable
    {
        private TestSession(
            string storageRoot,
            WebApplicationFactory<Program> factory,
            HttpClient client)
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
                "CentraSA.PendingTaskFeedbackTests",
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
                string setupToken = ExtractAntiforgeryToken(setupHtml);
                const string password = "senha-feedback-2026";
                HttpResponseMessage setupResponse = await client.PostAsync(
                    new Uri("/conta/configuracao-inicial", UriKind.Relative),
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["UserName"] = "administrador",
                        ["Password"] = password,
                        ["ConfirmPassword"] = password,
                        ["__RequestVerificationToken"] = setupToken,
                    }));
                Assert.Equal(HttpStatusCode.Redirect, setupResponse.StatusCode);

                string loginHtml = await client.GetStringAsync(new Uri("/conta/entrar", UriKind.Relative));
                string loginToken = ExtractAntiforgeryToken(loginHtml);
                HttpResponseMessage loginResponse = await client.PostAsync(
                    new Uri("/conta/entrar", UriKind.Relative),
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["UserName"] = "administrador",
                        ["Password"] = password,
                        ["RememberMe"] = "false",
                        ["__RequestVerificationToken"] = loginToken,
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

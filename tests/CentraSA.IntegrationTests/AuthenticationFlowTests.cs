using System.Net;
using System.Text.RegularExpressions;
using CentraSA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CentraSA.IntegrationTests;

public sealed partial class AuthenticationFlowTests
{
    [Fact]
    public async Task FirstAdministratorCanBeCreatedAndAuthenticated()
    {
        string storageRoot = Path.Combine(Path.GetTempPath(), "CentraSA.WebTests", Guid.NewGuid().ToString("N"));

        try
        {
            using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Development");
                    builder.UseSetting("Storage:DataDirectory", storageRoot);
                    builder.UseSetting("SeedDemoData", "false");
                });
            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });

            HttpResponseMessage protectedPage = await client.GetAsync(new Uri("/", UriKind.Relative));
            Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
            Assert.Equal("/conta/entrar", GetLocationPath(protectedPage.Headers.Location));

            HttpResponseMessage tokensAsset = await client.GetAsync(new Uri("/css/tokens.css", UriKind.Relative));
            HttpResponseMessage layoutAsset = await client.GetAsync(new Uri("/css/layout.css", UriKind.Relative));
            HttpResponseMessage scriptAsset = await client.GetAsync(new Uri("/js/site.js", UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, tokensAsset.StatusCode);
            Assert.Equal(HttpStatusCode.OK, layoutAsset.StatusCode);
            Assert.Equal(HttpStatusCode.OK, scriptAsset.StatusCode);
            Assert.Contains("--surface-navy", await tokensAsset.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            HttpResponseMessage setupWithoutToken = await client.PostAsync(
                new Uri("/conta/configuracao-inicial", UriKind.Relative),
                new FormUrlEncodedContent([]));
            Assert.Equal(HttpStatusCode.BadRequest, setupWithoutToken.StatusCode);

            string setupHtml = await client.GetStringAsync(new Uri("/conta/configuracao-inicial", UriKind.Relative));
            Assert.Contains("<html lang=\"pt-BR\">", setupHtml, StringComparison.Ordinal);
            Assert.Contains("class=\"auth-brand-panel\"", setupHtml, StringComparison.Ordinal);
            Assert.Contains("href=\"#main-content\"", setupHtml, StringComparison.Ordinal);
            string setupToken = ExtractAntiforgeryToken(setupHtml);
            const string password = "senha-local-2026";
            HttpResponseMessage setupResult = await client.PostAsync(
                new Uri("/conta/configuracao-inicial", UriKind.Relative),
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["UserName"] = "administrador",
                    ["Password"] = password,
                    ["ConfirmPassword"] = password,
                    ["__RequestVerificationToken"] = setupToken,
                }));
            Assert.Equal(HttpStatusCode.Redirect, setupResult.StatusCode);
            Assert.Equal("/conta/entrar", GetLocationPath(setupResult.Headers.Location));

            HttpResponseMessage repeatedSetup = await client.GetAsync(new Uri("/conta/configuracao-inicial", UriKind.Relative));
            Assert.Equal(HttpStatusCode.Redirect, repeatedSetup.StatusCode);

            string loginToken = await GetAntiforgeryTokenAsync(client, "/conta/entrar");
            HttpResponseMessage loginResult = await client.PostAsync(
                new Uri("/conta/entrar", UriKind.Relative),
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["UserName"] = "administrador",
                    ["Password"] = password,
                    ["RememberMe"] = "false",
                    ["__RequestVerificationToken"] = loginToken,
                }));
            Assert.Equal(HttpStatusCode.Redirect, loginResult.StatusCode);
            Assert.Equal("/", GetLocationPath(loginResult.Headers.Location));

            HttpResponseMessage authenticatedPage = await client.GetAsync(new Uri("/", UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, authenticatedPage.StatusCode);
            string authenticatedHtml = await authenticatedPage.Content.ReadAsStringAsync();
            Assert.Contains("Central CentraSA", authenticatedHtml, StringComparison.Ordinal);
            Assert.Contains("class=\"app-sidebar\"", authenticatedHtml, StringComparison.Ordinal);
            Assert.Contains("aria-label=\"Navegação principal\"", authenticatedHtml, StringComparison.Ordinal);
            Assert.Contains("data-navigation-toggle", authenticatedHtml, StringComparison.Ordinal);
            Assert.Contains("Módulos operacionais", authenticatedHtml, StringComparison.Ordinal);

            string pendingTasksHtml = await client.GetStringAsync(new Uri("/pendencias", UriKind.Relative));
            Assert.Contains("Pendências da equipe", pendingTasksHtml, StringComparison.Ordinal);
            Assert.Contains("data-async-filter", pendingTasksHtml, StringComparison.Ordinal);

            Guid areaId;
            await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            {
                CentraSaDbContext dbContext = scope.ServiceProvider.GetRequiredService<CentraSaDbContext>();
                areaId = await dbContext.TeamAreas.Select(area => area.Id).FirstAsync();
            }

            string pendingTaskToken = ExtractAntiforgeryToken(pendingTasksHtml);
            HttpResponseMessage quickCreateResult = await client.PostAsync(
                new Uri("/pendencias/criacao-rapida", UriKind.Relative),
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Title"] = "Pendência criada pelo fluxo web",
                    ["ResponsibleAreaId"] = areaId.ToString(),
                    ["__RequestVerificationToken"] = pendingTaskToken,
                }));
            Assert.Equal(HttpStatusCode.Redirect, quickCreateResult.StatusCode);
            Assert.Equal("/pendencias", GetLocationPath(quickCreateResult.Headers.Location));

            await using (AsyncServiceScope verificationScope = factory.Services.CreateAsyncScope())
            {
                CentraSaDbContext verificationContext = verificationScope.ServiceProvider.GetRequiredService<CentraSaDbContext>();
                Assert.True(await verificationContext.PendingTasks.AnyAsync(task => task.Title == "Pendência criada pelo fluxo web"));
            }

            string updatedPendingTasksHtml = await client.GetStringAsync(new Uri("/pendencias", UriKind.Relative));
            Assert.Contains("Pendência criada pelo fluxo web", WebUtility.HtmlDecode(updatedPendingTasksHtml), StringComparison.Ordinal);

            using var partialRequest = new HttpRequestMessage(HttpMethod.Get, "/pendencias?Search=fluxo+web");
            partialRequest.Headers.Add("X-Requested-With", "fetch");
            HttpResponseMessage partialResponse = await client.SendAsync(partialRequest);
            string partialHtml = await partialResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, partialResponse.StatusCode);
            Assert.Contains("Pendência criada pelo fluxo web", WebUtility.HtmlDecode(partialHtml), StringComparison.Ordinal);
            Assert.DoesNotContain("<html", partialHtml, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        string html = await client.GetStringAsync(new Uri(path, UriKind.Relative));
        return ExtractAntiforgeryToken(html);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        Match match = AntiforgeryTokenPattern().Match(html);
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

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenPattern();
}

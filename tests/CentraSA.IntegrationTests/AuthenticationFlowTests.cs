using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

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

            HttpResponseMessage setupWithoutToken = await client.PostAsync(
                new Uri("/conta/configuracao-inicial", UriKind.Relative),
                new FormUrlEncodedContent([]));
            Assert.Equal(HttpStatusCode.BadRequest, setupWithoutToken.StatusCode);

            string setupToken = await GetAntiforgeryTokenAsync(client, "/conta/configuracao-inicial");
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
            Assert.Contains("Central CentraSA", await authenticatedPage.Content.ReadAsStringAsync(), StringComparison.Ordinal);
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

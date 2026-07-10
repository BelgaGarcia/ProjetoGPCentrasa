using CentraSA.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CentraSA.Web;

internal static class AccountMaintenance
{
    public static async Task<int> ResetAdministratorPasswordAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser? user = userManager.Users.SingleOrDefault();

        if (user is null)
        {
            Console.Error.WriteLine("Nenhum administrador foi configurado.");
            return 1;
        }

        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Execute o comando em um terminal interativo.");
            return 1;
        }

        Console.WriteLine($"Redefinindo a senha de '{user.UserName}'.");
        string password = ReadSecret("Nova senha: ");
        string confirmation = ReadSecret("Confirme a nova senha: ");
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("As senhas não correspondem.");
            return 1;
        }

        string token = await userManager.GeneratePasswordResetTokenAsync(user);
        IdentityResult result = await userManager.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                Console.Error.WriteLine(error.Description);
            }

            return 1;
        }

        Console.WriteLine("Senha redefinida com sucesso.");
        cancellationToken.ThrowIfCancellationRequested();
        return 0;
    }

    private static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        var characters = new List<char>();

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return new string(characters.ToArray());
            }

            if (key.Key == ConsoleKey.Backspace && characters.Count > 0)
            {
                characters.RemoveAt(characters.Count - 1);
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                characters.Add(key.KeyChar);
            }
        }
    }
}

using System.Net;
using CentraSA.Infrastructure.Identity;
using CentraSA.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CentraSA.Web.Controllers;

[Route("conta")]
public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TimeProvider timeProvider) : Controller
{
    [AllowAnonymous]
    [HttpGet("configuracao-inicial")]
    public IActionResult InitialSetup()
    {
        if (!IsLocalRequest())
        {
            return NotFound();
        }

        if (HasRegisteredUser())
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new InitialSetupViewModel());
    }

    [AllowAnonymous]
    [HttpPost("configuracao-inicial")]
    public async Task<IActionResult> InitialSetup(
        InitialSetupViewModel model,
        CancellationToken cancellationToken)
    {
        if (!IsLocalRequest())
        {
            return NotFound();
        }

        if (HasRegisteredUser())
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = model.UserName.Trim(),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };

        IdentityResult result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(error));
            }

            return View(model);
        }

        TempData["SuccessMessage"] = "Administrador criado. Entre com as credenciais definidas.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("entrar")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (!HasRegisteredUser())
        {
            return RedirectToAction(nameof(InitialSetup));
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost("entrar")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!HasRegisteredUser())
        {
            return RedirectToAction(nameof(InitialSetup));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.PasswordSignInAsync(
            model.UserName.Trim(),
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return !string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
                ? LocalRedirect(model.ReturnUrl)
                : RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(
            string.Empty,
            result.IsLockedOut
                ? "Acesso temporariamente bloqueado após tentativas inválidas. Aguarde cinco minutos."
                : "Nome de usuário ou senha inválidos.");
        return View(model);
    }

    [Authorize]
    [HttpPost("sair")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet("alterar-senha")]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost("alterar-senha")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        ApplicationUser? user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        IdentityResult result = await userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(error));
            }

            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "Senha alterada com sucesso.";
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet("acesso-negado")]
    public IActionResult AccessDenied() => View();

    private bool HasRegisteredUser() => userManager.Users.Any();

    private bool IsLocalRequest()
    {
        IPAddress? remoteAddress = HttpContext.Connection.RemoteIpAddress;
        return remoteAddress is null || IPAddress.IsLoopback(remoteAddress);
    }

    private static string TranslateIdentityError(IdentityError error) => error.Code switch
    {
        "DuplicateUserName" => "Esse nome de usuário já está em uso.",
        "PasswordTooShort" => "A senha deve ter pelo menos 12 caracteres.",
        "PasswordRequiresDigit" => "A senha deve conter pelo menos um número.",
        "PasswordRequiresLower" => "A senha deve conter pelo menos uma letra minúscula.",
        "PasswordMismatch" => "A senha atual está incorreta.",
        _ => "Não foi possível concluir a operação. Revise os dados informados.",
    };
}

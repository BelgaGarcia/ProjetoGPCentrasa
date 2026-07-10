using System.ComponentModel.DataAnnotations;

namespace CentraSA.Web.ViewModels.Account;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Informe o nome de usuário.")]
    [Display(Name = "Nome de usuário")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Manter conectado")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace CentraSA.Web.ViewModels.Account;

public sealed class InitialSetupViewModel
{
    [Required(ErrorMessage = "Informe o nome de usuário.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "O nome de usuário deve ter entre 3 e 50 caracteres.")]
    [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Use apenas letras, números, ponto, hífen ou sublinhado.")]
    [Display(Name = "Nome de usuário")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "A senha deve ter pelo menos 12 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a senha.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "A confirmação não corresponde à senha.")]
    [Display(Name = "Confirmar senha")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

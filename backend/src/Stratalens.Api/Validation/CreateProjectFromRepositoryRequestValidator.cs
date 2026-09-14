using FluentValidation;
using Stratalens.Api.Contracts;

namespace Stratalens.Api.Validation;

// Límites de longitud iguales a las columnas de ProjectConfiguration: un valor demasiado
// largo falla acá con un 400 claro, en vez de reventar en el INSERT con un 500.
public class CreateProjectFromRepositoryRequestValidator : AbstractValidator<CreateProjectFromRepositoryRequest>
{
    public CreateProjectFromRepositoryRequestValidator()
    {
        RuleFor(x => x.Owner)
            .NotEmpty().WithMessage("El owner del repositorio es obligatorio.")
            .MaximumLength(39).WithMessage("El owner no puede superar 39 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del repositorio es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede superar 100 caracteres.");
    }
}

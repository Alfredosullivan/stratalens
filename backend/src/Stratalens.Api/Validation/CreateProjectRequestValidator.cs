using FluentValidation;
using Stratalens.Api.Contracts;

namespace Stratalens.Api.Validation;

// Valida la entrada del usuario antes de que toque la base de datos (regla de RULES.md).
public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del proyecto es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede superar 200 caracteres.");
    }
}

using FluentValidation;
using server.Exceptions.Exceptions;
namespace server.Application.UseCases.People;

internal static class PeopleValidation
{
    public static bool Calendar(DateTime date) => date.Year is >= 2000 and <= 2100 && date.TimeOfDay == TimeSpan.Zero;
    public static bool Precision(decimal value, int digits) => Math.Abs(value) <= 9999999999m && decimal.Round(value, digits) == value;
    public static void Validate<T>(IValidator<T> validator, T request)
    {
        var result = validator.Validate(request);
        if (!result.IsValid) throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}

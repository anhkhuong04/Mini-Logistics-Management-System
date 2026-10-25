using FluentValidation.Results;
using MiniLogistics.Web.Endpoints.Browser;
using System.Text.Json;

namespace MiniLogistics.Web.Services;

public static class BrowserValidationProblem
{
    public static string FieldPath(string propertyName) => string.Join('.', propertyName.Split('.')
        .Select(JsonNamingPolicy.CamelCase.ConvertName));

    public static IResult From(HttpContext context, IEnumerable<ValidationFailure> failures) =>
        BrowserApiResultMapper.Problem(context, "Application.ValidationFailed", 400,
            failures.GroupBy(failure => FieldPath(failure.PropertyName)).ToDictionary(
                errors => errors.Key, errors => errors.Select(error => error.ErrorMessage).Distinct().ToArray()));
}

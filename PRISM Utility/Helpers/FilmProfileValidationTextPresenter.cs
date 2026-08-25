using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Helpers;

public static class FilmProfileValidationTextPresenter
{
    private const string GenericValidationResourceKey = "FilmProfile_Validation_GenericInputInvalid";

    public static string GetValidationIssueText(ScanFilmProfileValidationIssue? issue)
    {
        if (issue is null)
            return string.Empty;

        var resourceKey = ToResourceKey(issue.MessageKey);
        var fallback = GenericValidationResourceKey.GetLocalizedOrFallback("Review this film profile setting and try again.");
        return issue.MessageArguments.Count == 0
            ? resourceKey.GetLocalizedOrFallback(fallback)
            : resourceKey.GetLocalizedFormatOrFallback(fallback, issue.MessageArguments.Cast<object>().ToArray());
    }

    private static string ToResourceKey(string messageKey)
        => messageKey.Replace('.', '_');
}

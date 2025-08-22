using System.Text.RegularExpressions;

namespace ProPresenter_StageDisplayLayout_AutoSwitcher;

public static class StringUtil
{
    private static readonly string[] LibraryNamePatterns =
    {
        @"(?<=Libraries\/).+?(?=/)",
        @"(?<=Libraries\\).+?(?=\\)"
    };

    public static string ExtractLibraryNameFromPath(string file_path)
    {
        foreach (var pattern in LibraryNamePatterns)
        {
            var library = Regex.Match(file_path, pattern).Value;
            if (!string.IsNullOrEmpty(library))
            {
                return library;
            }
        }
        return string.Empty;
    }
}
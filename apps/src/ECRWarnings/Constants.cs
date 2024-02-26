using System.Collections.ObjectModel;

namespace ECRWarnings;

public static class Constants
{
    public static readonly ReadOnlyCollection<string> ECRRepositories = new(
        ["playground"]
    );
}
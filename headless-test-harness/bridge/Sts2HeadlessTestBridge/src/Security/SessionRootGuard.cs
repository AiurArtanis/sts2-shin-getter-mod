namespace Sts2HeadlessTestBridge.Security;

public static class SessionRootGuard
{
    public static string Validate(string value)
    {
        if (!Path.IsPathFullyQualified(value))
            throw new InvalidDataException("STS2_TEST_OUTPUT_ROOT must be absolute");
        string path = Path.GetFullPath(value);
        string root = Path.GetPathRoot(path) ?? "";
        if (StringComparer.OrdinalIgnoreCase.Equals(path.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar)))
            throw new InvalidDataException("filesystem root cannot be a test output root");
        Directory.CreateDirectory(path);
        DirectoryInfo? current = new(path);
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"test output root traverses a reparse point: {current.FullName}");
            current = current.Parent;
        }
        return path;
    }
}

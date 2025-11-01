namespace CodingAgent.Core;

using System;
using System.IO;

/// <summary>
/// Validates that file operations stay within a specified working directory.
/// </summary>
public class PathValidator
{
    private readonly string? _workingDirectory;
    private readonly string _resolvedWorkingDirectory;

    public PathValidator(string? workingDirectory)
    {
        _workingDirectory = workingDirectory;
        if (string.IsNullOrEmpty(workingDirectory))
        {
            _resolvedWorkingDirectory = Directory.GetCurrentDirectory();
        }
        else
        {
            _resolvedWorkingDirectory = Path.GetFullPath(workingDirectory);
            if (!Directory.Exists(_resolvedWorkingDirectory))
            {
                throw new DirectoryNotFoundException($"Working directory does not exist: {_resolvedWorkingDirectory}");
            }
        }
    }

    /// <summary>
    /// Gets the resolved working directory.
    /// </summary>
    public string WorkingDirectory => _resolvedWorkingDirectory;

    /// <summary>
    /// Checks if a path is within the working directory. Relative paths are treated as relative to working directory.
    /// </summary>
    /// <returns>The full path if valid, throws if outside working directory.</returns>
    public string ValidatePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Path cannot be empty");
        }

        // Resolve the path relative to working directory if it's relative
        string resolvedPath;
        if (Path.IsPathRooted(path))
        {
            resolvedPath = Path.GetFullPath(path);
        }
        else
        {
            resolvedPath = Path.GetFullPath(Path.Combine(_resolvedWorkingDirectory, path));
        }

        // Normalize both paths for comparison
        var normalizedResolved = Path.GetFullPath(resolvedPath);
        var normalizedWorking = Path.GetFullPath(_resolvedWorkingDirectory);

        // Ensure the resolved path is within working directory
        if (!normalizedResolved.StartsWith(normalizedWorking, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                $"Access denied: path '{path}' resolves to '{normalizedResolved}' which is outside working directory '{normalizedWorking}'");
        }

        return normalizedResolved;
    }

    /// <summary>
    /// Checks if a directory path is within the working directory.
    /// </summary>
    public string ValidateDirectoryPath(string path)
    {
        return ValidatePath(path);
    }
}
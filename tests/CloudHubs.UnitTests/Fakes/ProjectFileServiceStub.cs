using CloudHubs.Application.Modules.Projects.Interfaces;

namespace CloudHubs.UnitTests.Fakes;

public class ProjectFileServiceStub : IProjectFileService
{
    public bool TreeCalled { get; private set; }
    public string? LastTreeRootPath { get; private set; }
    public ProjectFileItem TreeToReturn { get; set; } = new("root", "", true, 0, []);
    public Dictionary<string, string> StoredFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<ProjectFileItem> GetFileTreeAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        TreeCalled = true;
        LastTreeRootPath = rootPath;
        return Task.FromResult(TreeToReturn);
    }

    public Task<string> ReadFileAsync(string rootPath, string relativePath, CancellationToken cancellationToken = default)
    {
        var key = $"{rootPath}/{relativePath}".Replace('\\', '/');
        if (StoredFiles.TryGetValue(key, out var content))
        {
            return Task.FromResult(content);
        }
        if (StoredFiles.TryGetValue(relativePath, out var relativeContent))
        {
            return Task.FromResult(relativeContent);
        }
        if (relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("File not found");
        }
        return Task.FromResult("content of " + relativePath);
    }

    public Task WriteFileAsync(string rootPath, string relativePath, string content, CancellationToken cancellationToken = default)
    {
        var key = $"{rootPath}/{relativePath}".Replace('\\', '/');
        StoredFiles[key] = content;
        StoredFiles[relativePath] = content;
        return Task.CompletedTask;
    }

    public Task CreateItemAsync(string rootPath, string relativePath, bool isDirectory, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RenameItemAsync(string rootPath, string oldRelativePath, string newRelativePath, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteItemAsync(string rootPath, string relativePath, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProjectSearchResult>> SearchFilesAsync(string rootPath, string query, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProjectSearchResult> results = [new ProjectSearchResult("src/server.js", 1, "const express = require('express');")];
        return Task.FromResult(results);
    }

    public Task<byte[]> ExportZipAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Array.Empty<byte>());
    }
}


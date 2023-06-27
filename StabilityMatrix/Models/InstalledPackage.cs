﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StabilityMatrix.Models;

/// <summary>
/// Profile information for a user-installed package.
/// </summary>
public class InstalledPackage
{
    // Unique ID for the installation
    public Guid Id { get; set; }
    // User defined name
    public string? DisplayName { get; set; }
    // Package name
    public string? PackageName { get; set; }
    // Package version
    public string? PackageVersion { get; set; }
    public string? InstalledBranch { get; set; }
    public string? DisplayVersion { get; set; }
    
    // Old type absolute path
    [Obsolete("Use LibraryPath instead. (Kept for migration)")]
    public string? Path { get; set; }
    
    /// <summary>
    /// Relative path from the library root.
    /// </summary>
    public string? LibraryPath { get; set; }
    
    /// <summary>
    /// Full path to the package, using LibraryPath and GlobalConfig.LibraryDir.
    /// </summary>
    public string? FullPath => LibraryPath != null ? System.IO.Path.Combine(GlobalConfig.LibraryDir, LibraryPath) : null;
    
    public string? LaunchCommand { get; set; }
    public List<LaunchOption>? LaunchArgs { get; set; }
    public DateTimeOffset? LastUpdateCheck { get; set; }

    [JsonIgnore] public bool UpdateAvailable { get; set; }

    /// <summary>
    /// Get the path as a relative sub-path of the relative path.
    /// If not a sub-path, return null.
    /// </summary>
    internal static string? GetSubPath(string relativeTo, string path)
    {
        var relativePath = System.IO.Path.GetRelativePath(relativeTo, path);
        // GetRelativePath returns the path if it's not relative
        if (relativePath == path) return null;
        // Further check if the path is a sub-path of the library
        var isSubPath = relativePath != "."
                    && relativePath != ".."
                    && !relativePath.StartsWith("..\\")
                    && !System.IO.Path.IsPathRooted(relativePath);
        return isSubPath ? relativePath : null;
    } 

    /// <summary>
    /// Migrates the old Path to the new LibraryPath.
    /// If libraryDirectory is null, GlobalConfig.LibraryDir is used.
    /// </summary>
    /// <returns>True if the path was migrated, false otherwise.</returns>
    public bool TryPureMigratePath(string? libraryDirectory = null)
    {
#pragma warning disable CS0618
        var oldPath = Path;
#pragma warning restore CS0618
        if (oldPath == null) return false;
        
        // Check if the path is a sub-path of the library
        var library = libraryDirectory ?? GlobalConfig.LibraryDir;
        var relativePath = GetSubPath(library, oldPath);
        
        // If so we migrate without any IO operations
        if (relativePath != null)
        {
            LibraryPath = relativePath;
#pragma warning disable CS0618
            Path = null;
#pragma warning restore CS0618
            return true;
        }

        return false;
    }

    /// <summary>
    /// Migrate the old Path to the new LibraryPath.
    /// If libraryDirectory is null, GlobalConfig.LibraryDir is used.
    /// Will move the package directory to Library/Packages if not relative.
    /// </summary>
    public async Task MigratePath(string? libraryDirectory = null)
    {
#pragma warning disable CS0618
        var oldPath = Path;
#pragma warning restore CS0618
        if (oldPath == null) return;
        
        // Try using pure migration first
        if (TryPureMigratePath(libraryDirectory)) return;
        
        // If not, we need to move the package directory
        var packageFolderName = new DirectoryInfo(oldPath).Name;
        
        // Get the new Library/Packages path
        var library = libraryDirectory ?? GlobalConfig.LibraryDir;
        var newPackagesDir = System.IO.Path.Combine(library, "Packages");
        
        // Get the new target path
        var newPackagePath = System.IO.Path.Combine(newPackagesDir, packageFolderName);
        // Ensure it is not already there, if so, add a suffix until it's not
        var suffix = 2;
        while (Directory.Exists(newPackagePath))
        {
            newPackagePath = System.IO.Path.Combine(newPackagesDir, $"{packageFolderName}-{suffix}");
            suffix++;
        }
        
        // Move the package directory
        await Task.Run(() => Directory.Move(oldPath, newPackagePath));

        // Update the paths
#pragma warning disable CS0618
        Path = null;
#pragma warning restore CS0618
        LibraryPath = System.IO.Path.Combine("Packages", packageFolderName);
    }
}

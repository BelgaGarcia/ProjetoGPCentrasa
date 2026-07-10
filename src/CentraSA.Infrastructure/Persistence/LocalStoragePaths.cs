using Microsoft.Extensions.Configuration;

namespace CentraSA.Infrastructure.Persistence;

public sealed class LocalStoragePaths
{
    private LocalStoragePaths(DirectoryInfo rootDirectory)
    {
        RootDirectory = rootDirectory;
        DataDirectory = new DirectoryInfo(Path.Combine(rootDirectory.FullName, "Data"));
        BackupDirectory = new DirectoryInfo(Path.Combine(rootDirectory.FullName, "Backups"));
        DataProtectionKeysDirectory = new DirectoryInfo(Path.Combine(rootDirectory.FullName, "Keys"));
        DatabaseFile = new FileInfo(Path.Combine(DataDirectory.FullName, "centrasa.db"));
    }

    public DirectoryInfo RootDirectory { get; }

    public DirectoryInfo DataDirectory { get; }

    public DirectoryInfo BackupDirectory { get; }

    public DirectoryInfo DataProtectionKeysDirectory { get; }

    public FileInfo DatabaseFile { get; }

    public static LocalStoragePaths FromConfiguration(IConfiguration configuration)
    {
        string? configuredRoot = configuration["Storage:DataDirectory"];
        string root = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CentraSA")
            : configuredRoot;

        return new LocalStoragePaths(new DirectoryInfo(Path.GetFullPath(root)));
    }

    public void EnsureDirectoriesExist()
    {
        RootDirectory.Create();
        DataDirectory.Create();
        BackupDirectory.Create();
        DataProtectionKeysDirectory.Create();
    }
}

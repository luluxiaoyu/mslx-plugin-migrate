using System;
using System.Collections.Generic;

namespace MSLX.Plugin.Migrate.Models;

public class ExportOptions
{
    public bool ExportServers { get; set; } = true;
    public List<int>? SelectedServerIds { get; set; }

    public bool ExportFrp { get; set; } = true;
    public List<int>? SelectedFrpIds { get; set; }

    public bool ExportUsers { get; set; } = true;
    public bool ExportSystemSettings { get; set; } = true;
    public bool ExportPlugins { get; set; } = true;
}

public class MigrationManifest
{
    public string Version { get; set; } = "1.0.0";
    public string PluginVersion { get; set; } = "1.0.0";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string SourceOs { get; set; } = Environment.OSVersion.Platform.ToString();
    public bool IncludesServers { get; set; }
    public List<int> ServerIds { get; set; } = new();
    public bool IncludesFrp { get; set; }
    public List<int> FrpIds { get; set; } = new();
    public bool IncludesUsers { get; set; }
    public bool IncludesSystemSettings { get; set; }
    public bool IncludesPlugins { get; set; }
}

public class ImportOptions
{
    public bool ImportServers { get; set; } = true;
    public bool ImportFrp { get; set; } = true;
    public bool ImportUsers { get; set; } = false;
    public bool ImportSystemSettings { get; set; } = false;
    public bool ImportPlugins { get; set; } = false;
}

public class ImportByUploadIdRequest
{
    public string UploadId { get; set; } = string.Empty;
    public ImportOptions? Options { get; set; }
}

public class ImportFromLocalFileRequest
{
    public string FileName { get; set; } = string.Empty;
    public ImportOptions? Options { get; set; }
}

public class ExportableItemsDto
{
    public List<ServerItemDto> Servers { get; set; } = new();
    public List<FrpItemDto> FrpTunnels { get; set; } = new();
    public int UserCount { get; set; }
    public int PluginCount { get; set; }
    public string AppDataPath { get; set; } = string.Empty;
    public string ImportsPath { get; set; } = string.Empty;
}

public class ServerItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Core { get; set; } = string.Empty;
    public string Base { get; set; } = string.Empty;
}

public class FrpItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
}

public class BackupItemDto
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileSizeText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

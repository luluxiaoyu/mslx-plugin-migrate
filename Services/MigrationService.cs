using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MSLX.Plugin.Migrate.Models;
using MSLX.SDK;
using MSLX.SDK.Models;
using MSLX.SDK.Models.Files;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MSLX.Plugin.Migrate.Services;

public class MigrationService
{
    private readonly string _appDataPath;
    private readonly string _exportsDir;
    private readonly string _importsDir;

    public MigrationService()
    {
        _appDataPath = SDK.MSLX.Config.GetAppDataPath();
        string pluginDataPath = MSLXPluginEntry.Instance.Config().GetDataPath();
        _exportsDir = Path.Combine(pluginDataPath, "Backups");
        _importsDir = Path.Combine(pluginDataPath, "Imports");

        if (!Directory.Exists(_exportsDir)) Directory.CreateDirectory(_exportsDir);
        if (!Directory.Exists(_importsDir)) Directory.CreateDirectory(_importsDir);
    }

    public ExportableItemsDto GetExportableItems()
    {
        var servers = SDK.MSLX.Config.Servers.GetServerList()
            .Select(s => new ServerItemDto
            {
                Id = s.ID,
                Name = s.Name ?? $"实例 #{s.ID}",
                Core = s.Core ?? string.Empty,
                Base = s.Base ?? string.Empty
            }).ToList();

        var frpTunnels = SDK.MSLX.Config.Frp.GetFrpList()
            .Select(f => new FrpItemDto
            {
                Id = f["ID"]?.Value<int>() ?? 0,
                Name = f["Name"]?.Value<string>() ?? string.Empty,
                Service = f["Service"]?.Value<string>() ?? string.Empty
            }).ToList();

        int userCount = SDK.MSLX.Config.Users.GetAllUsers().Count;

        string pluginsDir = Path.Combine(_appDataPath, "Plugins");
        int pluginCount = Directory.Exists(pluginsDir)
            ? Directory.GetFiles(pluginsDir, "*.dll").Length
            : 0;

        return new ExportableItemsDto
        {
            Servers = servers,
            FrpTunnels = frpTunnels,
            UserCount = userCount,
            PluginCount = pluginCount,
            AppDataPath = _appDataPath,
            ImportsPath = _importsDir
        };
    }

    public string StartExportTask(ExportOptions options, string userId)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string zipFileName = $"MSLX_Migration_{timestamp}.zip";
        string finalZipPath = Path.Combine(_exportsDir, zipFileName);
        string tempZipPath = Path.Combine(_exportsDir, $"{zipFileName}.tmp");

        var (task, token) = SDK.MSLX.Tasks.CreateTask(
            userId,
            0,
            TaskType.Export,
            "整机迁移包导出",
            zipFileName
        );

        _ = Task.Run(async () =>
        {
            try
            {
                SDK.MSLX.Tasks.UpdateProgress(task.Id, 5, "正在整理导出清单...", TaskState.Running);

                if (File.Exists(tempZipPath)) File.Delete(tempZipPath);

                var manifest = new MigrationManifest
                {
                    IncludesServers = options.ExportServers,
                    IncludesFrp = options.ExportFrp,
                    IncludesUsers = options.ExportUsers,
                    IncludesSystemSettings = options.ExportSystemSettings,
                    IncludesPlugins = options.ExportPlugins
                };

                using (var zipToOpen = new FileStream(tempZipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                using (var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                {
                    // 1. 系统设置 (config.json)
                    if (options.ExportSystemSettings)
                    {
                        SDK.MSLX.Tasks.UpdateProgress(task.Id, 15, "正在导出系统配置...");
                        string configPath = Path.Combine(SDK.MSLX.Config.GetAppConfigPath(), "config.json");
                        if (File.Exists(configPath))
                        {
                            archive.CreateEntryFromFile(configPath, "configs/config.json");
                        }
                    }

                    // 2. 用户数据 (Users.json)
                    if (options.ExportUsers)
                    {
                        SDK.MSLX.Tasks.UpdateProgress(task.Id, 25, "正在导出用户数据...");
                        string usersPath = Path.Combine(SDK.MSLX.Config.GetAppConfigPath(), "Users.json");
                        if (File.Exists(usersPath))
                        {
                            archive.CreateEntryFromFile(usersPath, "configs/Users.json");
                        }
                    }

                    // 3. FRP 隧道
                    if (options.ExportFrp)
                    {
                        SDK.MSLX.Tasks.UpdateProgress(task.Id, 35, "正在导出 FRP 隧道配置...");
                        var allFrp = SDK.MSLX.Config.Frp.GetFrpList();
                        var selectedFrp = options.SelectedFrpIds != null && options.SelectedFrpIds.Any()
                            ? allFrp.Where(f => options.SelectedFrpIds.Contains(f["ID"]?.Value<int>() ?? -1)).ToList()
                            : allFrp;

                        manifest.FrpIds = selectedFrp.Select(f => f["ID"]?.Value<int>() ?? 0).ToList();

                        var frpJsonEntry = archive.CreateEntry("configs/FrpList.json");
                        using (var entryStream = frpJsonEntry.Open())
                        using (var writer = new StreamWriter(entryStream))
                        {
                            await writer.WriteAsync(new JArray(selectedFrp).ToString());
                        }

                        // 打包各自的 frpc 配置文件
                        foreach (var frp in selectedFrp)
                        {
                            int frpId = frp["ID"]?.Value<int>() ?? 0;
                            string frpConfigFolder = Path.Combine(SDK.MSLX.Config.GetAppConfigPath(), "Frpc", frpId.ToString());
                            if (Directory.Exists(frpConfigFolder))
                            {
                                AddDirectoryToArchive(archive, frpConfigFolder, $"frpc/{frpId}");
                            }
                        }
                    }

                    // 4. 插件与插件数据
                    if (options.ExportPlugins)
                    {
                        SDK.MSLX.Tasks.UpdateProgress(task.Id, 45, "正在导出插件数据...");
                        string pluginsDir = Path.Combine(_appDataPath, "Plugins");
                        if (Directory.Exists(pluginsDir))
                        {
                            foreach (var file in Directory.GetFiles(pluginsDir, "*.dll", SearchOption.AllDirectories))
                            {
                                string fileName = Path.GetFileName(file);
                                if (fileName.Equals("MSLX.Plugin.Migrate.dll", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                string relPath = Path.GetRelativePath(pluginsDir, file);
                                archive.CreateEntryFromFile(file, $"plugins/bin/{relPath.Replace('\\', '/')}");
                            }
                        }

                        string pluginsDataDir = Path.Combine(_appDataPath, "PluginsData");
                        if (Directory.Exists(pluginsDataDir))
                        {
                            foreach (var file in Directory.GetFiles(pluginsDataDir, "*.*", SearchOption.AllDirectories))
                            {
                                string relToData = Path.GetRelativePath(pluginsDataDir, file);
                                if (relToData.StartsWith("mslx-plugin-migrate", StringComparison.OrdinalIgnoreCase) ||
                                    relToData.StartsWith("mslx-plugin-migrate/", StringComparison.OrdinalIgnoreCase) ||
                                    relToData.StartsWith("mslx-plugin-migrate\\", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                string relPath = Path.GetRelativePath(pluginsDataDir, file);
                                archive.CreateEntryFromFile(file, $"plugins/data/{relPath.Replace('\\', '/')}");
                            }
                        }
                    }

                    // 5. 服务端实例
                    if (options.ExportServers)
                    {
                        var allServers = SDK.MSLX.Config.Servers.GetServerList();
                        var selectedServers = options.SelectedServerIds != null && options.SelectedServerIds.Any()
                            ? allServers.Where(s => options.SelectedServerIds.Contains(s.ID)).ToList()
                            : allServers;

                        manifest.ServerIds = selectedServers.Select(s => s.ID).ToList();

                        var serverJsonEntry = archive.CreateEntry("configs/ServerList.json");
                        using (var entryStream = serverJsonEntry.Open())
                        using (var writer = new StreamWriter(entryStream))
                        {
                            await writer.WriteAsync(JArray.FromObject(selectedServers).ToString());
                        }

                        int totalServers = selectedServers.Count;
                        for (int i = 0; i < totalServers; i++)
                        {
                            var s = selectedServers[i];
                            int percent = 50 + (int)((i + 1) / (double)totalServers * 40);
                            SDK.MSLX.Tasks.UpdateProgress(task.Id, percent, $"正在打包实例 [{s.Name ?? s.ID.ToString()}] ({i + 1}/{totalServers})...");

                            if (!string.IsNullOrEmpty(s.Base) && Directory.Exists(s.Base))
                            {
                                AddDirectoryToArchive(archive, s.Base, $"servers/{s.ID}");
                            }
                        }
                    }

                    // 6. 写入清单 manifest.json
                    var manifestEntry = archive.CreateEntry("manifest.json");
                    using (var entryStream = manifestEntry.Open())
                    using (var writer = new StreamWriter(entryStream))
                    {
                        await writer.WriteAsync(JsonConvert.SerializeObject(manifest, Formatting.Indented));
                    }
                }

                // 流完全关闭后，原子重命名为正式 zip 文件（保证没打包完成前绝对不显示）
                if (File.Exists(finalZipPath)) File.Delete(finalZipPath);
                File.Move(tempZipPath, finalZipPath);

                SDK.MSLX.Tasks.UpdateProgress(task.Id, 100, "导出完成！");
                SDK.MSLX.Tasks.SetSuccess(task.Id, $"迁移包已生成：{zipFileName}");
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(tempZipPath)) try { File.Delete(tempZipPath); } catch { }
                SDK.MSLX.Tasks.SetFailed(task.Id, "用户已取消导出任务");
            }
            catch (Exception ex)
            {
                if (File.Exists(tempZipPath)) try { File.Delete(tempZipPath); } catch { }
                SDK.MSLX.Logger.Error($"[MSLX Migration] 导出异常: {ex.Message}");
                SDK.MSLX.Tasks.SetFailed(task.Id, $"导出失败: {ex.Message}");
            }
        }, token);

        return zipFileName;
    }

    private static readonly HashSet<string> MachineSpecificKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "listenHost",
        "listenPort",
        "JwtSecret",
        "apiKey",
        "IsSlaveMode",
        "SlaveLinkKey",
        "JavaCache",
        "SSLCertificate",
        "SSLCertificateKey",
        "KnownProxies"
    };

    public bool StartImportFromUploadId(string uploadId, ImportOptions options, string userId)
    {
        if (string.IsNullOrWhiteSpace(uploadId) || !System.Text.RegularExpressions.Regex.IsMatch(uploadId, "^[a-fA-F0-9]{32}$"))
        {
            return false;
        }

        string mergedPath = Path.Combine(_appDataPath, "Temp", "Uploads", $"{uploadId}.tmp");
        if (!File.Exists(mergedPath))
        {
            return false;
        }

        string targetZip = Path.Combine(_importsDir, $"import_{uploadId}.zip");
        if (File.Exists(targetZip)) File.Delete(targetZip);
        File.Move(mergedPath, targetZip);

        StartImportTask(targetZip, options, userId);
        return true;
    }

    public bool StartImportFromLocalFile(string fileName, ImportOptions options, string userId)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..") || Path.GetFileName(fileName) != fileName)
        {
            return false;
        }

        string fullPath = Path.Combine(_importsDir, fileName);
        if (!File.Exists(fullPath))
        {
            return false;
        }

        StartImportTask(fullPath, options, userId);
        return true;
    }

    public void StartImportTask(string zipFilePath, ImportOptions options, string userId)
    {
        string fileName = Path.GetFileName(zipFilePath);
        var (task, token) = SDK.MSLX.Tasks.CreateTask(
            userId,
            0,
            TaskType.Decompress,
            "整机迁移包导入",
            fileName
        );

        _ = Task.Run(async () =>
        {
            string extractTempDir = Path.Combine(_importsDir, $"extract_{Guid.NewGuid():N}");
            try
            {
                SDK.MSLX.Tasks.UpdateProgress(task.Id, 10, "正在解压迁移包...", TaskState.Running);
                Directory.CreateDirectory(extractTempDir);
                ZipFile.ExtractToDirectory(zipFilePath, extractTempDir, true);

                string manifestFile = Path.Combine(extractTempDir, "manifest.json");
                MigrationManifest? manifest = null;
                if (File.Exists(manifestFile))
                {
                    try
                    {
                        manifest = JsonConvert.DeserializeObject<MigrationManifest>(await File.ReadAllTextAsync(manifestFile));
                    }
                    catch { }
                }

                // 1. 导入系统全局配置（智能合并，严格保护本地网络与安全敏感项）
                if (options.ImportSystemSettings)
                {
                    SDK.MSLX.Tasks.UpdateProgress(task.Id, 30, "正在智能合并系统配置...");
                    string sourceCfgFile = Path.Combine(extractTempDir, "configs", "config.json");
                    if (File.Exists(sourceCfgFile))
                    {
                        string localCfgFile = Path.Combine(SDK.MSLX.Config.GetAppConfigPath(), "config.json");
                        if (File.Exists(localCfgFile))
                        {
                            var localJson = JObject.Parse(await File.ReadAllTextAsync(localCfgFile));
                            var sourceJson = JObject.Parse(await File.ReadAllTextAsync(sourceCfgFile));

                            foreach (var prop in sourceJson.Properties())
                            {
                                if (MachineSpecificKeys.Contains(prop.Name))
                                {
                                    continue;
                                }
                                localJson[prop.Name] = prop.Value;
                            }

                            await File.WriteAllTextAsync(localCfgFile, localJson.ToString(Formatting.Indented));
                        }
                    }
                }

                // 2. 导入用户数据（纯追加模式：遇到已存在的同名用户名跳过）
                if (options.ImportUsers)
                {
                    SDK.MSLX.Tasks.UpdateProgress(task.Id, 45, "正在追加导入用户数据...");
                    string sourceUsersFile = Path.Combine(extractTempDir, "configs", "Users.json");
                    if (File.Exists(sourceUsersFile))
                    {
                        string localUsersFile = Path.Combine(SDK.MSLX.Config.GetAppConfigPath(), "Users.json");
                        var sourceUsers = JArray.Parse(await File.ReadAllTextAsync(sourceUsersFile));

                        JArray localUsers = File.Exists(localUsersFile)
                            ? JArray.Parse(await File.ReadAllTextAsync(localUsersFile))
                            : new JArray();

                        var existingUsernames = new HashSet<string>(
                            localUsers.Select(u => u["Username"]?.Value<string>() ?? "").Where(s => !string.IsNullOrEmpty(s)),
                            StringComparer.OrdinalIgnoreCase
                        );

                        var existingUserIds = new HashSet<string>(
                            localUsers.Select(u => u["UserId"]?.Value<string>() ?? "").Where(s => !string.IsNullOrEmpty(s)),
                            StringComparer.OrdinalIgnoreCase
                        );

                        bool changed = false;
                        foreach (var u in sourceUsers)
                        {
                            string? username = u["Username"]?.Value<string>();
                            if (string.IsNullOrEmpty(username) || existingUsernames.Contains(username))
                            {
                                continue;
                            }

                            string? userIdVal = u["UserId"]?.Value<string>();
                            if (string.IsNullOrEmpty(userIdVal) || existingUserIds.Contains(userIdVal))
                            {
                                u["UserId"] = Guid.NewGuid().ToString("N");
                            }

                            localUsers.Add(u);
                            existingUsernames.Add(username);
                            changed = true;
                        }

                        if (changed)
                        {
                            await File.WriteAllTextAsync(localUsersFile, localUsers.ToString(Formatting.Indented));
                        }
                    }
                }

                // 3. 导入 FRP 隧道配置（纯追加模式：若 ID 冲突自动生成新 ID 追加）
                if (options.ImportFrp)
                {
                    SDK.MSLX.Tasks.UpdateProgress(task.Id, 55, "正在追加恢复 FRP 隧道配置...");
                    string frpListPath = Path.Combine(extractTempDir, "configs", "FrpList.json");
                    if (File.Exists(frpListPath))
                    {
                        var frpList = JArray.Parse(await File.ReadAllTextAsync(frpListPath));
                        string frpcTargetBase = Path.Combine(SDK.MSLX.Config.GetAppConfigPath(), "Frpc");
                        Directory.CreateDirectory(frpcTargetBase);

                        foreach (var item in frpList)
                        {
                            int origFrpId = item["ID"]?.Value<int>() ?? 0;
                            string name = item["Name"]?.Value<string>() ?? $"Tunnel_{origFrpId}";
                            string service = item["Service"]?.Value<string>() ?? "";
                            string configType = item["ConfigType"]?.Value<string>() ?? "toml";

                            int targetFrpId = origFrpId;
                            // 检查本地是否存在冲突
                            if (SDK.MSLX.Config.Frp.IsFrpIdValid(origFrpId))
                            {
                                targetFrpId = SDK.MSLX.Config.Frp.GenerateFrpId();
                                name = $"{name} (导入_{targetFrpId})";
                            }

                            string sourceFrpcDir = Path.Combine(extractTempDir, "frpc", origFrpId.ToString());
                            string targetFrpcDir = Path.Combine(frpcTargetBase, targetFrpId.ToString());

                            if (Directory.Exists(sourceFrpcDir))
                            {
                                DirectoryCopy(sourceFrpcDir, targetFrpcDir, true);
                            }

                            string cfgFile = Path.Combine(targetFrpcDir, $"frpc.{configType}");
                            string cfgContent = File.Exists(cfgFile) ? await File.ReadAllTextAsync(cfgFile) : "";

                            SDK.MSLX.Config.Frp.CreateFrpConfig(name, service, configType, cfgContent);
                        }
                    }
                }

                // 4. 导入插件数据
                if (options.ImportPlugins)
                {
                    SDK.MSLX.Tasks.UpdateProgress(task.Id, 65, "正在导入插件与插件数据...");
                    string pluginBinSourceDir = Path.Combine(extractTempDir, "plugins", "bin");
                    string pluginTargetDir = Path.Combine(_appDataPath, "Plugins");
                    if (Directory.Exists(pluginBinSourceDir))
                    {
                        DirectoryCopy(pluginBinSourceDir, pluginTargetDir, true);
                    }

                    string pluginDataSourceDir = Path.Combine(extractTempDir, "plugins", "data");
                    string pluginDataTargetDir = Path.Combine(_appDataPath, "PluginsData");
                    if (Directory.Exists(pluginDataSourceDir))
                    {
                        DirectoryCopy(pluginDataSourceDir, pluginDataTargetDir, true);
                    }
                }

                // 5. 导入服务端实例（纯追加模式：若 ID 冲突自动分配新 ID，自适应重写路径并关闭自启）
                if (options.ImportServers)
                {
                    SDK.MSLX.Tasks.UpdateProgress(task.Id, 75, "正在自适应恢复服务端实例(纯追加模式)...");
                    string serverListPath = Path.Combine(extractTempDir, "configs", "ServerList.json");
                    if (File.Exists(serverListPath))
                    {
                        var serverArray = JArray.Parse(await File.ReadAllTextAsync(serverListPath));
                        var servers = serverArray.Select(s => s.ToObject<McServerInfo.ServerInfo>()!).ToList();

                        int count = servers.Count;
                        for (int i = 0; i < count; i++)
                        {
                            var s = servers[i];
                            int percent = 75 + (int)((i + 1) / (double)count * 20);
                            SDK.MSLX.Tasks.UpdateProgress(task.Id, percent, $"正在自适应恢复实例 [{s.Name ?? s.ID.ToString()}]...");

                            // 检查本地 ID 是否冲突
                            bool idConflict = SDK.MSLX.Config.Servers.GetServer((uint)s.ID) != null;
                            uint targetServerId = idConflict ? SDK.MSLX.Config.Servers.GenerateServerId() : (uint)s.ID;
                            if (idConflict)
                            {
                                s.Name = $"{s.Name} (导入_{targetServerId})";
                            }

                            string sourceServerDir = Path.Combine(extractTempDir, "servers", s.ID.ToString());
                            string targetServerDir = Path.Combine(_appDataPath, "Servers", targetServerId.ToString());

                            if (Directory.Exists(sourceServerDir))
                            {
                                DirectoryCopy(sourceServerDir, targetServerDir, true);
                            }

                            s.ID = (int)targetServerId;
                            s.Base = targetServerDir;
                            s.RunOnStartup = false;
                            s.AutoRestart = false;

                            // 跨平台启动参数 Core 自适应转换（@保持开头，转换 win_args.txt 与 unix_args.txt）
                            AdaptServerArgsForCurrentPlatform(s);

                            // 始终作为新实例追加写入，绝不覆盖已有实例
                            SDK.MSLX.Config.Servers.CreateServer(s);
                        }
                    }
                }

                SDK.MSLX.Tasks.UpdateProgress(task.Id, 100, "整机迁移导入完成！");
                SDK.MSLX.Tasks.SetSuccess(task.Id, "数据已成功导入并完成自适应路径修复！");
            }
            catch (Exception ex)
            {
                SDK.MSLX.Logger.Error($"[MSLX Migration] 导入异常: {ex.Message}");
                SDK.MSLX.Tasks.SetFailed(task.Id, $"导入失败: {ex.Message}");
            }
            finally
            {
                // 清理解压临时目录
                if (Directory.Exists(extractTempDir))
                {
                    try { Directory.Delete(extractTempDir, true); } catch { }
                }
            }
        }, token);
    }

    public string? GetExportFilePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return null;
        }

        string fullPath = Path.Combine(_exportsDir, fileName);
        return File.Exists(fullPath) ? fullPath : null;
    }

    public List<BackupItemDto> GetBackupFiles()
    {
        return ScanZipDirectory(_exportsDir);
    }

    public List<BackupItemDto> GetImportFolderFiles()
    {
        return ScanZipDirectory(_importsDir);
    }

    private static List<BackupItemDto> ScanZipDirectory(string directory)
    {
        var list = new List<BackupItemDto>();
        if (!Directory.Exists(directory)) return list;

        var files = Directory.GetFiles(directory, "*.zip");
        foreach (var file in files)
        {
            try
            {
                var fi = new FileInfo(file);
                if (fi.Length == 0) continue;

                list.Add(new BackupItemDto
                {
                    FileName = fi.Name,
                    FileSize = fi.Length,
                    FileSizeText = FormatFileSize(fi.Length),
                    CreatedAt = fi.CreationTime
                });
            }
            catch
            {
            }
        }

        return list.OrderByDescending(b => b.CreatedAt).ToList();
    }

    public bool DeleteBackupFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..") || Path.GetFileName(fileName) != fileName)
        {
            return false;
        }

        string fullPath = Path.Combine(_exportsDir, fileName);
        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                return true;
            }
            catch (Exception ex)
            {
                SDK.MSLX.Logger.Error($"[MSLX Migration] 删除备份文件失败: {ex.Message}");
            }
        }
        return false;
    }

    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static void AddDirectoryToArchive(ZipArchive archive, string sourceDir, string entryPrefix)
    {
        if (!Directory.Exists(sourceDir)) return;

        foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
        {
            string relPath = Path.GetRelativePath(sourceDir, file);
            string entryName = $"{entryPrefix}/{relPath.Replace('\\', '/')}";
            archive.CreateEntryFromFile(file, entryName);
        }
    }

    private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
    {
        var dir = new DirectoryInfo(sourceDirName);
        if (!dir.Exists) return;

        DirectoryInfo[] dirs = dir.GetDirectories();
        Directory.CreateDirectory(destDirName);

        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            string temppath = Path.Combine(destDirName, file.Name);
            file.CopyTo(temppath, true);
        }

        if (copySubDirs)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath, copySubDirs);
            }
        }
    }

    /// <summary>
    /// 针对 Forge / NeoForge 服务端启动参数中引用的 win_args.txt 与 unix_args.txt，
    /// 在 Windows 与 Unix (Linux / macOS) 之间迁移时进行自适应动态重写。
    /// 无论在何种操作系统，均严格保持 @ 前缀。
    /// </summary>
    private static void AdaptServerArgsForCurrentPlatform(McServerInfo.ServerInfo server)
    {
        try
        {
            bool isCurrentWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string targetArgFileName = isCurrentWindows ? "win_args.txt" : "unix_args.txt";
            string oppositeArgFileName = isCurrentWindows ? "unix_args.txt" : "win_args.txt";

            // 检查并重写 Core 启动参数引用
            if (!string.IsNullOrEmpty(server.Core))
            {
                string core = server.Core;

                if (core.Contains(oppositeArgFileName, StringComparison.OrdinalIgnoreCase))
                {
                    core = core.Replace(oppositeArgFileName, targetArgFileName, StringComparison.OrdinalIgnoreCase);
                }

                bool hasAtPrefix = core.StartsWith("@");
                string pathPart = hasAtPrefix ? core.Substring(1) : core;

                if (isCurrentWindows)
                {
                    pathPart = pathPart.Replace('/', '\\');
                }
                else
                {
                    pathPart = pathPart.Replace('\\', '/');
                }

                server.Core = (hasAtPrefix ? "@" : "") + pathPart;
            }

            // 如果用户在 Args 字段中也引用了 args.txt，同步修正
            if (!string.IsNullOrEmpty(server.Args) && server.Args.Contains(oppositeArgFileName, StringComparison.OrdinalIgnoreCase))
            {
                server.Args = server.Args.Replace(oppositeArgFileName, targetArgFileName, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Warn($"[MSLX Migration] 实例 [{server.Name}] 跨平台参数转换警告: {ex.Message}");
        }
    }
}

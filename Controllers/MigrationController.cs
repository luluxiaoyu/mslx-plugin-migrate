using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MSLX.Plugin.Migrate.Models;
using MSLX.Plugin.Migrate.Services;
using MSLX.SDK.Models;

namespace MSLX.Plugin.Migrate.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/plugin/mslx-plugin-migrate/migration")]
public class MigrationController : ControllerBase
{
    private readonly MigrationService _migrationService;

    public MigrationController(MigrationService migrationService)
    {
        _migrationService = migrationService;
    }

    [HttpGet("items")]
    public IActionResult GetExportableItems()
    {
        var items = _migrationService.GetExportableItems();
        return Ok(new ApiResponse<ExportableItemsDto>
        {
            Code = 200,
            Message = "获取成功",
            Data = items
        });
    }

    [HttpGet("backups")]
    public IActionResult GetBackups()
    {
        var backups = _migrationService.GetBackupFiles();
        return Ok(new ApiResponse<List<BackupItemDto>>
        {
            Code = 200,
            Message = "获取成功",
            Data = backups
        });
    }

    [HttpDelete("backups/{fileName}")]
    public IActionResult DeleteBackup(string fileName)
    {
        bool success = _migrationService.DeleteBackupFile(fileName);
        if (!success)
        {
            return BadRequest(new ApiResponse<object>
            {
                Code = 400,
                Message = "文件不存在或删除失败"
            });
        }

        return Ok(new ApiResponse<object>
        {
            Code = 200,
            Message = "备份文件已成功删除"
        });
    }

    [HttpPost("export")]
    public IActionResult Export([FromBody] ExportOptions options)
    {
        string userId = GetCurrentUserId();
        string zipFileName = _migrationService.StartExportTask(options, userId);

        return Ok(new ApiResponse<object>
        {
            Code = 200,
            Message = "整机迁移包导出任务已在后台启动，可在任务中心查看实时进度。",
            Data = new
            {
                fileName = zipFileName
            }
        });
    }

    [HttpPost("import")]
    [RequestSizeLimit(10737418240)] // 10 GB
    public async Task<IActionResult> Import([FromForm] IFormFile file, [FromForm] string? optionsJson)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                Code = 400,
                Message = "请上传有效的迁移 Zip 文件。"
            });
        }

        string tempPath = Path.GetTempFileName();
        using (var stream = new FileStream(tempPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var options = string.IsNullOrWhiteSpace(optionsJson)
            ? new ImportOptions()
            : Newtonsoft.Json.JsonConvert.DeserializeObject<ImportOptions>(optionsJson) ?? new ImportOptions();

        string userId = GetCurrentUserId();
        _migrationService.StartImportTask(tempPath, options, userId);

        return Ok(new ApiResponse<object>
        {
            Code = 200,
            Message = "整机迁移包导入任务已提交，可在后台任务中心查看进度。"
        });
    }

    [HttpGet("import-files")]
    public IActionResult GetImportFolderFiles()
    {
        var files = _migrationService.GetImportFolderFiles();
        return Ok(new ApiResponse<List<BackupItemDto>>
        {
            Code = 200,
            Message = "获取成功",
            Data = files
        });
    }

    [HttpPost("import-local-file")]
    public IActionResult ImportLocalFile([FromBody] ImportFromLocalFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest(new ApiResponse<object>
            {
                Code = 400,
                Message = "请指定要导入的 Zip 文件名"
            });
        }

        string userId = GetCurrentUserId();
        bool started = _migrationService.StartImportFromLocalFile(request.FileName, request.Options ?? new ImportOptions(), userId);
        if (!started)
        {
            return NotFound(new ApiResponse<object>
            {
                Code = 404,
                Message = "未在插件 Imports 目录下找到该文件"
            });
        }

        return Ok(new ApiResponse<object>
        {
            Code = 200,
            Message = "整机迁移包导入任务已在后台启动！可在任务中心查看实时进度。"
        });
    }

    [HttpPost("import-by-upload-id")]
    public IActionResult ImportByUploadId([FromBody] ImportByUploadIdRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UploadId))
        {
            return BadRequest(new ApiResponse<object>
            {
                Code = 400,
                Message = "无效的 uploadId"
            });
        }

        string userId = GetCurrentUserId();
        bool started = _migrationService.StartImportFromUploadId(request.UploadId, request.Options ?? new ImportOptions(), userId);
        if (!started)
        {
            return NotFound(new ApiResponse<object>
            {
                Code = 404,
                Message = "未能定位已合并的上传文件或 ID 格式非法，请尝试重新上传。"
            });
        }

        return Ok(new ApiResponse<object>
        {
            Code = 200,
            Message = "整机迁移包导入任务已在后台启动！可在任务中心查看实时进度。"
        });
    }

    [HttpGet("download/{fileName}")]
    public IActionResult Download(string fileName, [FromQuery(Name = "x-user-token")] string? xUserToken = null, [FromQuery] string? token = null)
    {
        string? filePath = _migrationService.GetExportFilePath(fileName);
        if (filePath == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Code = 404,
                Message = "文件不存在、正在打包或已被删除。"
            });
        }

        return PhysicalFile(filePath, "application/zip", fileName);
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst("UserId")?.Value 
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? "system-admin";
    }
}

using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MSLX.Plugin.Migrate.Services;
using MSLX.SDK;
using MSLX.SDK.Interfaces;

[assembly: ApplicationPart("MSLX.Plugin.Migrate")]

namespace MSLX.Plugin.Migrate;

public class MSLXPluginEntry : IPlugin
{
    public static MSLXPluginEntry Instance { get; private set; } = null!;
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    public string Id => "mslx-plugin-migrate";
    public string Name => "整机文件迁移";
    public string Description => "支持服务端实例、映射隧道、用户数据及系统设置的一键导出与导入迁移。";
    public string Version => "1.0.0.6";
    public string Icon => "icon.png";
    public string MinSDKVersion => "1.5.2";
    public string Developer => "xiaoyu";
    public string AuthorUrl => "https://github.com/luluxiaoyu/mslx-plugin-migrate";
    public string PluginUrl => "https://mslx-plugins.mslmc.net/plugins/mslx-plugin-migrate";

    public void OnRegisterServices(IServiceCollection services)
    {
        services.AddSingleton<MigrationService>();
    }

    public void OnPluginInitialize(IServiceProvider serviceProvider)
    {
        Instance = this;
        ServiceProvider = serviceProvider;
    }

    public void OnLoad()
    {
        SDK.MSLX.Logger.Info("MSLX 整机迁移文件插件 (mslx-plugin-migrate) 载入成功！");
    }

    public void OnUnload()
    {
        SDK.MSLX.Logger.Info("MSLX 整机迁移文件插件 (mslx-plugin-migrate) 已卸载。");
    }

    public void OnRegisterEndpoints(IEndpointRouteBuilder endpoints)
    {
        // 端点通过 ASP.NET Core MVC Controller 自动注入
    }
}
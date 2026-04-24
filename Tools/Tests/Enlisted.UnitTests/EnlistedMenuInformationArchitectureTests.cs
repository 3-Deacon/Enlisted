using System;
using System.IO;
using Xunit;

namespace Enlisted.UnitTests;

public sealed class EnlistedMenuInformationArchitectureTests
{
    [Fact]
    public void EnlistedMenuRegistersSeparateStatusCampStanceAndReportsSurfaces()
    {
        var menu = ReadRepoFile("src", "Features", "Interface", "Behaviors", "EnlistedMenuBehavior.cs");

        Assert.Contains("private const string ServiceStanceMenuId", menu, StringComparison.Ordinal);
        Assert.Contains("private const string ReportsMenuId", menu, StringComparison.Ordinal);
        Assert.Contains("RegisterServiceStanceMenu(starter);", menu, StringComparison.Ordinal);
        Assert.Contains("RegisterReportsMenu(starter);", menu, StringComparison.Ordinal);
        Assert.Contains("\"enlisted_service_stance\"", menu, StringComparison.Ordinal);
        Assert.Contains("\"enlisted_reports\"", menu, StringComparison.Ordinal);
    }

    [Fact]
    public void CampHubDoesNotOwnReportsOrPlayerHistory()
    {
        var menu = ReadRepoFile("src", "Features", "Interface", "Behaviors", "EnlistedMenuBehavior.cs");
        var campHub = ExtractMethod(menu, "BuildCampHubText");

        Assert.DoesNotContain("BuildPeriodRecapSection", campHub, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildUpcomingSection", campHub, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildPlayerPersonalStatus", campHub, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildRecentActivitiesNarrative", campHub, StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceStanceManagerPersistsCurrentStance()
    {
        var manager = ReadRepoFile("src", "Features", "Agency", "ServiceStanceManager.cs");

        Assert.Contains("public string CurrentStanceId", manager, StringComparison.Ordinal);
        Assert.Contains("routine_service", manager, StringComparison.Ordinal);
        Assert.Contains("SyncData", manager, StringComparison.Ordinal);
        Assert.Contains("en_service_stance_current", manager, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsSurfaceIsSingleOrganizedPageWithOnlyBackOption()
    {
        var menu = ReadRepoFile("src", "Features", "Interface", "Behaviors", "EnlistedMenuBehavior.cs");
        var addMenus = ExtractMethod(menu, "AddEnlistedMenus");
        var reportsMenu = ExtractMethod(menu, "RegisterReportsMenu");
        var reportsText = ExtractMethod(menu, "private static string BuildReportsText");

        Assert.DoesNotContain("RegisterReportDetailMenu(starter);", addMenus, StringComparison.Ordinal);
        Assert.DoesNotContain("reports_since_muster", reportsMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("reports_personal_dispatches", reportsMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("reports_company_log", reportsMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("reports_kingdom_dispatches", reportsMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenReportDetail", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportDetailMenuId", menu, StringComparison.Ordinal);
        Assert.Contains("BuildPeriodRecapSection", reportsText, StringComparison.Ordinal);
        Assert.Contains("BuildPersonalDispatchReport", reportsText, StringComparison.Ordinal);
        Assert.Contains("BuildCompanyLogReport", reportsText, StringComparison.Ordinal);
        Assert.Contains("BuildKingdomDispatchReport", reportsText, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, Path.Combine(relativeParts));
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(relativeParts));
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var index = source.IndexOf(methodName, StringComparison.Ordinal);
        if (index < 0)
        {
            throw new InvalidOperationException($"Method not found: {methodName}");
        }

        var openBrace = source.IndexOf('{', index);
        if (openBrace < 0)
        {
            throw new InvalidOperationException($"Method body not found: {methodName}");
        }

        var depth = 0;
        for (var i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(openBrace, i - openBrace + 1);
                }
            }
        }

        throw new InvalidOperationException($"Method body did not close: {methodName}");
    }
}

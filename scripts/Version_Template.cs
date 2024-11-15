using System.Diagnostics.CodeAnalysis;

namespace HockeyPickup.Api;

[ExcludeFromCodeCoverage]
public static class VersionInfo
{
    public static string BuildInfo = @"
        {
          ""Branch"": ""{{branch}}"",
          ""BuildTime"": ""{{buildtime}}"",
          ""LastTag"": ""{{lasttag}}"",
          ""Commit"": ""{{commit}}""
        }";
}

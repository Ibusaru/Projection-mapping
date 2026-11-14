using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class OceanWebUrlBuildProcessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows &&
            report.summary.platform != BuildTarget.StandaloneWindows64) return;
        string url = OceanWebConfig.FindWebUrl();
        string path = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "ocean-web-url.txt");
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            Debug.LogWarning("Ocean: Set OCEAN_WEB_URL or VITE_PUBLIC_APP_URL in web/.env.local; then put that URL in ocean-web-url.txt next to the player.");
            return;
        }
        File.WriteAllText(path, url.Trim());
    }
}

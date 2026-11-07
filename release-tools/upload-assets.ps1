param(
    [Parameter(Mandatory = $true)]
    [long]$ReleaseId,
    [Parameter(Mandatory = $true)]
    [string[]]$Paths,
    [switch]$Clobber
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$credentialSource = @'
using System;
using System.Runtime.InteropServices;

public static class GitHubReleaseCredential
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll")]
    public static extern void CredFree(IntPtr buffer);
}
'@
Add-Type -TypeDefinition $credentialSource -ErrorAction SilentlyContinue

$pointer = [IntPtr]::Zero
if (-not [GitHubReleaseCredential]::CredRead(
    "GitHub - https://api.github.com/rAmka220", 1, 0, [ref]$pointer)) {
    throw "GitHub credential is unavailable in Windows Credential Manager"
}

try {
    $credential = [Runtime.InteropServices.Marshal]::PtrToStructure(
        $pointer, [type][GitHubReleaseCredential+CREDENTIAL])
    $secret = New-Object byte[] $credential.CredentialBlobSize
    [Runtime.InteropServices.Marshal]::Copy(
        $credential.CredentialBlob, $secret, 0, $secret.Length)
    $unicode = $true
    for ($index = 1; $index -lt $secret.Length; $index += 2) {
        if ($secret[$index] -ne 0) { $unicode = $false; break }
    }
    if ($unicode) {
        $token = [Text.Encoding]::Unicode.GetString($secret).TrimEnd([char]0)
    } else {
        $token = [Text.Encoding]::UTF8.GetString($secret).TrimEnd([char]0)
    }

    $handler = New-Object Net.Http.HttpClientHandler
    $client = New-Object Net.Http.HttpClient($handler)
    $client.Timeout = [Threading.Timeout]::InfiniteTimeSpan
    $client.DefaultRequestHeaders.Authorization =
        New-Object Net.Http.Headers.AuthenticationHeaderValue("Bearer", $token)
    $client.DefaultRequestHeaders.UserAgent.ParseAdd("Honor9-Release-Uploader/1.0")
    $client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json")

    $assetsUrl = "https://api.github.com/repos/rAmka220/Honor9-HarmonyOS2-Installer/releases/$ReleaseId/assets?per_page=100"
    $parsedAssets = ConvertFrom-Json $client.GetStringAsync($assetsUrl).Result
    $existing = @()
    foreach ($parsedAsset in $parsedAssets) { $existing += $parsedAsset }
    foreach ($path in $Paths) {
        $file = Get-Item -LiteralPath $path
        $found = $existing | Where-Object { $_.name -eq $file.Name } | Select-Object -First 1
        if ($found) {
            $localDigest = "sha256:$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())"
            if ([long]$found.size -eq $file.Length -and
                $found.state -eq "uploaded" -and
                $found.digest -eq $localDigest) {
                Write-Host "SKIP $($file.Name) already uploaded"
                continue
            }
            if (-not $Clobber) {
                throw "Asset exists with a different size: $($file.Name)"
            }
            $deleteUrl = "https://api.github.com/repos/rAmka220/Honor9-HarmonyOS2-Installer/releases/assets/$($found.id)"
            $deleteResponse = $client.DeleteAsync($deleteUrl).Result
            if (-not $deleteResponse.IsSuccessStatusCode) {
                throw "Unable to replace asset $($file.Name): HTTP $([int]$deleteResponse.StatusCode)"
            }
            $existing = @($existing | Where-Object { $_.id -ne $found.id })
            Write-Host "DELETE old $($file.Name) id=$($found.id)" -ForegroundColor Yellow
        }

        Write-Host "UPLOAD $($file.Name) ($($file.Length) bytes)" -ForegroundColor Cyan
        $stream = [IO.File]::OpenRead($file.FullName)
        $content = $null
        try {
            $content = New-Object Net.Http.StreamContent($stream, 1048576)
            $content.Headers.ContentType =
                New-Object Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream")
            $escapedName = [Uri]::EscapeDataString($file.Name)
            $uploadUrl = "https://uploads.github.com/repos/rAmka220/Honor9-HarmonyOS2-Installer/releases/$ReleaseId/assets?name=$escapedName"
            $response = $client.PostAsync($uploadUrl, $content).Result
            $responseText = $response.Content.ReadAsStringAsync().Result
            if (-not $response.IsSuccessStatusCode) {
                throw "Upload failed HTTP $([int]$response.StatusCode): $responseText"
            }
            $asset = ConvertFrom-Json $responseText
            Write-Host "DONE $($asset.name) id=$($asset.id)" -ForegroundColor Green
            $existing += $asset
        } finally {
            if ($content) { $content.Dispose() }
            $stream.Dispose()
        }
    }
} finally {
    if ($client) { $client.Dispose() }
    if ($handler) { $handler.Dispose() }
    if ($secret) { [Array]::Clear($secret, 0, $secret.Length) }
    $token = $null
    if ($pointer -ne [IntPtr]::Zero) { [GitHubReleaseCredential]::CredFree($pointer) }
}

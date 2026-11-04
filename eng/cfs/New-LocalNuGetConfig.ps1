[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $DestinationPath,

    [Parameter(Mandatory)]
    [string] $LocalSourcePath,

    [Parameter(Mandatory)]
    [string[]] $PackagePatterns
)

$repositoryConfigPath = (Resolve-Path (Join-Path $PSScriptRoot "..\..\nuget.config")).Path
$destinationFullPath = [System.IO.Path]::GetFullPath($DestinationPath)
Copy-Item -LiteralPath $repositoryConfigPath -Destination $destinationFullPath -Force

[xml] $config = Get-Content -LiteralPath $destinationFullPath -Raw

$source = $config.CreateElement("add")
$source.SetAttribute("key", "local-packages")
$source.SetAttribute("value", $LocalSourcePath)
[void] $config.configuration.packageSources.AppendChild($source)

$mapping = $config.CreateElement("packageSource")
$mapping.SetAttribute("key", "local-packages")
foreach ($pattern in $PackagePatterns) {
    $package = $config.CreateElement("package")
    $package.SetAttribute("pattern", $pattern)
    [void] $mapping.AppendChild($package)
}

[void] $config.configuration.packageSourceMapping.AppendChild($mapping)

$settings = [System.Xml.XmlWriterSettings]::new()
$settings.Encoding = [System.Text.UTF8Encoding]::new($false)
$settings.Indent = $true

$writer = [System.Xml.XmlWriter]::Create($destinationFullPath, $settings)
try {
    $config.Save($writer)
}
finally {
    $writer.Dispose()
}

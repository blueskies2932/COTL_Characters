Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$packageRoot = Split-Path -Parent $PSScriptRoot
$configRoot = Join-Path $packageRoot "BepInEx\config\COTL_AL_NPCs"
$configPath = Join-Path $configRoot "AiProvider.json"
$keyPath = Join-Path $configRoot "AiProviderKey.txt"
$sidecarExe = Join-Path $packageRoot "BepInEx\plugins\COTL_AL_NPCs\sidecar\CotlAiNpcSidecar.exe"
$testRoot = Join-Path $configRoot "Saves\ProviderTest\Sidecar"

function New-DefaultConfig {
    [ordered]@{
        providerType = "openai"
        apiKeyEnvVar = "OPENAI_API_KEY"
        apiKeyFile = ""
        requiresApiKey = $true
        setupComplete = $false
        baseUrl = ""
        endpointPath = "/responses"
        model = ""
        timeoutSeconds = 120
        temperature = $null
        maxTokens = $null
        headers = @{}
    }
}

function Read-Config {
    if (Test-Path -LiteralPath $configPath) {
        try {
            return Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
        } catch {
            [System.Windows.Forms.MessageBox]::Show("AiProvider.json could not be read. Defaults will be shown.", "AI Provider Settings") | Out-Null
        }
    }
    return [pscustomobject](New-DefaultConfig)
}

function Set-Preset([string]$preset) {
    switch ($preset) {
        "OpenAI" {
            $providerType.Text = "openai"
            $envVar.Text = "OPENAI_API_KEY"
            $requiresKey.Checked = $true
            $baseUrl.Text = ""
            $endpoint.Text = "/responses"
            $model.Text = ""
        }
        "OpenRouter" {
            $providerType.Text = "openai-compatible"
            $envVar.Text = "OPENROUTER_API_KEY"
            $requiresKey.Checked = $true
            $baseUrl.Text = "https://openrouter.ai/api/v1"
            $endpoint.Text = "/chat/completions"
            $model.Text = ""
        }
        "LM Studio" {
            $providerType.Text = "openai-compatible"
            $envVar.Text = ""
            $requiresKey.Checked = $false
            $baseUrl.Text = "http://localhost:1234/v1"
            $endpoint.Text = "/chat/completions"
            $model.Text = ""
        }
        "Ollama Compatible" {
            $providerType.Text = "openai-compatible"
            $envVar.Text = ""
            $requiresKey.Checked = $false
            $baseUrl.Text = "http://localhost:11434/v1"
            $endpoint.Text = "/chat/completions"
            $model.Text = ""
        }
        "Anthropic Claude" {
            $providerType.Text = "anthropic"
            $envVar.Text = "ANTHROPIC_API_KEY"
            $requiresKey.Checked = $true
            $baseUrl.Text = "https://api.anthropic.com/v1"
            $endpoint.Text = "/messages"
            $model.Text = ""
        }
        "Google Gemini" {
            $providerType.Text = "gemini"
            $envVar.Text = "GEMINI_API_KEY"
            $requiresKey.Checked = $true
            $baseUrl.Text = "https://generativelanguage.googleapis.com"
            $endpoint.Text = "/v1beta/models/{model}:generateContent"
            $model.Text = ""
        }
        "Mock" {
            $providerType.Text = "mock"
            $envVar.Text = ""
            $requiresKey.Checked = $false
            $baseUrl.Text = ""
            $endpoint.Text = ""
            $model.Text = "mock"
        }
    }
}

function Save-Config {
    New-Item -ItemType Directory -Force -Path $configRoot | Out-Null

    $apiKeyFile = ""
    if ($saveEnvVar.Checked -and -not [string]::IsNullOrWhiteSpace($envVar.Text) -and -not [string]::IsNullOrWhiteSpace($apiKey.Text)) {
        [Environment]::SetEnvironmentVariable($envVar.Text.Trim(), $apiKey.Text.Trim(), "User")
    }

    if ($saveKeyFile.Checked) {
        $apiKeyFile = $keyPath
        Set-Content -LiteralPath $keyPath -Value $apiKey.Text.Trim() -NoNewline
    }

    if ([string]::IsNullOrWhiteSpace($model.Text)) {
        $status.Text = "Enter a model name available to your provider/API key before saving."
        return
    }

    if ($requiresKey.Checked -and [string]::IsNullOrWhiteSpace($apiKey.Text) -and [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($envVar.Text.Trim()))) {
        $status.Text = "Paste an API key, or set the provider environment variable before saving."
        return
    }

    $config = [ordered]@{
        providerType = $providerType.Text.Trim()
        apiKeyEnvVar = $envVar.Text.Trim()
        apiKeyFile = $apiKeyFile
        requiresApiKey = [bool]$requiresKey.Checked
        setupComplete = $true
        baseUrl = $baseUrl.Text.Trim()
        endpointPath = $endpoint.Text.Trim()
        model = $model.Text.Trim()
        timeoutSeconds = [int]$timeout.Value
        temperature = $null
        maxTokens = $null
        headers = @{}
    }

    $config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $configPath -Encoding UTF8
    $status.Text = "Saved AiProvider.json to $configPath"
}

function Test-Provider {
    Save-Config
    if (-not (Test-Path -LiteralPath $sidecarExe)) {
        $status.Text = "Sidecar executable was not found: $sidecarExe"
        return
    }

    New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
    $status.Text = "Testing provider..."
    $form.Refresh()

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $sidecarExe
    $psi.Arguments = "--root `"$testRoot`" --provider-config `"$configPath`" --test-provider"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    $status.Text = (($stdout + "`r`n" + $stderr).Trim())
    if ([string]::IsNullOrWhiteSpace($status.Text)) {
        $status.Text = "Provider test finished with exit code $($process.ExitCode)."
    }
}

$config = Read-Config

$form = New-Object System.Windows.Forms.Form
$form.Text = "COTL AI Provider Settings"
$form.StartPosition = "CenterScreen"
$form.Size = New-Object System.Drawing.Size(760, 740)
$form.MinimumSize = New-Object System.Drawing.Size(720, 700)

$font = New-Object System.Drawing.Font("Segoe UI", 10)
$form.Font = $font

$y = 18
function Add-Label($text, $x, $y) {
    $label = New-Object System.Windows.Forms.Label
    $label.Text = $text
    $label.Location = New-Object System.Drawing.Point($x, $y)
    $label.Size = New-Object System.Drawing.Size(180, 24)
    $form.Controls.Add($label)
    return $label
}

function Add-TextBox($x, $y, $w) {
    $box = New-Object System.Windows.Forms.TextBox
    $box.Location = New-Object System.Drawing.Point($x, $y)
    $box.Size = New-Object System.Drawing.Size($w, 28)
    $form.Controls.Add($box)
    return $box
}

Add-Label "Preset" 18 $y | Out-Null
$preset = New-Object System.Windows.Forms.ComboBox
$preset.Location = New-Object System.Drawing.Point(210, $y)
$preset.Size = New-Object System.Drawing.Size(500, 28)
$preset.DropDownStyle = "DropDownList"
[void]$preset.Items.AddRange(@("OpenAI", "OpenRouter", "LM Studio", "Ollama Compatible", "Anthropic Claude", "Google Gemini", "Custom", "Mock"))
$preset.SelectedItem = "Custom"
$form.Controls.Add($preset)
$y += 42

Add-Label "Provider type" 18 $y | Out-Null
$providerType = Add-TextBox 210 $y 500
$y += 42

Add-Label "Model" 18 $y | Out-Null
$model = Add-TextBox 210 $y 500
$y += 42

Add-Label "Base URL" 18 $y | Out-Null
$baseUrl = Add-TextBox 210 $y 500
$y += 42

Add-Label "Endpoint path" 18 $y | Out-Null
$endpoint = Add-TextBox 210 $y 500
$y += 42

Add-Label "API key env var" 18 $y | Out-Null
$envVar = Add-TextBox 210 $y 500
$y += 42

$requiresKey = New-Object System.Windows.Forms.CheckBox
$requiresKey.Text = "Provider requires an API key"
$requiresKey.Location = New-Object System.Drawing.Point(210, $y)
$requiresKey.Size = New-Object System.Drawing.Size(360, 28)
$form.Controls.Add($requiresKey)
$y += 38

$saveEnvVar = New-Object System.Windows.Forms.CheckBox
$saveEnvVar.Text = "Save pasted key to Windows user environment variable"
$saveEnvVar.Location = New-Object System.Drawing.Point(210, $y)
$saveEnvVar.Size = New-Object System.Drawing.Size(470, 28)
$saveEnvVar.Checked = $true
$form.Controls.Add($saveEnvVar)
$y += 38

$saveKeyFile = New-Object System.Windows.Forms.CheckBox
$saveKeyFile.Text = "Save pasted key to local AiProviderKey.txt"
$saveKeyFile.Location = New-Object System.Drawing.Point(210, $y)
$saveKeyFile.Size = New-Object System.Drawing.Size(420, 28)
$form.Controls.Add($saveKeyFile)
$y += 38

Add-Label "API key" 18 $y | Out-Null
$apiKey = Add-TextBox 210 $y 500
$apiKey.UseSystemPasswordChar = $true
$y += 42

Add-Label "Timeout seconds" 18 $y | Out-Null
$timeout = New-Object System.Windows.Forms.NumericUpDown
$timeout.Location = New-Object System.Drawing.Point(210, $y)
$timeout.Size = New-Object System.Drawing.Size(120, 28)
$timeout.Minimum = 10
$timeout.Maximum = 600
$timeout.Value = 120
$form.Controls.Add($timeout)
$y += 50

$save = New-Object System.Windows.Forms.Button
$save.Text = "Save"
$save.Location = New-Object System.Drawing.Point(210, $y)
$save.Size = New-Object System.Drawing.Size(130, 36)
$form.Controls.Add($save)

$test = New-Object System.Windows.Forms.Button
$test.Text = "Test Connection"
$test.Location = New-Object System.Drawing.Point(355, $y)
$test.Size = New-Object System.Drawing.Size(170, 36)
$form.Controls.Add($test)

$close = New-Object System.Windows.Forms.Button
$close.Text = "Close"
$close.Location = New-Object System.Drawing.Point(540, $y)
$close.Size = New-Object System.Drawing.Size(130, 36)
$form.Controls.Add($close)
$y += 50

$status = New-Object System.Windows.Forms.TextBox
$status.Location = New-Object System.Drawing.Point(18, $y)
$status.Size = New-Object System.Drawing.Size(700, 160)
$status.Multiline = $true
$status.ScrollBars = "Vertical"
$status.ReadOnly = $true
$form.Controls.Add($status)

$providerType.Text = $config.providerType
$model.Text = $config.model
$baseUrl.Text = $config.baseUrl
$endpoint.Text = $config.endpointPath
$envVar.Text = $config.apiKeyEnvVar
$requiresKey.Checked = [bool]$config.requiresApiKey
if ($config.timeoutSeconds) { $timeout.Value = [decimal]$config.timeoutSeconds }
if ($config.apiKeyFile) { $saveKeyFile.Checked = $true }

$preset.Add_SelectedIndexChanged({ Set-Preset $preset.SelectedItem })
$save.Add_Click({ Save-Config })
$test.Add_Click({ Test-Provider })
$close.Add_Click({ $form.Close() })

[void]$form.ShowDialog()

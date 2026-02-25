<#
.SYNOPSIS
    Azure infrastructure and deployment health checks for Purview Consortium.
.DESCRIPTION
    Validates that all Azure resources are correctly configured and can communicate.
    Checks networking, security controls, service states, and end-to-end connectivity.

    Run this script after any deployment, after maintenance windows, or any time
    you suspect corporate security policies may have altered resource configurations.

    Checks performed:
      - Azure login and subscription context
      - App Service: state, HTTPS, network access, managed identity
      - SQL Server: public network access, Azure services firewall rule
      - SQL Database: online status
      - Key Vault: public network access, App Service RBAC assignment
      - Azure AI Search: public network access, service status
      - Static Web App: reachability
      - End-to-end: health endpoint, API returning data

.PARAMETER ResourceGroup
    The resource group containing all consortium resources.
.PARAMETER AppServiceName
    The App Service name.
.PARAMETER SqlServerName
    The SQL Server name (without .database.windows.net).
.PARAMETER SqlDatabaseName
    The SQL Database name.
.PARAMETER KeyVaultName
    The Key Vault name.
.PARAMETER SearchServiceName
    The Azure AI Search service name.
.PARAMETER StaticWebAppName
    The Static Web App name.
.PARAMETER ApiBaseUrl
    The base URL of the deployed API.
.PARAMETER SwaHostname
    The Static Web App hostname (for reachability check).
.EXAMPLE
    .\tests\Test-DeploymentHealth.ps1
.EXAMPLE
    .\tests\Test-DeploymentHealth.ps1 -ResourceGroup "rg-purview-consortium-dev"
#>
param(
    [string]$ResourceGroup     = "rg-purview-consortium-dev",
    [string]$AppServiceName    = "app-purview-consortium-dev-xsspy6",
    [string]$SqlServerName     = "sql-purview-consortium-dev-xsspy6",
    [string]$SqlDatabaseName   = "PurviewConsortium",
    [string]$KeyVaultName      = "kvpurviewconsortiumdevxs",
    [string]$SearchServiceName = "srch-purview-consortium-dev-xsspy6",
    [string]$StaticWebAppName  = "stapp-purview-consortium-dev-xsspy6",
    [string]$ApiBaseUrl        = "https://app-purview-consortium-dev-xsspy6.azurewebsites.net",
    [string]$SwaHostname       = "delightful-bush-084400c0f.4.azurestaticapps.net"
)

$ErrorActionPreference = "Continue"
$passed  = 0
$failed  = 0
$warned  = 0
$results = @()

# ── Helper functions ──────────────────────────────────────────────────────────

function Write-Check {
    param(
        [string]$Name,
        [string]$Status,   # PASS | FAIL | WARN
        [string]$Detail = ""
    )

    switch ($Status) {
        "PASS" {
            $script:passed++
            Write-Host "  ✅ PASS  $Name$(if ($Detail) { " — $Detail" })" -ForegroundColor Green
        }
        "FAIL" {
            $script:failed++
            Write-Host "  ❌ FAIL  $Name$(if ($Detail) { " — $Detail" })" -ForegroundColor Red
        }
        "WARN" {
            $script:warned++
            Write-Host "  ⚠️  WARN  $Name$(if ($Detail) { " — $Detail" })" -ForegroundColor Yellow
        }
    }

    $script:results += [PSCustomObject]@{
        Check  = $Name
        Status = $Status
        Detail = $Detail
    }
}

function Invoke-AzQuery {
    param([string]$Command)
    $output = Invoke-Expression $Command 2>&1
    if ($LASTEXITCODE -ne 0) { return $null }
    return $output
}

# ── Header ────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host " Purview Consortium — Deployment Health Check" -ForegroundColor Cyan
Write-Host " Resource Group: $ResourceGroup" -ForegroundColor Cyan
Write-Host " Time:           $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# ════════════════════════════════════════════════════════════════════
# SECTION 1: Azure CLI Context
# ════════════════════════════════════════════════════════════════════
Write-Host "── Section 1: Azure CLI Context ──" -ForegroundColor Magenta

$account = az account show 2>&1 | ConvertFrom-Json
if ($account.id) {
    Write-Check "Azure CLI logged in" "PASS" "subscription=$($account.name) ($($account.id))"

    if ($account.id -eq "6d36db89-c579-43a9-9dfc-066026a7dfc0") {
        Write-Check "Correct subscription selected" "PASS" "fauxuni-demo"
    } else {
        Write-Check "Correct subscription selected" "WARN" "Expected fauxuni-demo, got '$($account.name)' ($($account.id))"
    }
} else {
    Write-Check "Azure CLI logged in" "FAIL" "Run 'az login' first"
    Write-Host "`n❌ Cannot continue without Azure CLI login." -ForegroundColor Red
    exit 1
}

$rgExists = az group show --name $ResourceGroup --query "name" -o tsv 2>&1
if ($rgExists -eq $ResourceGroup) {
    Write-Check "Resource group exists" "PASS" $ResourceGroup
} else {
    Write-Check "Resource group exists" "FAIL" "$ResourceGroup not found"
}

Write-Host ""

# ════════════════════════════════════════════════════════════════════
# SECTION 2: App Service
# ════════════════════════════════════════════════════════════════════
Write-Host "── Section 2: App Service ──" -ForegroundColor Magenta

$app = az webapp show --resource-group $ResourceGroup --name $AppServiceName 2>&1 | ConvertFrom-Json

if ($app.state) {
    # State
    if ($app.state -eq "Running") {
        Write-Check "App Service is Running" "PASS" $AppServiceName
    } else {
        Write-Check "App Service is Running" "FAIL" "state=$($app.state) — expected Running"
    }

    # HTTPS only
    if ($app.httpsOnly -eq $true) {
        Write-Check "App Service HTTPS-only enforced" "PASS"
    } else {
        Write-Check "App Service HTTPS-only enforced" "WARN" "httpsOnly=false — HTTP requests are allowed"
    }

    # Managed identity
    if ($app.identity.type -like "*SystemAssigned*") {
        Write-Check "App Service managed identity enabled" "PASS" "principalId=$($app.identity.principalId)"
        $script:appPrincipalId = $app.identity.principalId
    } else {
        Write-Check "App Service managed identity enabled" "FAIL" "No system-assigned identity — Key Vault access will fail"
        $script:appPrincipalId = $null
    }

    # Public network access
    $pna = if ($null -eq $app.publicNetworkAccess) { "Enabled (default)" } else { $app.publicNetworkAccess }
    if ($app.publicNetworkAccess -ne "Disabled") {
        Write-Check "App Service public network access" "PASS" $pna
    } else {
        Write-Check "App Service public network access" "FAIL" "publicNetworkAccess=Disabled — API unreachable from internet"
    }

    # Outbound IPs
    $ipCount = ($app.outboundIpAddresses -split ",").Count
    Write-Check "App Service has outbound IPs" "PASS" "$ipCount IPs: $($app.outboundIpAddresses)"
} else {
    Write-Check "App Service found" "FAIL" "$AppServiceName not found in $ResourceGroup"
}

Write-Host ""

# ════════════════════════════════════════════════════════════════════
# SECTION 3: SQL Server & Database
# ════════════════════════════════════════════════════════════════════
Write-Host "── Section 3: SQL Server & Database ──" -ForegroundColor Magenta

$sql = az sql server show --resource-group $ResourceGroup --name $SqlServerName 2>&1 | ConvertFrom-Json

if ($sql.fullyQualifiedDomainName) {
    # Public network access
    if ($sql.publicNetworkAccess -eq "Enabled") {
        Write-Check "SQL Server public network access" "PASS" "Enabled"
    } else {
        Write-Check "SQL Server public network access" "FAIL" "publicNetworkAccess=$($sql.publicNetworkAccess) — App Service cannot connect"
    }

    # TLS version
    $tls = if ($null -eq $sql.minimalTlsVersion) { "default" } else { $sql.minimalTlsVersion }
    Write-Check "SQL Server TLS version" "PASS" "minTls=$tls"

    # Firewall — AllowAzureServices (0.0.0.0 → 0.0.0.0)
    $fwRules = az sql server firewall-rule list --resource-group $ResourceGroup --server $SqlServerName 2>&1 | ConvertFrom-Json
    $azureRule = $fwRules | Where-Object { $_.startIpAddress -eq "0.0.0.0" -and $_.endIpAddress -eq "0.0.0.0" }
    if ($azureRule) {
        Write-Check "SQL firewall: Allow Azure Services" "PASS" "rule='$($azureRule.name)'"
    } else {
        Write-Check "SQL firewall: Allow Azure Services" "FAIL" "0.0.0.0/0.0.0.0 rule missing — App Service cannot reach SQL"
    }

    # Count any extra rules
    $extraRules = $fwRules | Where-Object { $_.startIpAddress -ne "0.0.0.0" }
    if ($extraRules.Count -gt 0) {
        $ruleNames = ($extraRules | ForEach-Object { $_.name }) -join ", "
        Write-Check "SQL firewall: extra IP rules present" "WARN" "$($extraRules.Count) rule(s): $ruleNames (OK for dev, review for prod)"
    }
} else {
    Write-Check "SQL Server found" "FAIL" "$SqlServerName not found"
}

# Database status
$db = az sql db show --resource-group $ResourceGroup --server $SqlServerName --name $SqlDatabaseName 2>&1 | ConvertFrom-Json
if ($db.status) {
    if ($db.status -eq "Online") {
        Write-Check "SQL Database is Online" "PASS" "$SqlDatabaseName ($($db.currentSku.name))"
    } elseif ($db.status -eq "Paused") {
        Write-Check "SQL Database is Online" "WARN" "status=Paused — serverless auto-pause active, first request will be slow"
    } else {
        Write-Check "SQL Database is Online" "FAIL" "status=$($db.status)"
    }
} else {
    Write-Check "SQL Database found" "FAIL" "$SqlDatabaseName not found on $SqlServerName"
}

Write-Host ""

# ════════════════════════════════════════════════════════════════════
# SECTION 4: Key Vault
# ════════════════════════════════════════════════════════════════════
Write-Host "── Section 4: Key Vault ──" -ForegroundColor Magenta

$kv = az keyvault show --name $KeyVaultName 2>&1 | ConvertFrom-Json

if ($kv.name) {
    # Public network access — the most likely thing corporate security will toggle
    if ($kv.properties.publicNetworkAccess -eq "Enabled") {
        Write-Check "Key Vault public network access" "PASS" "Enabled"
    } elseif ($kv.properties.publicNetworkAccess -eq "Disabled") {
        Write-Check "Key Vault public network access" "FAIL" `
            "DISABLED — App Service cannot read secrets. Fix: az keyvault update --name $KeyVaultName --public-network-access Enabled"
    } else {
        $naStr = if ($null -eq $kv.properties.publicNetworkAccess) { "null (default=Enabled)" } else { $kv.properties.publicNetworkAccess }
        Write-Check "Key Vault public network access" "PASS" $naStr
    }

    # Network ACL default action
    $aclAction = $kv.properties.networkAcls.defaultAction
    if ($null -eq $aclAction -or $aclAction -eq "Allow") {
        Write-Check "Key Vault network ACL default action" "PASS" "$(if ($null -eq $aclAction) { 'Allow (default)' } else { $aclAction })"
    } else {
        Write-Check "Key Vault network ACL default action" "WARN" "defaultAction=$aclAction — only explicitly allowed IPs/VNets can access"
    }

    # App Service identity RBAC on Key Vault
    if ($script:appPrincipalId) {
        $kvScope = "/subscriptions/$($account.id)/resourceGroups/$ResourceGroup/providers/Microsoft.KeyVault/vaults/$KeyVaultName"
        $kvRole = az role assignment list --assignee $script:appPrincipalId --scope $kvScope --query "[].roleDefinitionName" -o tsv 2>&1
        if ($kvRole -match "Key Vault Secrets User|Key Vault Administrator|Key Vault Secrets Officer") {
            Write-Check "App Service identity has Key Vault role" "PASS" "role=$kvRole"
        } elseif ($kvRole) {
            Write-Check "App Service identity has Key Vault role" "WARN" "role=$kvRole (unexpected — expected 'Key Vault Secrets User')"
        } else {
            # Fall back: check broader scope (subscription-level assignment)
            $kvRoleSub = az role assignment list --assignee $script:appPrincipalId --query "[?contains(roleDefinitionName,'Key Vault')].roleDefinitionName" -o tsv 2>&1
            if ($kvRoleSub) {
                Write-Check "App Service identity has Key Vault role" "PASS" "role=$kvRoleSub (subscription scope)"
            } else {
                Write-Check "App Service identity has Key Vault role" "FAIL" "No Key Vault role found — secrets unreadable"
            }
        }
    } else {
        Write-Check "App Service identity has Key Vault role" "WARN" "Skipped — no managed identity principal ID available"
    }

    # Soft-delete / purge protection (informational)
    $softDelete = $kv.properties.enableSoftDelete
    $purgeProtect = $kv.properties.enablePurgeProtection
    Write-Check "Key Vault soft-delete enabled" "$(if ($softDelete) { 'PASS' } else { 'WARN' })" "enableSoftDelete=$(if ($null -eq $softDelete) { 'null' } else { $softDelete })"
} else {
    Write-Check "Key Vault found" "FAIL" "$KeyVaultName not found"
}

Write-Host ""

# ════════════════════════════════════════════════════════════════════
# SECTION 5: Azure AI Search
# ════════════════════════════════════════════════════════════════════
Write-Host "── Section 5: Azure AI Search ──" -ForegroundColor Magenta

$search = az search service show --resource-group $ResourceGroup --name $SearchServiceName 2>&1 | ConvertFrom-Json

if ($search.name) {
    # Public network access
    if ($search.publicNetworkAccess -eq "Enabled") {
        Write-Check "AI Search public network access" "PASS" "Enabled"
    } else {
        Write-Check "AI Search public network access" "FAIL" "publicNetworkAccess=$($search.publicNetworkAccess) — catalog search will fail"
    }

    # Service status
    $searchStatus = $search.status
    if ($searchStatus -eq "running") {
        Write-Check "AI Search service status" "PASS" "running ($($search.sku.name))"
    } else {
        Write-Check "AI Search service status" "FAIL" "status=$searchStatus"
    }

    # Replica/partition count (informational)
    Write-Check "AI Search capacity" "PASS" "replicas=$($search.replicaCount), partitions=$($search.partitionCount)"
} else {
    Write-Check "AI Search found" "FAIL" "$SearchServiceName not found"
}

Write-Host ""

# ════════════════════════════════════════════════════════════════════
# SECTION 6: Static Web App
# ════════════════════════════════════════════════════════════════════
Write-Host "── Section 6: Static Web App ──" -ForegroundColor Magenta

$swa = az staticwebapp show --name $StaticWebAppName --resource-group $ResourceGroup 2>&1 | ConvertFrom-Json

if ($swa.defaultHostname) {
    Write-Check "Static Web App found" "PASS" "hostname=$($swa.defaultHostname)"

    if ($swa.defaultHostname -eq $SwaHostname) {
        Write-Check "Static Web App hostname matches config" "PASS"
    } else {
        Write-Check "Static Web App hostname matches config" "WARN" "Expected $SwaHostname, resource shows $($swa.defaultHostname)"
    }

    # Reachability check via HTTP
    try {
        $swaResp = Invoke-WebRequest -Uri "https://$SwaHostname" -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
        if ($swaResp.StatusCode -eq 200) {
            Write-Check "Static Web App reachable (HTTPS)" "PASS" "HTTP $($swaResp.StatusCode)"
        } else {
            Write-Check "Static Web App reachable (HTTPS)" "WARN" "HTTP $($swaResp.StatusCode)"
        }
    } catch {
        Write-Check "Static Web App reachable (HTTPS)" "FAIL" $_.Exception.Message
    }
} else {
    Write-Check "Static Web App found" "FAIL" "$StaticWebAppName not found"
}

Write-Host ""

# ════════════════════════════════════════════════════════════════════
# SECTION 7: App Service CORS Configuration
# ════════════════════════════════════════════════════════════════════
Write-Host "── Section 7: CORS Configuration ──" -ForegroundColor Magenta

$appSettings = az webapp config appsettings list --resource-group $ResourceGroup --name $AppServiceName 2>&1 | ConvertFrom-Json
$corsOrigin = ($appSettings | Where-Object { $_.name -like "Cors__AllowedOrigins*" } | Select-Object -First 1).value

if ($corsOrigin) {
    Write-Check "CORS origin configured" "PASS" $corsOrigin
    if ($corsOrigin -match $SwaHostname) {
        Write-Check "CORS origin matches SWA hostname" "PASS"
    } else {
        Write-Check "CORS origin matches SWA hostname" "WARN" "Configured '$corsOrigin' but SWA is '$SwaHostname'"
    }
} else {
    Write-Check "CORS origin configured" "FAIL" "No Cors__AllowedOrigins* setting found in App Service"
}

# Verify Key Vault URI setting is present
$kvUri = ($appSettings | Where-Object { $_.name -eq "KeyVault__Uri" }).value
if ($kvUri) {
    Write-Check "Key Vault URI setting present" "PASS" $kvUri
} else {
    Write-Check "Key Vault URI setting present" "FAIL" "KeyVault__Uri app setting missing — secrets won't be loaded"
}

Write-Host ""

# ════════════════════════════════════════════════════════════════════
# SECTION 8: End-to-End Connectivity
# ════════════════════════════════════════════════════════════════════
Write-Host "── Section 8: End-to-End Connectivity ──" -ForegroundColor Magenta

# Health endpoint
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $health = Invoke-WebRequest -Uri "$ApiBaseUrl/healthz" -UseBasicParsing -TimeoutSec 30 -ErrorAction Stop
    $stopwatch.Stop()
    $healthJson = $health.Content | ConvertFrom-Json
    if ($health.StatusCode -eq 200 -and $healthJson.status -eq "healthy") {
        Write-Check "API health endpoint" "PASS" "status=$($healthJson.status) ($($stopwatch.ElapsedMilliseconds)ms)"
    } else {
        Write-Check "API health endpoint" "WARN" "HTTP $($health.StatusCode) — $($health.Content)"
    }
} catch {
    $stopwatch.Stop()
    Write-Check "API health endpoint" "FAIL" $_.Exception.Message
}

# Catalog stats (anonymous — hits DB, proves Key Vault + SQL chain works)
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $stats = Invoke-WebRequest -Uri "$ApiBaseUrl/api/catalog/stats" -UseBasicParsing -TimeoutSec 30 -ErrorAction Stop
    $stopwatch.Stop()
    $statsJson = $stats.Content | ConvertFrom-Json
    if ($stats.StatusCode -eq 200) {
        Write-Check "API → DB chain (catalog/stats)" "PASS" `
            "totalProducts=$($statsJson.totalProducts), totalInstitutions=$($statsJson.totalInstitutions) ($($stopwatch.ElapsedMilliseconds)ms)"
    } else {
        Write-Check "API → DB chain (catalog/stats)" "FAIL" "HTTP $($stats.StatusCode)"
    }
} catch {
    $stopwatch.Stop()
    $errBody = $_.ErrorDetails.Message
    Write-Check "API → DB chain (catalog/stats)" "FAIL" "HTTP 500 — DB/KV unreachable. $errBody"
}

# Auth enforcement — unauthenticated call should 401, not 500
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $authCheck = Invoke-WebRequest -Uri "$ApiBaseUrl/api/catalog/products" -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
    $stopwatch.Stop()
    Write-Check "Auth enforcement (catalog/products no token)" "FAIL" "Expected 401, got HTTP $($authCheck.StatusCode)"
} catch {
    $stopwatch.Stop()
    $code = [int]$_.Exception.Response.StatusCode
    if ($code -eq 401) {
        Write-Check "Auth enforcement (catalog/products no token)" "PASS" "HTTP 401 as expected ($($stopwatch.ElapsedMilliseconds)ms)"
    } elseif ($code -eq 500) {
        Write-Check "Auth enforcement (catalog/products no token)" "FAIL" "HTTP 500 — app likely failing to start (check KV/SQL)"
    } else {
        Write-Check "Auth enforcement (catalog/products no token)" "WARN" "HTTP $code (expected 401)"
    }
}

Write-Host ""

# ════════════════════════════════════════════════════════════════════
# RESULTS SUMMARY
# ════════════════════════════════════════════════════════════════════
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host " RESULTS SUMMARY" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "  ✅ Passed:   $passed"  -ForegroundColor Green
Write-Host "  ❌ Failed:   $failed"  -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
Write-Host "  ⚠️  Warnings: $warned"  -ForegroundColor $(if ($warned -gt 0)  { "Yellow" } else { "Green" })
Write-Host "  📊 Total:    $($passed + $failed + $warned)" -ForegroundColor White
Write-Host ""

$results | Format-Table -Property Check, Status, Detail -AutoSize -Wrap

if ($failed -gt 0) {
    Write-Host ""
    Write-Host "❌ $failed check(s) FAILED — common fixes:" -ForegroundColor Red
    Write-Host "   Key Vault disabled:  az keyvault update --name $KeyVaultName --public-network-access Enabled" -ForegroundColor DarkRed
    Write-Host "   SQL access disabled: az sql server update --resource-group $ResourceGroup --name $SqlServerName --enable-public-network true" -ForegroundColor DarkRed
    Write-Host "   App Service down:    az webapp start --resource-group $ResourceGroup --name $AppServiceName" -ForegroundColor DarkRed
    Write-Host "   After fixing:        az webapp restart --resource-group $ResourceGroup --name $AppServiceName" -ForegroundColor DarkRed
    exit 1
} elseif ($warned -gt 0) {
    Write-Host "⚠️  $warned warning(s) — review above but deployment is likely functional." -ForegroundColor Yellow
    exit 0
} else {
    Write-Host "✅ All deployment health checks passed!" -ForegroundColor Green
    exit 0
}

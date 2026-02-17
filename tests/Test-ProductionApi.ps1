<#
.SYNOPSIS
    Production API validation tests for Purview Consortium.
.DESCRIPTION
    Runs HTTP tests against the deployed API to validate endpoints are reachable
    and returning expected status codes. Tests are split into:
      - Anonymous tests (no auth required)
      - Authenticated tests (require a Bearer token)
.PARAMETER BaseUrl
    The base URL of the deployed API (no trailing slash).
.PARAMETER BearerToken
    Optional Bearer token for authenticated endpoint tests.
    Get one from: az account get-access-token --resource api://6561c659-2dfd-44a5-9931-cd059de86857 --query accessToken -o tsv
.EXAMPLE
    .\tests\Test-ProductionApi.ps1 -BaseUrl "https://app-purview-consortium-dev-xsspy6.azurewebsites.net"
.EXAMPLE
    $token = az account get-access-token --resource api://6561c659-2dfd-44a5-9931-cd059de86857 --query accessToken -o tsv
    .\tests\Test-ProductionApi.ps1 -BaseUrl "https://app-purview-consortium-dev-xsspy6.azurewebsites.net" -BearerToken $token
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [Parameter(Mandatory = $false)]
    [string]$BearerToken
)

$ErrorActionPreference = "Continue"
$passed = 0
$failed = 0
$skipped = 0
$results = @()

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [int[]]$ExpectedStatus,
        [hashtable]$Headers = @{},
        [string]$Body = $null,
        [bool]$RequiresAuth = $false
    )

    if ($RequiresAuth -and -not $BearerToken) {
        $script:skipped++
        $script:results += [PSCustomObject]@{
            Test     = $Name
            Method   = $Method
            Url      = $Url
            Status   = "SKIPPED"
            Code     = "-"
            Time     = "-"
            Detail   = "No Bearer token provided"
        }
        Write-Host "  ⏭  SKIP  $Name (no token)" -ForegroundColor Yellow
        return
    }

    if ($RequiresAuth) {
        $Headers["Authorization"] = "Bearer $BearerToken"
    }

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $params = @{
            Uri                = $Url
            Method             = $Method
            Headers            = $Headers
            ContentType        = "application/json"
            UseBasicParsing    = $true
            ErrorAction        = "Stop"
        }
        if ($Body) {
            $params["Body"] = $Body
        }

        # Disable auto-redirect to see actual status codes
        $response = Invoke-WebRequest @params
        $stopwatch.Stop()
        $statusCode = $response.StatusCode
        $detail = "Content-Length: $($response.Content.Length) bytes"

        # Try to parse JSON for a summary
        try {
            $json = $response.Content | ConvertFrom-Json
            if ($json.status) { $detail = "status=$($json.status)" }
            elseif ($json.totalProducts -ne $null) { $detail = "totalProducts=$($json.totalProducts), totalInstitutions=$($json.totalInstitutions)" }
            elseif ($json.Count -ne $null -and $json -is [System.Array]) { $detail = "items=$($json.Count)" }
            elseif ($json.items) { $detail = "items=$($json.items.Count), totalCount=$($json.totalCount)" }
        } catch {}
    }
    catch {
        $stopwatch.Stop()
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            $detail = $_.Exception.Message
        }
        else {
            $statusCode = 0
            $detail = $_.Exception.Message
        }
    }

    $elapsed = "$($stopwatch.ElapsedMilliseconds)ms"

    if ($statusCode -in $ExpectedStatus) {
        $script:passed++
        $status = "PASS"
        Write-Host "  ✅ PASS  $Name — HTTP $statusCode ($elapsed)" -ForegroundColor Green
    }
    else {
        $script:failed++
        $status = "FAIL"
        Write-Host "  ❌ FAIL  $Name — HTTP $statusCode (expected $($ExpectedStatus -join '/')) ($elapsed)" -ForegroundColor Red
        Write-Host "           $detail" -ForegroundColor DarkRed
    }

    $script:results += [PSCustomObject]@{
        Test     = $Name
        Method   = $Method
        Url      = $Url
        Status   = $status
        Code     = $statusCode
        Time     = $elapsed
        Detail   = $detail
    }
}

# ============================================================
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " Purview Consortium API — Production Tests" -ForegroundColor Cyan
Write-Host " Target: $BaseUrl" -ForegroundColor Cyan
Write-Host " Time:   $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host " Auth:   $(if ($BearerToken) { 'Bearer token provided' } else { 'Anonymous only' })" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================
# SECTION 1: Connectivity & Health
# ============================================================
Write-Host "── Section 1: Connectivity & Health ──" -ForegroundColor Magenta

Test-Endpoint -Name "Health check" `
    -Method "GET" -Url "$BaseUrl/healthz" `
    -ExpectedStatus @(200)

Test-Endpoint -Name "Root URL (expect 404, no handler)" `
    -Method "GET" -Url "$BaseUrl/" `
    -ExpectedStatus @(404)

Test-Endpoint -Name "Swagger JSON (disabled in prod)" `
    -Method "GET" -Url "$BaseUrl/swagger/v1/swagger.json" `
    -ExpectedStatus @(404)

Write-Host ""

# ============================================================
# SECTION 2: Anonymous Endpoints
# ============================================================
Write-Host "── Section 2: Anonymous Endpoints ──" -ForegroundColor Magenta

Test-Endpoint -Name "Catalog stats (AllowAnonymous)" `
    -Method "GET" -Url "$BaseUrl/api/catalog/stats" `
    -ExpectedStatus @(200)

Write-Host ""

# ============================================================
# SECTION 3: Auth Enforcement (should return 401 without token)
# ============================================================
Write-Host "── Section 3: Auth Enforcement (no token → 401) ──" -ForegroundColor Magenta

Test-Endpoint -Name "Catalog products (no auth → 401)" `
    -Method "GET" -Url "$BaseUrl/api/catalog/products" `
    -ExpectedStatus @(401)

Test-Endpoint -Name "Catalog filters (no auth → 401)" `
    -Method "GET" -Url "$BaseUrl/api/catalog/filters" `
    -ExpectedStatus @(401)

Test-Endpoint -Name "Institutions list (no auth → 401)" `
    -Method "GET" -Url "$BaseUrl/api/admin/institutions" `
    -ExpectedStatus @(401)

Test-Endpoint -Name "Access requests (no auth → 401)" `
    -Method "GET" -Url "$BaseUrl/api/requests" `
    -ExpectedStatus @(401)

Test-Endpoint -Name "Sync history (no auth → 401)" `
    -Method "GET" -Url "$BaseUrl/api/admin/sync/history" `
    -ExpectedStatus @(401)

Write-Host ""

# ============================================================
# SECTION 4: Authenticated Endpoints (require Bearer token)
# ============================================================
Write-Host "── Section 4: Authenticated Endpoints ──" -ForegroundColor Magenta

Test-Endpoint -Name "Catalog products (authed)" `
    -Method "GET" -Url "$BaseUrl/api/catalog/products" `
    -ExpectedStatus @(200) -RequiresAuth $true

Test-Endpoint -Name "Catalog filters (authed)" `
    -Method "GET" -Url "$BaseUrl/api/catalog/filters" `
    -ExpectedStatus @(200) -RequiresAuth $true

Test-Endpoint -Name "Catalog products with search query" `
    -Method "GET" -Url "$BaseUrl/api/catalog/products?search=test&page=1&pageSize=5" `
    -ExpectedStatus @(200) -RequiresAuth $true

Test-Endpoint -Name "Catalog product by invalid ID (404)" `
    -Method "GET" -Url "$BaseUrl/api/catalog/products/00000000-0000-0000-0000-000000000000" `
    -ExpectedStatus @(404) -RequiresAuth $true

Test-Endpoint -Name "Institutions list (authed)" `
    -Method "GET" -Url "$BaseUrl/api/admin/institutions" `
    -ExpectedStatus @(200) -RequiresAuth $true

Test-Endpoint -Name "Institutions list (activeOnly)" `
    -Method "GET" -Url "$BaseUrl/api/admin/institutions?activeOnly=true" `
    -ExpectedStatus @(200) -RequiresAuth $true

Test-Endpoint -Name "Institution by invalid ID (404)" `
    -Method "GET" -Url "$BaseUrl/api/admin/institutions/00000000-0000-0000-0000-000000000000" `
    -ExpectedStatus @(404) -RequiresAuth $true

Test-Endpoint -Name "Access requests list (authed)" `
    -Method "GET" -Url "$BaseUrl/api/requests" `
    -ExpectedStatus @(200) -RequiresAuth $true

Test-Endpoint -Name "Sync history (authed)" `
    -Method "GET" -Url "$BaseUrl/api/admin/sync/history" `
    -ExpectedStatus @(200) -RequiresAuth $true

Test-Endpoint -Name "Sync history with count param" `
    -Method "GET" -Url "$BaseUrl/api/admin/sync/history?count=5" `
    -ExpectedStatus @(200) -RequiresAuth $true

Write-Host ""

# ============================================================
# SECTION 5: CORS Validation
# ============================================================
Write-Host "── Section 5: CORS Preflight ──" -ForegroundColor Magenta

$corsHeaders = @{
    "Origin"                         = "https://delightful-bush-084400c0f.4.azurestaticapps.net"
    "Access-Control-Request-Method"  = "GET"
    "Access-Control-Request-Headers" = "Authorization,Content-Type"
}
Test-Endpoint -Name "CORS preflight from SWA origin" `
    -Method "OPTIONS" -Url "$BaseUrl/api/catalog/stats" `
    -ExpectedStatus @(200, 204) -Headers $corsHeaders

$badCorsHeaders = @{
    "Origin"                         = "https://evil-site.example.com"
    "Access-Control-Request-Method"  = "GET"
    "Access-Control-Request-Headers" = "Authorization"
}
Test-Endpoint -Name "CORS preflight from bad origin (rejected)" `
    -Method "OPTIONS" -Url "$BaseUrl/api/catalog/stats" `
    -ExpectedStatus @(200, 204, 400) -Headers $badCorsHeaders

Write-Host ""

# ============================================================
# SECTION 6: Error Handling
# ============================================================
Write-Host "── Section 6: Error Handling ──" -ForegroundColor Magenta

Test-Endpoint -Name "Non-existent route (404)" `
    -Method "GET" -Url "$BaseUrl/api/does-not-exist" `
    -ExpectedStatus @(404)

Test-Endpoint -Name "Invalid HTTP method on catalog (405)" `
    -Method "DELETE" -Url "$BaseUrl/api/catalog/products" `
    -ExpectedStatus @(405, 401)

Write-Host ""

# ============================================================
# RESULTS SUMMARY
# ============================================================
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " RESULTS SUMMARY" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  ✅ Passed:  $passed" -ForegroundColor Green
Write-Host "  ❌ Failed:  $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
Write-Host "  ⏭  Skipped: $skipped" -ForegroundColor Yellow
Write-Host "  📊 Total:   $($passed + $failed + $skipped)" -ForegroundColor White
Write-Host ""

# Print table
$results | Format-Table -Property Test, Method, Status, Code, Time, Detail -AutoSize

if ($failed -gt 0) {
    Write-Host "❌ $failed test(s) FAILED — review the output above." -ForegroundColor Red
    exit 1
}
else {
    Write-Host "✅ All tests passed!" -ForegroundColor Green
    exit 0
}

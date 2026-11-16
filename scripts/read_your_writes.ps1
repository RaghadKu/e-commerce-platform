param(
    [Parameter(Mandatory)] [string] $AdminJwt,
    [Parameter(Mandatory)] [string] $ProductId,
    [Parameter(Mandatory)] [string] $CategoryId,
    [Parameter(Mandatory)] [string] $Sku,
    [Parameter(Mandatory)] [decimal] $Price,
    [Parameter(Mandatory)] [string] $SaPassword,
    [string] $BaseUrl = "http://localhost:8080"
)

function Invoke-Sql($container, $sql) {
    docker exec $container /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P $SaPassword -Q $sql | Out-Null
}

$name = "Replica Demo " + ([guid]::NewGuid().ToString("N").Substring(0, 8))
$headers = @{ Authorization = "Bearer $AdminJwt" }
$body = @{ name = $name; description = "demo"; price = $Price; sku = $Sku; categoryId = $CategoryId } | ConvertTo-Json

try {
    Write-Host "1. Pausing the replica..."
    Invoke-Sql "ecommerce-sqlserver-replica" "ALTER DATABASE MainDb SET HADR SUSPEND"

    Write-Host "2. Updating the product on the primary..."
    $put = Invoke-WebRequest -Method Put -Uri "$BaseUrl/api/v2/products/$ProductId" `
        -Headers $headers -ContentType "application/json" -Body $body
    $token = [string]($put.Headers["X-Consistency-Token"] | Select-Object -First 1)
    Write-Host "   Token returned: $token"

    # Different letter case = different Redis key, so the cache cannot hide the result.
    $withToken = "$BaseUrl/api/v2/products?search=" + [uri]::EscapeDataString($name)
    $noToken = "$BaseUrl/api/v2/products?search=" + [uri]::EscapeDataString($name.ToUpper())

    Write-Host "3. Reading WITH the token (replica is behind, so the primary is used)..."
    $a = Invoke-WebRequest -Uri $withToken -Headers @{ "X-Consistency-Token" = $token }
    Write-Host ("   Sees the new name: " + ($a.Content -like "*$name*"))   # expected: True

    Write-Host "4. Reading WITHOUT the token (replica is used, stale)..."
    $b = Invoke-WebRequest -Uri $noToken
    Write-Host ("   Sees the new name: " + ($b.Content -like "*$name*"))   # expected: False (lag)
}
finally {
    Write-Host "5. Resuming the replica..."
    Invoke-Sql "ecommerce-sqlserver-replica" "ALTER DATABASE MainDb SET HADR RESUME"
}
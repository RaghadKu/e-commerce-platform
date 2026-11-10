param(
	[string] $Url = 'http://localhost:8080/api/orders/checkout',
	[int]	$ExpectedWinners = 3
)

$Tokens = @(
	'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJlY29tbWVyY2UtY2xpZW50cyIsImlzcyI6ImVjb21tZXJjZS1hcGkiLCJleHAiOjE3OTM3ODczMjYsInN1YiI6Ijc5ZDg2NGFiLTBjZmEtNDFmOC05OWNkLTdlZWUwYWNiMzY5ZiIsImVtYWlsIjoidGVzdDFAZXhhbXBsZS5jb20iLCJuYW1lIjoidGVzdCAxIiwianRpIjoiZmZmZmVmZTItZTZjOC00NGUxLWIwYWEtZTVlY2Q3NTM5ODRlIiwicm9sZSI6IkN1c3RvbWVyIiwiaWF0IjoxNzkzNzgzNzI2LCJuYmYiOjE3OTM3ODM3MjZ9.6IXscViR6T5lStnB8lF3pETHiSe5UyceaRKr1kBajww'
	'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJlY29tbWVyY2UtY2xpZW50cyIsImlzcyI6ImVjb21tZXJjZS1hcGkiLCJleHAiOjE3OTM3ODczNjEsInN1YiI6IjczZDRmMzQzLTRhOTItNGYxMC05MzRjLWRhZjI2MjRkOTVjNyIsImVtYWlsIjoidGVzdDJAZXhhbXBsZS5jb20iLCJuYW1lIjoidGVzdCAyIiwianRpIjoiOTM3Mjc2YWYtZDQ2Yi00MWRjLWI5YmMtNzI5M2FhNDM5ZjBlIiwicm9sZSI6IkN1c3RvbWVyIiwiaWF0IjoxNzkzNzgzNzYxLCJuYmYiOjE3OTM3ODM3NjF9.aXpwpCShwyg1LJY6n2DqPKIxk22fPDwSq-331wgBHEM'
	'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJlY29tbWVyY2UtY2xpZW50cyIsImlzcyI6ImVjb21tZXJjZS1hcGkiLCJleHAiOjE3OTM3ODc0NDUsInN1YiI6ImU5MmViZTUyLTdiNTAtNDQzNi1iNWI3LTUyY2MwODY4ZmI4ZiIsImVtYWlsIjoidGVzdDNAZXhhbXBsZS5jb20iLCJuYW1lIjoidGVzdCAzIiwianRpIjoiZWEyMzI2ZjUtMGZiYi00YzUxLTk0NjMtZjRjZDlhMGM5NTY2Iiwicm9sZSI6IkN1c3RvbWVyIiwiaWF0IjoxNzkzNzgzODQ1LCJuYmYiOjE3OTM3ODM4NDV9.eq3-sFKSy88CjrbN5MQKMdY_MwhWkw5yYslBHDLCIGg'
	'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJlY29tbWVyY2UtY2xpZW50cyIsImlzcyI6ImVjb21tZXJjZS1hcGkiLCJleHAiOjE3OTM3ODc0NzMsInN1YiI6IjQxZTc3MWVlLWUzMTItNDA2Zi1hMWY2LWRhZWI2YjY2NWVkMCIsImVtYWlsIjoidGVzdDRAZXhhbXBsZS5jb20iLCJuYW1lIjoidGVzdCA0IiwianRpIjoiMzcyNzNjYmYtNTMyYy00ZjRkLTk4OGEtZGY1YjEzZmM2OTVlIiwicm9sZSI6IkN1c3RvbWVyIiwiaWF0IjoxNzkzNzgzODczLCJuYmYiOjE3OTM3ODM4NzN9.vBuOElqQpgyrnv67cTmh8DQ_ItKw9sbWnNWA7QLm5oE'
	'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJlY29tbWVyY2UtY2xpZW50cyIsImlzcyI6ImVjb21tZXJjZS1hcGkiLCJleHAiOjE3OTM3ODc1MDksInN1YiI6ImM0OThlOWViLWFhMzgtNDVmOS1hZGU0LTcxNTU0NDBjMTlkNSIsImVtYWlsIjoidGVzdDVAZXhhbXBsZS5jb20iLCJuYW1lIjoidGVzdCA1IiwianRpIjoiZDI3MTFjZjAtZDk4OS00Mjc3LWFmMDQtY2Q4NTRjZWI1ZWM1Iiwicm9sZSI6IkN1c3RvbWVyIiwiaWF0IjoxNzkzNzgzOTA5LCJuYmYiOjE3OTM3ODM5MDl9.46-KW4P8aN4EUuCTlHhsfBxAjnzDqPw6jjHA9mRGuvE'
)

if ($Tokens | Where-Object { $_ -like 'PASTE_*' }) { throw 'Paste all 5 tokens first.' }

Add-Type -AssemblyName System.Net.Http
$http = New-Object System.Net.Http.HttpClient
$http.Timeout = [TimeSpan]::FromSeconds(60)

$requests = foreach ($token in $Tokens) {
	$req = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post, $Url)
	$req.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $token)
	$req
}

Write-Host "`nSending $($Tokens.Count) concurrent checkouts to $Url ..." -ForegroundColor Cyan
$clock = [Diagnostics.Stopwatch]::StartNew()
$tasks = @($requests | ForEach-Object { $http.SendAsync($_) })
try { [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]$tasks) } catch { }
$clock.Stop()

$results = for ($i = 0; $i -lt $tasks.Count; $i++) {
	if ($tasks[$i].IsFaulted) {
    	[pscustomobject]@{ User = $i + 1; Status = 'ERROR'; Success = $false
                       	Detail = $tasks[$i].Exception.GetBaseException().Message }
    	continue
	}
	$resp = $tasks[$i].Result
	$text = $resp.Content.ReadAsStringAsync().Result
	$detail = $text
	try {
    	$j = $text | ConvertFrom-Json
    	if ($j.detail) { $detail = $j.detail } elseif ($j.status) { $detail = $j.status }
	} catch { }
	if ($detail.Length -gt 90) { $detail = $detail.Substring(0, 90) + '...' }

	[pscustomobject]@{ User = $i + 1
                   	Status = "$([int]$resp.StatusCode) $($resp.StatusCode)"
                   	Success = $resp.IsSuccessStatusCode
                   	Detail = $detail }
}
$http.Dispose()

$results | Format-Table User, Status, Success, Detail -AutoSize -Wrap
$winners = @($results | Where-Object Success).Count
Write-Host ("Finished in {0} ms. Successful checkouts: {1} (expected {2})" -f $clock.ElapsedMilliseconds, $winners, $ExpectedWinners)

if ($winners -eq $ExpectedWinners) { Write-Host 'PASS: no overselling.' -ForegroundColor Green; exit 0 }
Write-Host 'FAIL: wrong number of successful checkouts.' -ForegroundColor Red; exit 1
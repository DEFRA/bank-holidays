param(
    [string]$BaseUrl = 'http://localhost/defra.bankholidays',
    [string]$AccessToken
)

# The update endpoint requires an Entra ID access token that carries the BankHolidays.Refresh app role.
$headers = @{}

if ($AccessToken) {
    $headers['Authorization'] = "Bearer $AccessToken"
}

Invoke-WebRequest -Uri "$($BaseUrl.TrimEnd('/'))/home/update" -Method 'Post' -Headers $headers -UseBasicParsing

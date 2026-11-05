$loginBody = @{
    email = 'hoanghaihau989@gmail.com'
    password = '@123456xX'
} | ConvertTo-Json

try {
    $loginRes = Invoke-RestMethod -Uri 'http://projectmgmt_develop.dev.com/api/auth/login' -Method Post -Body $loginBody -ContentType 'application/json'
    Write-Host "Login Success! User:" ($loginRes | ConvertTo-Json -Depth 5)
    $token = $loginRes.accessToken
    $headers = @{ Authorization = "Bearer $token" }

    $orgs = Invoke-RestMethod -Uri 'http://projectmgmt_develop.dev.com/api/organizations' -Method Get -Headers $headers
    Write-Host "Orgs:" ($orgs | ConvertTo-Json -Depth 3)

    if ($orgs.Count -gt 0) {
        $orgId = $orgs[0].id
        Write-Host "Testing project creation in org $orgId..."
        $projBody = @{
            orgId = $orgId
            name = "Test Project Auto"
            projectKey = "TPA05"
            description = "Test project creation"
            leadUserId = $loginRes.userId
            memberUserIds = @()
        } | ConvertTo-Json
        
        $projRes = Invoke-RestMethod -Uri 'http://projectmgmt_develop.dev.com/api/projects' -Method Post -Body $projBody -ContentType 'application/json' -Headers $headers
        Write-Host "Project Creation Result:" ($projRes | ConvertTo-Json -Depth 3)
    }

    Write-Host "Calling /api/users/for-you..."
    $forYouRes = Invoke-RestMethod -Uri 'http://projectmgmt_develop.dev.com/api/users/for-you' -Method Get -Headers $headers
    Write-Host "ForYou Succeeded! Count of assigned issues:" $forYouRes.assignedIssues.Count

    Write-Host "Calling /api/users/me/recent..."
    $recentRes = Invoke-RestMethod -Uri 'http://projectmgmt_develop.dev.com/api/users/me/recent' -Method Get -Headers $headers
    Write-Host "Recent Succeeded! Items count:" $recentRes.Count

    Write-Host "Calling /api/users/me/organization-memberships..."
    $membershipsRes = Invoke-RestMethod -Uri 'http://projectmgmt_develop.dev.com/api/users/me/organization-memberships' -Method Get -Headers $headers
    Write-Host "Memberships Succeeded! Orgs count:" $membershipsRes.Count
    Write-Host ($membershipsRes | ConvertTo-Json -Depth 3)

    Write-Host "Calling /api/organizations/roles..."
    $rolesRes = Invoke-RestMethod -Uri 'http://projectmgmt_develop.dev.com/api/organizations/roles' -Method Get -Headers $headers
    Write-Host "Roles Succeeded!" ($rolesRes | ConvertTo-Json -Depth 3)

    if ($orgs.Count -gt 0) {
        $firstOrgId = $orgs[0].id
        Write-Host "Calling /api/organizations/$firstOrgId/members..."
        $membersRes = Invoke-RestMethod -Uri "http://projectmgmt_develop.dev.com/api/organizations/$firstOrgId/members" -Method Get -Headers $headers
        Write-Host "Members Succeeded! Count:" $membersRes.Count
        Write-Host ($membersRes | ConvertTo-Json -Depth 3)

        Write-Host "Calling /api/organizations/$firstOrgId/workflow-templates..."
        $templatesRes = Invoke-RestMethod -Uri "http://projectmgmt_develop.dev.com/api/organizations/$firstOrgId/workflow-templates" -Method Get -Headers $headers
        Write-Host "Templates Succeeded! Count:" $templatesRes.Count
        Write-Host ($templatesRes | ConvertTo-Json -Depth 3)

        Write-Host "Testing Record Visit POST /api/users/me/recent..."
        $visitBody = @{ kind = "organization"; id = $firstOrgId } | ConvertTo-Json
        $null = Invoke-RestMethod -Uri "http://projectmgmt_develop.dev.com/api/users/me/recent" -Method Post -Body $visitBody -ContentType 'application/json' -Headers $headers
        Write-Host "Record visit succeeded!"

        $recentAfter = Invoke-RestMethod -Uri 'http://projectmgmt_develop.dev.com/api/users/me/recent' -Method Get -Headers $headers
        Write-Host "Recent after visit:" ($recentAfter | ConvertTo-Json -Depth 3)
    }

} catch {
    Write-Host "Error:" $_.Exception.Message
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        Write-Host "Error Response:" $reader.ReadToEnd()
    }
}

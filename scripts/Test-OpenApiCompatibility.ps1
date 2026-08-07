[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Baseline,

    [Parameter(Mandatory = $true)]
    [string] $Candidate,

    [string] $Approval
)

$ErrorActionPreference = 'Stop'
$issues = [System.Collections.Generic.List[string]]::new()
$httpMethods = @('get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace')

function Read-OpenApiDocument([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "OpenAPI document not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Get-PropertyValue($Object, [string] $Name) {
    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Has-Property($Object, [string] $Name) {
    return $null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]
}

function Get-PropertyNames($Object) {
    if ($null -eq $Object) {
        return @()
    }

    return @($Object.PSObject.Properties.Name)
}

function ConvertTo-StableJson($Value) {
    return ConvertTo-Json -InputObject $Value -Depth 100 -Compress
}

function Add-Issue([string] $Location, [string] $Message) {
    $issues.Add("${Location}: $Message")
}

function Test-ScalarUnchanged($BaselineValue, $CandidateValue, [string] $Location, [string] $Name) {
    if ($null -eq $BaselineValue) {
        return
    }

    if ($null -eq $CandidateValue -or
        (ConvertTo-StableJson $BaselineValue) -ne (ConvertTo-StableJson $CandidateValue)) {
        Add-Issue $Location "'$Name' changed or was removed"
    }
}

function Test-SchemaCompatibility($BaselineSchema, $CandidateSchema, [string] $Location) {
    if ($null -eq $BaselineSchema) {
        return
    }

    if ($null -eq $CandidateSchema) {
        Add-Issue $Location 'schema was removed'
        return
    }

    foreach ($name in @('$ref', 'type', 'format', 'pattern', 'minimum', 'maximum',
            'exclusiveMinimum', 'exclusiveMaximum', 'minLength', 'maxLength',
            'minItems', 'maxItems', 'additionalProperties')) {
        Test-ScalarUnchanged `
            (Get-PropertyValue $BaselineSchema $name) `
            (Get-PropertyValue $CandidateSchema $name) `
            $Location `
            $name
    }

    $baselineNullable = Get-PropertyValue $BaselineSchema 'nullable'
    $candidateNullable = Get-PropertyValue $CandidateSchema 'nullable'
    if ($baselineNullable -eq $true -and $candidateNullable -ne $true) {
        Add-Issue $Location 'nullable input/output was narrowed'
    }

    $baselineEnum = @(Get-PropertyValue $BaselineSchema 'enum')
    $candidateEnum = @(Get-PropertyValue $CandidateSchema 'enum')
    if ($baselineEnum.Count -gt 0) {
        foreach ($value in $baselineEnum) {
            $serialized = ConvertTo-StableJson $value
            $found = $false
            foreach ($candidateValue in $candidateEnum) {
                if ((ConvertTo-StableJson $candidateValue) -eq $serialized) {
                    $found = $true
                    break
                }
            }

            if (-not $found) {
                Add-Issue $Location "enum value $serialized was removed"
            }
        }
    }

    $baselineRequired = @(Get-PropertyValue $BaselineSchema 'required')
    $candidateRequired = @(Get-PropertyValue $CandidateSchema 'required')
    foreach ($requiredName in $candidateRequired) {
        if ($requiredName -notin $baselineRequired) {
            Add-Issue $Location "property '$requiredName' became required"
        }
    }

    $baselineProperties = Get-PropertyValue $BaselineSchema 'properties'
    $candidateProperties = Get-PropertyValue $CandidateSchema 'properties'
    foreach ($propertyName in Get-PropertyNames $baselineProperties) {
        if (-not (Has-Property $candidateProperties $propertyName)) {
            Add-Issue $Location "property '$propertyName' was removed"
            continue
        }

        Test-SchemaCompatibility `
            (Get-PropertyValue $baselineProperties $propertyName) `
            (Get-PropertyValue $candidateProperties $propertyName) `
            "$Location.properties.$propertyName"
    }

    if (Has-Property $BaselineSchema 'items') {
        Test-SchemaCompatibility `
            (Get-PropertyValue $BaselineSchema 'items') `
            (Get-PropertyValue $CandidateSchema 'items') `
            "$Location.items"
    }

    foreach ($composition in @('allOf', 'anyOf', 'oneOf')) {
        $baselineItems = @(Get-PropertyValue $BaselineSchema $composition)
        $candidateItems = @(Get-PropertyValue $CandidateSchema $composition)
        if ($baselineItems.Count -eq 0) {
            continue
        }

        if ($baselineItems.Count -ne $candidateItems.Count) {
            Add-Issue $Location "'$composition' alternatives changed"
            continue
        }

        for ($index = 0; $index -lt $baselineItems.Count; $index++) {
            Test-SchemaCompatibility `
                $baselineItems[$index] `
                $candidateItems[$index] `
                "$Location.$composition[$index]"
        }
    }
}

function Get-ParameterKey($Parameter) {
    $reference = Get-PropertyValue $Parameter '$ref'
    if ($null -ne $reference) {
        return "ref:$reference"
    }

    return "$(Get-PropertyValue $Parameter 'in'):$(Get-PropertyValue $Parameter 'name')"
}

function Test-Parameters($BaselineParameters, $CandidateParameters, [string] $Location) {
    $baselineByKey = @{}
    foreach ($parameter in @($BaselineParameters)) {
        $baselineByKey[(Get-ParameterKey $parameter)] = $parameter
    }

    $candidateByKey = @{}
    foreach ($parameter in @($CandidateParameters)) {
        $candidateByKey[(Get-ParameterKey $parameter)] = $parameter
    }

    foreach ($key in $baselineByKey.Keys) {
        if (-not $candidateByKey.ContainsKey($key)) {
            Add-Issue $Location "parameter '$key' was removed"
            continue
        }

        $baselineParameter = $baselineByKey[$key]
        $candidateParameter = $candidateByKey[$key]
        if ((Get-PropertyValue $baselineParameter 'required') -ne $true -and
            (Get-PropertyValue $candidateParameter 'required') -eq $true) {
            Add-Issue $Location "parameter '$key' became required"
        }

        Test-SchemaCompatibility `
            (Get-PropertyValue $baselineParameter 'schema') `
            (Get-PropertyValue $candidateParameter 'schema') `
            "$Location.parameters.$key"
    }

    foreach ($key in $candidateByKey.Keys) {
        if (-not $baselineByKey.ContainsKey($key) -and
            (Get-PropertyValue $candidateByKey[$key] 'required') -eq $true) {
            Add-Issue $Location "new required parameter '$key' was added"
        }
    }
}

function Test-Content($BaselineContent, $CandidateContent, [string] $Location) {
    foreach ($mediaType in Get-PropertyNames $BaselineContent) {
        if (-not (Has-Property $CandidateContent $mediaType)) {
            Add-Issue $Location "media type '$mediaType' was removed"
            continue
        }

        Test-SchemaCompatibility `
            (Get-PropertyValue (Get-PropertyValue $BaselineContent $mediaType) 'schema') `
            (Get-PropertyValue (Get-PropertyValue $CandidateContent $mediaType) 'schema') `
            "$Location.content.$mediaType"
    }
}

function Test-Operation($BaselineOperation, $CandidateOperation, [string] $Location) {
    Test-Parameters `
        (Get-PropertyValue $BaselineOperation 'parameters') `
        (Get-PropertyValue $CandidateOperation 'parameters') `
        $Location

    $baselineBody = Get-PropertyValue $BaselineOperation 'requestBody'
    $candidateBody = Get-PropertyValue $CandidateOperation 'requestBody'
    if ($null -ne $baselineBody) {
        if ($null -eq $candidateBody) {
            Add-Issue $Location 'request body was removed'
        }
        else {
            Test-Content `
                (Get-PropertyValue $baselineBody 'content') `
                (Get-PropertyValue $candidateBody 'content') `
                "$Location.requestBody"
        }
    }
    elseif ($null -ne $candidateBody -and (Get-PropertyValue $candidateBody 'required') -eq $true) {
        Add-Issue $Location 'a required request body was added'
    }

    if ((Get-PropertyValue $baselineBody 'required') -ne $true -and
        (Get-PropertyValue $candidateBody 'required') -eq $true) {
        Add-Issue $Location 'request body became required'
    }

    $baselineResponses = Get-PropertyValue $BaselineOperation 'responses'
    $candidateResponses = Get-PropertyValue $CandidateOperation 'responses'
    foreach ($statusCode in Get-PropertyNames $baselineResponses) {
        if (-not (Has-Property $candidateResponses $statusCode)) {
            Add-Issue $Location "response '$statusCode' was removed"
            continue
        }

        Test-Content `
            (Get-PropertyValue (Get-PropertyValue $baselineResponses $statusCode) 'content') `
            (Get-PropertyValue (Get-PropertyValue $candidateResponses $statusCode) 'content') `
            "$Location.responses.$statusCode"
    }

    $baselineSecurity = Get-PropertyValue $BaselineOperation 'security'
    $candidateSecurity = Get-PropertyValue $CandidateOperation 'security'
    if ($null -ne $baselineSecurity -or $null -ne $candidateSecurity) {
        Test-ScalarUnchanged $baselineSecurity $candidateSecurity $Location 'security'
    }
}

$baselineDocument = Read-OpenApiDocument $Baseline
$candidateDocument = Read-OpenApiDocument $Candidate
$baselinePaths = Get-PropertyValue $baselineDocument 'paths'
$candidatePaths = Get-PropertyValue $candidateDocument 'paths'

foreach ($path in Get-PropertyNames $baselinePaths) {
    if (-not (Has-Property $candidatePaths $path)) {
        Add-Issue $path 'path was removed'
        continue
    }

    $baselinePath = Get-PropertyValue $baselinePaths $path
    $candidatePath = Get-PropertyValue $candidatePaths $path
    Test-Parameters `
        (Get-PropertyValue $baselinePath 'parameters') `
        (Get-PropertyValue $candidatePath 'parameters') `
        $path

    foreach ($method in $httpMethods) {
        if (-not (Has-Property $baselinePath $method)) {
            continue
        }

        if (-not (Has-Property $candidatePath $method)) {
            Add-Issue "$method $path" 'operation was removed'
            continue
        }

        Test-Operation `
            (Get-PropertyValue $baselinePath $method) `
            (Get-PropertyValue $candidatePath $method) `
            "$method $path"
    }
}

$baselineSchemas = Get-PropertyValue (Get-PropertyValue $baselineDocument 'components') 'schemas'
$candidateSchemas = Get-PropertyValue (Get-PropertyValue $candidateDocument 'components') 'schemas'
foreach ($schemaName in Get-PropertyNames $baselineSchemas) {
    if (-not (Has-Property $candidateSchemas $schemaName)) {
        Add-Issue "components.schemas.$schemaName" 'schema was removed'
        continue
    }

    Test-SchemaCompatibility `
        (Get-PropertyValue $baselineSchemas $schemaName) `
        (Get-PropertyValue $candidateSchemas $schemaName) `
        "components.schemas.$schemaName"
}

if ($issues.Count -eq 0) {
    Write-Host 'OpenAPI compatibility check passed.'
    exit 0
}

Write-Host 'Breaking OpenAPI changes detected:'
foreach ($issue in $issues | Sort-Object -Unique) {
    Write-Host " - $issue"
}

if (-not [string]::IsNullOrWhiteSpace($Approval)) {
    Write-Warning "Breaking changes accepted by explicit approval: $Approval"
    exit 0
}

[Console]::Error.WriteLine('Publish a new API version or rerun with a recorded approval.')
exit 1

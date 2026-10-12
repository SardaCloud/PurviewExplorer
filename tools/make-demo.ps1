# Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
# Licensed under the MIT License. See LICENSE in the repository root.
# Generates src\PurviewExplorer\Resources\demo-snapshot.json: a fictional "Contoso" tenant used by File > Open Demo Data.
# All names, addresses and identifiers are made up. GUIDs and dates are deterministic so the output is stable.
# Run: pwsh -File tools\make-demo.ps1
#Requires -Version 7
[CmdletBinding()]
param(
    [string]$OutFile = (Join-Path $PSScriptRoot '..\src\PurviewExplorer\Resources\demo-snapshot.json')
)
$ErrorActionPreference = 'Stop'

$Base = [datetime]::new(2026, 10, 1, 9, 0, 0, [DateTimeKind]::Utc)
$Md5 = [System.Security.Cryptography.MD5]::Create()
function Dt([int]$DaysAgo, [int]$Hours = 0) { $Base.AddDays(-$DaysAgo).AddHours($Hours).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") }
function G([string]$Seed) { [guid]::new($Md5.ComputeHash([Text.Encoding]::UTF8.GetBytes($Seed))).ToString() }
$Admin = 'Megan Bowen'
$Admin2 = 'Alex Wilber'

function Merge([System.Collections.Specialized.OrderedDictionary]$Target, $Extra) {
    if ($Extra) { foreach ($k in $Extra.Keys) { $Target[$k] = $Extra[$k] } }
    $Target
}

# Common shape of compliance policies and rules.
function Obj([string]$Kind, [string]$Name, $Props, [int]$Created = 200, [int]$Changed = 30) {
    $id = G "$Kind/$Name"
    $o = [ordered]@{ Name = $Name }
    $o = Merge $o $Props
    foreach ($kv in ([ordered]@{
        Guid = $id; Identity = $id; ImmutableId = $id
        CreatedBy = $Admin; LastModifiedBy = $Admin2
        WhenCreatedUTC = Dt $Created; WhenChangedUTC = Dt $Changed
    }).GetEnumerator()) { if (-not $o.Contains($kv.Key)) { $o[$kv.Key] = $kv.Value } }
    $o
}

# ---------------------------------------------------------------- Information Protection
function Label($Name, $Display, $Priority, $Parent, $Color, $Tooltip, $Extra) {
    $settings = @("[color, $Color]", "[isparent, $(if ($Parent) {'False'} else {'True'})]")
    $o = Obj 'label' $Name ([ordered]@{
        DisplayName = $Display; Priority = $Priority
        ParentId = $(if ($Parent) { G "label/$Parent" } else { '' })
        ContentType = 'File, Email, Site, UnifiedGroup, SchematizedData'
        Tooltip = $Tooltip; Comment = ''; Disabled = $false; Mode = 'Enforce'
        Workload = 'Exchange, SharePoint, Intune, ModernGroup, OneDriveForBusiness'
        Settings = $settings
        LocaleSettings = @('{"LocaleKey":"DisplayName","Settings":[{"Key":"it-it","Value":"' + $Display + '"}]}')
    })
    Merge $o $Extra
}
$labels = @(
    Label 'Public' 'Public' 0 $null '#317100' 'Business data that is specifically prepared and approved for public consumption.' ([ordered]@{
        LabelActions = @(); EncryptionEnabled = $false; ContentMarkingHeaderEnabled = $false })
    Label 'General' 'General' 1 $null '#0078D4' 'Business data that is not intended for public consumption.' ([ordered]@{
        LabelActions = @('{"Type":"applycontentmarking","SubType":"footer","Settings":[{"Key":"text","Value":"Classified as General"},{"Key":"fontsize","Value":"8"},{"Key":"fontcolor","Value":"#000000"},{"Key":"alignment","Value":"Center"}]}')
        EncryptionEnabled = $false; ContentMarkingFooterEnabled = $true; ContentMarkingFooterText = 'Classified as General' })
    Label 'Confidential' 'Confidential' 2 $null '#FF8C00' 'Sensitive business data that could cause damage to the business if shared with unauthorized people.' ([ordered]@{
        LabelActions = @(); EncryptionEnabled = $false })
    Label 'Confidential-AllEmployees' 'All Employees' 3 'Confidential' '#FF8C00' 'Confidential data that requires protection, which allows all employees full permissions.' ([ordered]@{
        LabelActions = @(
            '{"Type":"encrypt","SubType":null,"Settings":[{"Key":"protectiontype","Value":"template"},{"Key":"contentexpiredondateindaysornever","Value":"Never"},{"Key":"offlineaccessdays","Value":"30"},{"Key":"rightsdefinitions","Value":"[{\"Identity\":\"AllStaff-7184AB3F-CCD1-46F3-8233-3E09E9CF0E66@contoso.onmicrosoft.com\",\"Rights\":\"VIEW,VIEWRIGHTSDATA,DOCEDIT,EDIT,PRINT,EXTRACT,REPLY,REPLYALL,FORWARD,OBJMODEL\"}]"}]}',
            '{"Type":"applycontentmarking","SubType":"header","Settings":[{"Key":"text","Value":"Confidential - All Employees"},{"Key":"fontsize","Value":"10"},{"Key":"fontcolor","Value":"#FF8C00"},{"Key":"alignment","Value":"Left"}]}')
        EncryptionEnabled = $true; EncryptionProtectionType = 'Template'; EncryptionOfflineAccessDays = 30
        ContentMarkingHeaderEnabled = $true; ContentMarkingHeaderText = 'Confidential - All Employees' })
    Label 'Confidential-SpecificPeople' 'Specific People' 4 'Confidential' '#FF8C00' 'Confidential data that requires protection. Users choose who can access it.' ([ordered]@{
        LabelActions = @('{"Type":"encrypt","SubType":null,"Settings":[{"Key":"protectiontype","Value":"userdefined"},{"Key":"promptuser","Value":"true"},{"Key":"doublekeyencryptionurl","Value":""}]}')
        EncryptionEnabled = $true; EncryptionProtectionType = 'UserDefined'; EncryptionPromptUser = $true })
    Label 'Confidential-Partners' 'Trusted Partners' 5 'Confidential' '#FF8C00' 'Confidential data shared with Fabrikam and Northwind Traders under NDA.' ([ordered]@{
        LabelActions = @('{"Type":"encrypt","SubType":null,"Settings":[{"Key":"protectiontype","Value":"template"},{"Key":"rightsdefinitions","Value":"[{\"Identity\":\"fabrikam.com\",\"Rights\":\"VIEW,VIEWRIGHTSDATA,REPLY,REPLYALL\"},{\"Identity\":\"northwindtraders.com\",\"Rights\":\"VIEW,VIEWRIGHTSDATA\"}]"}]}')
        EncryptionEnabled = $true; EncryptionProtectionType = 'Template' })
    Label 'HighlyConfidential' 'Highly Confidential' 6 $null '#C50F1F' 'Very sensitive business data that would cause damage to the business if it was shared with unauthorized people.' ([ordered]@{
        LabelActions = @(); EncryptionEnabled = $false })
    Label 'HighlyConfidential-AllEmployees' 'All Employees' 7 'HighlyConfidential' '#C50F1F' 'Highly confidential data that allows all employees view, edit, and reply permissions.' ([ordered]@{
        LabelActions = @('{"Type":"applywatermarking","SubType":null,"Settings":[{"Key":"text","Value":"HIGHLY CONFIDENTIAL"},{"Key":"fontsize","Value":"40"},{"Key":"layout","Value":"Diagonal"}]}')
        EncryptionEnabled = $true; EncryptionProtectionType = 'Template'; ContentMarkingWaterMarkEnabled = $true; ContentMarkingWaterMarkText = 'HIGHLY CONFIDENTIAL' })
    Label 'HighlyConfidential-SpecificPeople' 'Specific People' 8 'HighlyConfidential' '#C50F1F' 'Highly confidential data that requires protection. Users choose who can access it.' ([ordered]@{
        LabelActions = @(); EncryptionEnabled = $true; EncryptionProtectionType = 'UserDefined'; EncryptionDoNotForward = $true })
    Label 'HighlyConfidential-Project-Falcon' 'Project Falcon' 9 'HighlyConfidential' '#C50F1F' 'Restricted to members of the Project Falcon team.' ([ordered]@{
        LabelActions = @(); EncryptionEnabled = $true; EncryptionProtectionType = 'Template'
        SiteAndGroupProtectionEnabled = $true; SiteAndGroupProtectionPrivacy = 'Private'; SiteExternalSharingControlType = 'Disabled' })
)
$labelIds = @{}; foreach ($l in $labels) { $labelIds[$l.Name] = $l.Guid }

$labelPolicies = @(
    Obj 'labelpolicy' 'Global sensitivity label policy' ([ordered]@{
        Priority = 0; Mode = 'Enforce'; Enabled = $true; Type = 'PublishedSensitivityLabel'
        Labels = @('Public', 'General', 'Confidential', 'Confidential-AllEmployees', 'Confidential-SpecificPeople', 'HighlyConfidential', 'HighlyConfidential-AllEmployees', 'HighlyConfidential-SpecificPeople')
        ExchangeLocation = @('All'); ModernGroupLocation = @()
        Settings = @('[requiredowngradejustification, true]', "[defaultlabelid, $($labelIds['General'])]", '[mandatory, false]', '[siteandgroupmandatory, false]', '[outlookdefaultlabel, None]', '[powerbimandatory, false]')
        DistributionStatus = 'Success'; Workload = 'Exchange' })
    Obj 'labelpolicy' 'Project Falcon label policy' ([ordered]@{
        Priority = 1; Mode = 'Enforce'; Enabled = $true; Type = 'PublishedSensitivityLabel'
        Labels = @('Confidential-Partners', 'HighlyConfidential-Project-Falcon')
        ExchangeLocation = @('falcon-team@contoso.com'); ModernGroupLocation = @('falcon-team@contoso.com')
        Settings = @('[requiredowngradejustification, true]', '[mandatory, true]')
        DistributionStatus = 'Success'; Workload = 'Exchange' })
)

$autoPolicies = @(
    Obj 'autopolicy' 'Credit card numbers in SharePoint and OneDrive' ([ordered]@{
        Priority = 0; Mode = 'TestWithoutNotifications'; Enabled = $true
        ApplySensitivityLabel = 'Confidential-AllEmployees'; Workload = 'SharePoint, OneDriveForBusiness'
        SharePointLocation = @('All'); OneDriveLocation = @('All'); DistributionStatus = 'Success' })
    Obj 'autopolicy' 'Passport numbers in email' ([ordered]@{
        Priority = 1; Mode = 'Enable'; Enabled = $true
        ApplySensitivityLabel = 'HighlyConfidential-AllEmployees'; Workload = 'Exchange'
        ExchangeLocation = @('All'); DistributionStatus = 'Success' })
)
$autoRules = @(
    Obj 'autorule' 'Credit card numbers - high count' ([ordered]@{
        ParentPolicyName = 'Credit card numbers in SharePoint and OneDrive'; Policy = (G 'autopolicy/Credit card numbers in SharePoint and OneDrive')
        Priority = 0; Mode = 'TestWithoutNotifications'; Disabled = $false; Workload = 'SharePoint'
        ContentContainsSensitiveInformation = @([ordered]@{ name = 'Credit Card Number'; mincount = '10'; maxcount = '-1'; minconfidence = '85'; maxconfidence = '100' }) })
    Obj 'autorule' 'Passport numbers' ([ordered]@{
        ParentPolicyName = 'Passport numbers in email'; Policy = (G 'autopolicy/Passport numbers in email')
        Priority = 0; Mode = 'Enable'; Disabled = $false; Workload = 'Exchange'
        ContentContainsSensitiveInformation = @(
            [ordered]@{ name = 'U.S. / U.K. Passport Number'; mincount = '1'; minconfidence = '75' },
            [ordered]@{ name = 'Italy Passport Number'; mincount = '1'; minconfidence = '75' }) })
)

# ---------------------------------------------------------------- Data Classification
function Sit($Name, $Publisher, $Confidence, $Description) {
    [ordered]@{
        Name = $Name; Id = (G "sit/$Name"); Publisher = $Publisher; Type = 'Entity'
        RecommendedConfidence = $Confidence; Description = $Description; Locale = 'en-US'
        RulePackId = $(if ($Publisher -eq 'Contoso') { G 'rulepack/Contoso' } else { G 'rulepack/Microsoft' })
        State = 'Published'
    }
}
$sits = @(
    Sit 'Credit Card Number' 'Microsoft Corporation' 85 'Detects valid credit card numbers from major brands, validated with the Luhn check.'
    Sit 'U.S. Social Security Number (SSN)' 'Microsoft Corporation' 85 'Detects nine-digit U.S. Social Security Numbers in formatted and unformatted patterns.'
    Sit 'U.S. Bank Account Number' 'Microsoft Corporation' 75 'Detects 6-17 digit U.S. bank account numbers near related keywords.'
    Sit 'International Banking Account Number (IBAN)' 'Microsoft Corporation' 85 'Detects IBANs with a valid country code and check digits.'
    Sit 'EU Debit Card Number' 'Microsoft Corporation' 85 'Detects 16-digit EU debit card numbers.'
    Sit 'U.S. / U.K. Passport Number' 'Microsoft Corporation' 75 'Detects nine-digit passport numbers near passport keywords.'
    Sit 'Italy Fiscal Code' 'Microsoft Corporation' 85 'Detects the 16-character Italian codice fiscale with a valid checksum.'
    Sit 'Italy Passport Number' 'Microsoft Corporation' 75 'Detects Italian passport numbers near passport keywords.'
    Sit 'Germany Identity Card Number' 'Microsoft Corporation' 85 'Detects German Personalausweis numbers.'
    Sit 'Azure Storage Account Key' 'Microsoft Corporation' 85 'Detects Azure Storage account keys and connection strings.'
    Sit 'Contoso Employee ID' 'Contoso' 85 'Contoso employee numbers: CTS- followed by six digits.'
    Sit 'Contoso Project Codename' 'Contoso' 75 'Matches entries of the Contoso Project Codenames keyword dictionary.'
)
$rulePackages = @(
    [ordered]@{ RuleCollectionName = 'Microsoft Rule Package'; LocalizedName = 'Microsoft Rule Package'; Publisher = 'Microsoft Corporation'; Version = '15.20.8001.0'; Identity = (G 'rulepack/Microsoft'); WhenChangedUTC = (Dt 40) }
    [ordered]@{ RuleCollectionName = 'Contoso Custom Sensitive Information Types'; LocalizedName = 'Contoso Custom SITs'; Publisher = 'Contoso'; Version = '1.0.3.0'; Identity = (G 'rulepack/Contoso'); WhenChangedUTC = (Dt 75)
        SerializedClassificationRuleCollection = '<RulePackage xmlns="http://schemas.microsoft.com/office/2011/mce"><RulePack id="' + (G 'rulepack/Contoso') + '"><Version major="1" minor="0" build="3" revision="0"/><Publisher id="' + (G 'publisher/Contoso') + '"/></RulePack><Rules><Entity id="' + (G 'sit/Contoso Employee ID') + '" patternsProximity="300" recommendedConfidence="85"><Pattern confidenceLevel="85"><IdMatch idRef="Regex_contoso_employee_id"/></Pattern></Entity><Regex id="Regex_contoso_employee_id">CTS-\d{6}</Regex></Rules></RulePackage>' }
)
$keywordDictionaries = @(
    [ordered]@{ Name = 'Contoso Project Codenames'; Description = 'Internal project codenames that must not leave the company.'; Identity = (G 'kwd/codenames')
        KeywordDictionary = 'Falcon, Kestrel, Osprey, Harrier, Merlin, Peregrine'; WhenChangedUTC = (Dt 90) }
)
$edmSchemas = @(
    [ordered]@{ Name = 'Contoso patient records'; DataStoreName = 'patientrecords'; Identity = (G 'edm/patients'); Version = '1'; WhenChangedUTC = (Dt 120)
        EdmSchemaXml = '<EdmSchema xmlns="http://schemas.microsoft.com/office/2018/edm"><DataStore name="patientrecords" description="Patient records" version="1"><Field name="PatientID" searchable="true" caseInsensitive="true"/><Field name="MRN" searchable="true"/><Field name="FirstName"/><Field name="LastName"/><Field name="DOB"/></DataStore></EdmSchema>' }
)

# ---------------------------------------------------------------- Data Loss Prevention
$dlpPolicies = @(
    Obj 'dlp' 'U.S. Financial Data' ([ordered]@{
        Priority = 0; Mode = 'Enable'; Enabled = $true; Type = 'Dlp'
        Workload = 'Exchange, SharePoint, OneDriveForBusiness, Teams'
        ExchangeLocation = @('All'); SharePointLocation = @('All'); OneDriveLocation = @('All'); TeamsLocation = @('All')
        DistributionStatus = 'Success'; Comment = 'Based on the U.S. Financial Data template.' })
    Obj 'dlp' 'GDPR Enhanced' ([ordered]@{
        Priority = 1; Mode = 'TestWithNotifications'; Enabled = $true; Type = 'Dlp'
        Workload = 'Exchange, SharePoint, OneDriveForBusiness'
        ExchangeLocation = @('All'); SharePointLocation = @('All'); OneDriveLocation = @('All')
        DistributionStatus = 'Success'; Comment = 'In simulation since 2026-09; review before enabling.' })
    Obj 'dlp' 'Endpoint - Highly Confidential to USB' ([ordered]@{
        Priority = 2; Mode = 'Enable'; Enabled = $true; Type = 'Dlp'
        Workload = 'EndpointDevices'; EndpointDlpLocation = @('All')
        DistributionStatus = 'Success' })
)
$advancedRule = '{"Version":"1.0","Condition":{"Operator":"And","SubConditions":[{"ConditionName":"ContentContainsSensitiveInformation","Value":[{"groups":[{"Operator":"Or","labels":[{"name":"' + $labelIds['HighlyConfidential-AllEmployees'] + '","type":"Sensitivity"},{"name":"' + $labelIds['HighlyConfidential-SpecificPeople'] + '","type":"Sensitivity"}],"name":"Default"}],"Operator":"And"}]}]}}'
$dlpRules = @(
    Obj 'dlprule' 'Low volume of U.S. financial data' ([ordered]@{
        ParentPolicyName = 'U.S. Financial Data'; Policy = (G 'dlp/U.S. Financial Data'); Priority = 0; Mode = 'Enable'; Disabled = $false
        ReportSeverityLevel = 'Low'; BlockAccess = $false; NotifyUser = @('Owner', 'LastModifier'); GenerateAlert = @()
        ContentContainsSensitiveInformation = @(
            [ordered]@{ name = 'Credit Card Number'; mincount = '1'; maxcount = '9'; minconfidence = '85' },
            [ordered]@{ name = 'U.S. Bank Account Number'; mincount = '1'; maxcount = '9'; minconfidence = '75' }) })
    Obj 'dlprule' 'High volume of U.S. financial data' ([ordered]@{
        ParentPolicyName = 'U.S. Financial Data'; Policy = (G 'dlp/U.S. Financial Data'); Priority = 1; Mode = 'Enable'; Disabled = $false
        ReportSeverityLevel = 'High'; BlockAccess = $true; BlockAccessScope = 'PerUser'; NotifyUser = @('Owner', 'SiteAdmin')
        GenerateAlert = @('dlp-alerts@contoso.com'); GenerateIncidentReport = @('dlp-alerts@contoso.com')
        NotifyAllowOverride = @('WithJustification')
        ContentContainsSensitiveInformation = @(
            [ordered]@{ name = 'Credit Card Number'; mincount = '10'; maxcount = '-1'; minconfidence = '85' },
            [ordered]@{ name = 'U.S. Bank Account Number'; mincount = '10'; maxcount = '-1'; minconfidence = '75' }) })
    Obj 'dlprule' 'EU personal data shared externally' ([ordered]@{
        ParentPolicyName = 'GDPR Enhanced'; Policy = (G 'dlp/GDPR Enhanced'); Priority = 2; Mode = 'TestWithNotifications'; Disabled = $false
        ReportSeverityLevel = 'Medium'; BlockAccess = $false; AccessScope = 'NotInOrganization'; NotifyUser = @('Owner')
        ContentContainsSensitiveInformation = @(
            [ordered]@{ name = 'Italy Fiscal Code'; mincount = '1'; minconfidence = '85' },
            [ordered]@{ name = 'Germany Identity Card Number'; mincount = '1'; minconfidence = '85' },
            [ordered]@{ name = 'International Banking Account Number (IBAN)'; mincount = '1'; minconfidence = '85' }) })
    Obj 'dlprule' 'Block copy of Highly Confidential to removable media' ([ordered]@{
        ParentPolicyName = 'Endpoint - Highly Confidential to USB'; Policy = (G 'dlp/Endpoint - Highly Confidential to USB'); Priority = 3; Mode = 'Enable'; Disabled = $false
        ReportSeverityLevel = 'High'; GenerateAlert = @('SiteAdmin')
        AdvancedRule = $advancedRule
        EndpointDlpRestrictions = @(
            [ordered]@{ Setting = 'RemovableMedia'; Value = 'Block' },
            [ordered]@{ Setting = 'CloudEgress'; Value = 'Audit' },
            [ordered]@{ Setting = 'Print'; Value = 'Warn' }) })
)
$policyTips = @(
    [ordered]@{ Name = 'en-US\NotifyUser'; Action = 'NotifyUser'; Locale = 'en-US'; Value = 'This item contains financial data. Check the recipients before you send it.'; Identity = (G 'tip/en') }
    [ordered]@{ Name = 'it-IT\NotifyUser'; Action = 'NotifyUser'; Locale = 'it-IT'; Value = 'Questo elemento contiene dati finanziari. Verifica i destinatari prima di inviarlo.'; Identity = (G 'tip/it') }
)
$policyConfig = @(
    [ordered]@{ Identity = 'PolicyConfig'; Name = 'PolicyConfig'
        EnableLabelCoauth = $true; EnableSpoAipMigration = $true; EnableAdvancedRuleBuilder = $true
        EndpointDlpGlobalSettings = @(
            [ordered]@{ Setting = 'PathExclusion'; Value = @('C:\Users\*\AppData\Local\Temp\*') },
            [ordered]@{ Setting = 'UnallowedApp'; Value = @([ordered]@{ Executable = 'fakeuploader.exe'; Name = 'Fake Uploader' }) },
            [ordered]@{ Setting = 'CloudAppMode'; Value = 'Block' },
            [ordered]@{ Setting = 'CloudAppRestrictionList'; Value = @('dropbox.com', 'wetransfer.com') },
            [ordered]@{ Setting = 'AdvancedClassificationEnabled'; Value = 'true' })
        WhenChangedUTC = (Dt 12) }
)

# ---------------------------------------------------------------- Data Lifecycle Management
$retentionPolicies = @(
    Obj 'ret' 'Org-wide mail and OneDrive - 7 years' ([ordered]@{
        Enabled = $true; Mode = 'Enforce'; Type = 'Retention'; RetentionRuleTypes = @('Default')
        Workload = 'Exchange, OneDriveForBusiness'; ExchangeLocation = @('All'); OneDriveLocation = @('All'); DistributionStatus = 'Success' })
    Obj 'ret' 'Teams chats - delete after 1 year' ([ordered]@{
        Enabled = $true; Mode = 'Enforce'; Type = 'Retention'; RetentionRuleTypes = @('Default')
        Workload = 'Skype'; TeamsChatLocation = @('All'); TeamsChannelLocation = @('All'); DistributionStatus = 'Success' })
    Obj 'ret' 'Publish records labels' ([ordered]@{
        Enabled = $true; Mode = 'Enforce'; Type = 'Retention'; RetentionRuleTypes = @('Publish')
        Workload = 'Exchange, SharePoint, OneDriveForBusiness, ModernGroup'
        ExchangeLocation = @('All'); SharePointLocation = @('All'); OneDriveLocation = @('All'); ModernGroupLocation = @('All'); DistributionStatus = 'Success' })
    Obj 'ret' 'Auto-apply: contracts in Finance sites' ([ordered]@{
        Enabled = $true; Mode = 'Enforce'; Type = 'Retention'; RetentionRuleTypes = @('ApplyTag')
        Workload = 'SharePoint'; AdaptiveScopeLocation = @('Finance SharePoint sites'); DistributionStatus = 'Pending' })
)
$retentionRules = @(
    Obj 'retrule' 'Org-wide mail and OneDrive - 7 years rule' ([ordered]@{
        Policy = (G 'ret/Org-wide mail and OneDrive - 7 years'); RetentionDuration = 2555; RetentionDurationDisplayHint = 'Days'
        RetentionComplianceAction = 'KeepAndDelete'; ExpirationDateOption = 'CreationAgeInDays'; Mode = 'Enforce'; Disabled = $false })
    Obj 'retrule' 'Teams chats - delete after 1 year rule' ([ordered]@{
        Policy = (G 'ret/Teams chats - delete after 1 year'); RetentionDuration = 365; RetentionDurationDisplayHint = 'Days'
        RetentionComplianceAction = 'Delete'; ExpirationDateOption = 'CreationAgeInDays'; Mode = 'Enforce'; Disabled = $false })
    Obj 'retrule' 'Publish records labels rule' ([ordered]@{
        Policy = (G 'ret/Publish records labels'); PublishComplianceTag = @('Contract - 7 years', 'HR record - 10 years after departure', 'Tax record - 10 years', 'Legal hold - keep forever')
        Mode = 'Enforce'; Disabled = $false })
    Obj 'retrule' 'Auto-apply: contracts in Finance sites rule' ([ordered]@{
        Policy = (G 'ret/Auto-apply: contracts in Finance sites'); ApplyComplianceTag = 'Contract - 7 years'
        ContentMatchQuery = 'contract OR "statement of work" OR "master services agreement"'; Mode = 'Enforce'; Disabled = $false })
)
$appPolicies = @(
    Obj 'appret' 'Copilot interactions - 2 years' ([ordered]@{
        Applications = @('User:M365Copilot'); Enabled = $true; Mode = 'Enforce'; DistributionStatus = 'Success' })
    Obj 'appret' 'Viva Engage - 3 years' ([ordered]@{
        Applications = @('User:VivaEngage', 'Group:VivaEngage'); Enabled = $true; Mode = 'Enforce'; DistributionStatus = 'Success' })
)
$appRules = @(
    Obj 'appretrule' 'Copilot interactions - 2 years rule' ([ordered]@{
        Policy = (G 'appret/Copilot interactions - 2 years'); RetentionDuration = 730; RetentionComplianceAction = 'KeepAndDelete'; ExpirationDateOption = 'CreationAgeInDays'; Mode = 'Enforce'; Disabled = $false })
    Obj 'appretrule' 'Viva Engage - 3 years rule' ([ordered]@{
        Policy = (G 'appret/Viva Engage - 3 years'); RetentionDuration = 1095; RetentionComplianceAction = 'Keep'; ExpirationDateOption = 'CreationAgeInDays'; Mode = 'Enforce'; Disabled = $false })
)
$scopes = @(
    Obj 'scope' 'Executives' ([ordered]@{
        LocationType = 'User'; FilterConditions = '{"Conditions":[{"Value":"Executive Leadership","Operator":"Equals","Name":"Department"}],"Conjunction":"And"}'
        RawQuery = ''; AdministrativeUnits = @() })
    Obj 'scope' 'Finance SharePoint sites' ([ordered]@{
        LocationType = 'Site'; FilterConditions = ''; RawQuery = 'RefinableString00=Finance'; AdministrativeUnits = @() })
)

# ---------------------------------------------------------------- Records Management
function Tag($Name, $Priority, $Action, $Duration, $Type, $Record, $Regulatory, $EventType, $Props) {
    $o = Obj 'tag' $Name ([ordered]@{
        Priority = $Priority; RetentionAction = $Action; RetentionDuration = $Duration; RetentionType = $Type
        IsRecordLabel = $Record; Regulatory = $Regulatory; EventType = $EventType
        Workload = 'Exchange, SharePoint, OneDriveForBusiness, ModernGroup'; ReviewerEmail = @(); Comment = '' })
    Merge $o $Props
}
$tags = @(
    Tag 'Contract - 7 years' 0 'KeepAndDelete' '2555' 'CreationAgeInDays' $false $false $null ([ordered]@{ FilePlanMetadata = '{"Settings":[{"Key":"FilePlanPropertyDepartment","Value":"Finance"},{"Key":"FilePlanPropertyCategory","Value":"Contracts"},{"Key":"FilePlanPropertyAuthority","Value":"Business"}]}' })
    Tag 'HR record - 10 years after departure' 1 'KeepAndDelete' '3650' 'EventAgeInDays' $true $false 'Employee departure' ([ordered]@{ FilePlanMetadata = '{"Settings":[{"Key":"FilePlanPropertyDepartment","Value":"Human Resources"},{"Key":"FilePlanPropertyCategory","Value":"Payroll"}]}' })
    Tag 'Tax record - 10 years' 2 'KeepAndDelete' '3650' 'CreationAgeInDays' $true $false $null ([ordered]@{ FilePlanMetadata = '{"Settings":[{"Key":"FilePlanPropertyDepartment","Value":"Finance"},{"Key":"FilePlanPropertyCategory","Value":"Tax"},{"Key":"FilePlanPropertyCitation","Value":"Sarbanes-Oxley Act of 2002"}]}' })
    Tag 'Legal hold - keep forever' 3 'Keep' 'Unlimited' 'TaggedAgeInDays' $true $true $null ([ordered]@{ ReviewerEmail = @('legal@contoso.com') })
    Tag 'Delete after 1 year' 4 'Delete' '365' 'ModificationAgeInDays' $false $false $null $null
)
$eventTypes = @(
    Obj 'evtype' 'Employee departure' ([ordered]@{ Comment = 'Triggered by HR when an employee leaves Contoso.' })
    Obj 'evtype' 'Contract expiration' ([ordered]@{ Comment = 'Triggered when a customer or vendor contract ends.' })
)
$events = @(
    Obj 'event' 'Departure - employee CTS-104233' ([ordered]@{ EventType = 'Employee departure'; EventDateTime = (Dt 21); EventStatus = 'Completed'
        SharePointAssetIdQuery = 'EmployeeNumber:CTS-104233'; ExchangeAssetIdQuery = 'CTS-104233' }) 22 21
)
function FilePlan($Kind, $Name, $Props) { Obj "fp-$Kind" $Name (Merge ([ordered]@{ DisplayName = $Name }) $Props) 300 300 }
$departments = @('Finance', 'Human Resources', 'Legal', 'Research') | ForEach-Object { FilePlan 'dept' $_ $null }
$categories = @('Contracts', 'Payroll', 'Tax', 'Intellectual property') | ForEach-Object { FilePlan 'cat' $_ $null }
$subCategories = @(
    FilePlan 'subcat' 'Vendor contracts' ([ordered]@{ ParentId = (G 'fp-cat/Contracts') })
    FilePlan 'subcat' 'Customer contracts' ([ordered]@{ ParentId = (G 'fp-cat/Contracts') })
)
$authorities = @('Business', 'Legal', 'Regulatory') | ForEach-Object { FilePlan 'auth' $_ $null }
$citations = @(
    FilePlan 'cit' 'Sarbanes-Oxley Act of 2002' ([ordered]@{ CitationUrl = 'https://www.congress.gov/bill/107th-congress/house-bill/3763'; CitationJurisdiction = 'U.S. Federal' })
    FilePlan 'cit' 'GDPR Article 17' ([ordered]@{ CitationUrl = 'https://eur-lex.europa.eu/eli/reg/2016/679/oj'; CitationJurisdiction = 'European Union' })
)
$referenceIds = @('FIN-001', 'FIN-002', 'HR-001', 'LEG-001') | ForEach-Object { FilePlan 'ref' $_ $null }

# ---------------------------------------------------------------- eDiscovery
function Case($Name, $Type, $Status, $Created, $Closed, $Description) {
    $id = G "case/$Name"
    [ordered]@{
        Name = $Name; Identity = $id; CaseType = $Type; Status = $Status; Description = $Description
        CreatedDateTime = (Dt $Created); ClosedDateTime = $(if ($Closed) { Dt $Closed } else { $null }); ClosedBy = $(if ($Closed) { $Admin2 } else { $null })
        CreatedBy = $Admin; LastModifiedDateTime = (Dt ([Math]::Max(1, $Created - 30)))
    }
}
$cases = @(
    Case 'Contoso v. Fabrikam' 'eDiscovery' 'Active' 160 $null 'Contract dispute with Fabrikam, Inc.'
    Case 'Internal review 2026-07' 'eDiscovery' 'Closed' 95 40 'Expense policy review.'
)
$premiumCases = @(
    Case 'Project Falcon leak investigation' 'AdvancedEdiscovery' 'Active' 50 $null 'Investigation of a suspected disclosure of Project Falcon documents.'
)
$caseHolds = @(
    Obj 'hold' 'Fabrikam custodians' ([ordered]@{ Case = 'Contoso v. Fabrikam'; CaseId = (G 'case/Contoso v. Fabrikam'); Enabled = $true; Mode = 'Enforce'; DistributionStatus = 'Success'
        ExchangeLocation = @('meganb@contoso.com', 'alexw@contoso.com', 'sales-emea@contoso.com'); SharePointLocation = @('https://contoso.sharepoint.com/sites/FabrikamAccount') }) 150 60
    Obj 'hold' 'Falcon team hold' ([ordered]@{ Case = 'Project Falcon leak investigation'; CaseId = (G 'case/Project Falcon leak investigation'); Enabled = $true; Mode = 'Enforce'; DistributionStatus = 'Success'
        ExchangeLocation = @('falcon-team@contoso.com'); SharePointLocation = @('https://contoso.sharepoint.com/sites/ProjectFalcon') }) 49 49
)
$searches = @(
    [ordered]@{ Name = 'Fabrikam correspondence'; Status = 'Completed'; ContentMatchQuery = 'fabrikam AND (contract OR invoice) AND sent>=2025-01-01'
        ExchangeLocation = @('All'); SharePointLocation = @(); CreatedBy = $Admin; JobEndTime = (Dt 30); Items = 1284; Size = 418273011; Identity = (G 'search/fab') }
    [ordered]@{ Name = 'Credit card numbers in public sites'; Status = 'Completed'; ContentMatchQuery = 'SensitiveType:"Credit Card Number|1..500|85..100"'
        ExchangeLocation = @(); SharePointLocation = @('All'); CreatedBy = $Admin2; JobEndTime = (Dt 8); Items = 37; Size = 12038854; Identity = (G 'search/cc') }
)
$securityFilters = @(
    [ordered]@{ FilterName = 'EU investigators - EU data only'; Action = 'All'; Users = @('eu-investigators@contoso.com'); Filters = @("Mailbox_CountryCode -eq '380'", "Site_Path -like 'https://contoso.sharepoint.com/sites/EU*'"); Region = ''; Description = 'Compliance boundary for the EU investigations team.' }
)

# ---------------------------------------------------------------- Audit, IRM, CC, IB, alerts, permissions
$auditPolicies = @(
    Obj 'audit' 'Exchange admin actions - 10 years' ([ordered]@{ Priority = 10; RetentionDuration = 'TenYears'; RecordTypes = @('ExchangeAdmin'); Operations = @(); UserIds = @(); Enabled = $true })
    Obj 'audit' 'DLP events - 3 years' ([ordered]@{ Priority = 20; RetentionDuration = 'ThreeYears'; RecordTypes = @('ComplianceDLPExchange', 'ComplianceDLPSharePoint', 'DLPEndpoint'); Operations = @(); UserIds = @(); Enabled = $true })
    Obj 'audit' 'Executive mailbox access - 1 year' ([ordered]@{ Priority = 30; RetentionDuration = 'OneYear'; RecordTypes = @('ExchangeItem'); Operations = @('MailItemsAccessed', 'Send'); UserIds = @('ceo@contoso.com', 'cfo@contoso.com'); Enabled = $true })
)
$auditConfig = @(
    [ordered]@{ Name = 'Admin Audit Log Settings'; UnifiedAuditLogIngestionEnabled = $true; AdminAuditLogEnabled = $true; AdminAuditLogAgeLimit = '90.00:00:00'; AdminAuditLogCmdlets = @('*'); WhenChangedUTC = (Dt 365) }
)
$irmPolicies = @(
    Obj 'irm' 'Data theft by departing users' ([ordered]@{ InsiderRiskScenario = 'IntellectualPropertyTheft'; Enabled = $true; Mode = 'Enable' })
    Obj 'irm' 'Data leaks by priority users' ([ordered]@{ InsiderRiskScenario = 'LeakOfInformation'; Enabled = $true; Mode = 'Enable' })
)
$irmCases = @( Case 'IRM case 12 - unusual downloads' 'InsiderRisk' 'Active' 14 $null 'Opened from a high-severity alert.' )
$ccPolicies = @(
    Obj 'cc' 'Inappropriate text' ([ordered]@{ Enabled = $true; Reviewers = @('cc-reviewers@contoso.com'); Comment = 'Detects threats, harassment and discrimination in Teams and email.' })
    Obj 'cc' 'Regulatory compliance - Finance' ([ordered]@{ Enabled = $true; Reviewers = @('finance-compliance@contoso.com'); Comment = 'Monitors communications of the trading desk.' })
)
$ccRules = @(
    Obj 'ccrule' 'Inappropriate text rule' ([ordered]@{ Policy = (G 'cc/Inappropriate text'); SamplingRate = 100; Mode = 'Enforce'
        Condition = '(Reviewee:cc-monitored@contoso.com) AND (TrainableClassifier:Threat OR TrainableClassifier:Harassment OR TrainableClassifier:Discrimination)' })
    Obj 'ccrule' 'Regulatory compliance - Finance rule' ([ordered]@{ Policy = (G 'cc/Regulatory compliance - Finance'); SamplingRate = 10; Mode = 'Enforce'
        Condition = '(Reviewee:trading-desk@contoso.com) AND (TrainableClassifier:RegulatoryCollusion OR TrainableClassifier:StockManipulation)' })
)
$segments = @(
    [ordered]@{ Name = 'Sales'; UserGroupFilter = "Department -eq 'Sales'"; Identity = (G 'seg/sales'); WhenChanged = (Dt 70) }
    [ordered]@{ Name = 'Research'; UserGroupFilter = "Department -eq 'Research'"; Identity = (G 'seg/research'); WhenChanged = (Dt 70) }
    [ordered]@{ Name = 'Everyone else'; UserGroupFilter = "Department -ne 'Sales' -and Department -ne 'Research'"; Identity = (G 'seg/other'); WhenChanged = (Dt 70) }
)
$ibPolicies = @(
    [ordered]@{ Name = 'Sales-Research'; State = 'Active'; AssignedSegment = 'Sales'; SegmentsAllowed = @(); SegmentsBlocked = @('Research'); ModerationAllowed = $false; Identity = (G 'ib/sr'); WhenChanged = (Dt 69) }
    [ordered]@{ Name = 'Research-Sales'; State = 'Active'; AssignedSegment = 'Research'; SegmentsAllowed = @(); SegmentsBlocked = @('Sales'); ModerationAllowed = $false; Identity = (G 'ib/rs'); WhenChanged = (Dt 69) }
)
$ibStatus = @(
    [ordered]@{ Identity = (G 'ib/apply'); Status = 'Completed'; CreationTime = (Dt 68); LastModifiedTime = (Dt 68 3); ApplicationTotalCount = 1214; ApplicationFailedCount = 0; PercentProgress = 100 }
)
function Alert($Name, $Category, $Severity, $System, $Aggregation, $Notify, $Disabled = $false) {
    Obj 'alert' $Name ([ordered]@{ Category = $Category; Severity = $Severity; Disabled = $Disabled; IsSystemRule = $System; AggregationType = $Aggregation; NotifyUser = $Notify; ThreatType = 'Activity' }) 400 $(if ($System) { 400 } else { 25 })
}
$alerts = @(
    Alert 'Elevation of Exchange admin privilege' 'Permissions' 'Low' $true 'None' @('TenantAdmins')
    Alert 'Creation of forwarding/redirect rule' 'ThreatManagement' 'Informational' $true 'None' @('TenantAdmins')
    Alert 'Unusual volume of file deletion' 'InformationGovernance' 'Medium' $true 'AnomalousAggregation' @('TenantAdmins')
    Alert 'DLP: high volume of financial data shared' 'DataLossPrevention' 'High' $false 'SimpleAggregation' @('dlp-alerts@contoso.com')
    Alert 'Retention label changed on a record' 'InformationGovernance' 'Medium' $false 'None' @('records@contoso.com') $true
)
function RoleGroup($Name, $Roles, $Members, $Description) {
    [ordered]@{ Name = ($Name -replace ' ', ''); DisplayName = $Name; Members = $Members; Roles = $Roles; Description = $Description; Identity = (G "rg/$Name"); WhenChanged = (Dt 33) }
}
$roleGroups = @(
    RoleGroup 'Compliance Administrator' @('Case Management', 'Compliance Administrator', 'DLP Compliance Management', 'Hold', 'IB Compliance Management', 'Information Protection Admin', 'Retention Management', 'View-Only Audit Logs') @($Admin) 'Members can manage settings for device management, data loss prevention, reports, and preservation.'
    RoleGroup 'eDiscovery Manager' @('Case Management', 'Communication', 'Compliance Search', 'Custodian', 'Export', 'Hold', 'Preview', 'Review', 'RMS Decrypt') @($Admin2, 'Lidia Holloway') 'Members can perform searches and place holds on mailboxes, SharePoint Online sites, and OneDrive for Business locations.'
    RoleGroup 'Information Protection' @('Information Protection Admin', 'Information Protection Analyst', 'Information Protection Investigator', 'Information Protection Reader') @('Isaiah Langer') 'Full control over all information protection features, including sensitivity labels and their policies, DLP, and all classifier types.'
    RoleGroup 'Records Management' @('Disposition Management', 'Retention Management', 'View-Only Retention Management') @('Lynne Robbins') 'Members can configure all aspects of records management, including retention labels and disposition reviews.'
    RoleGroup 'Insider Risk Management' @('Insider Risk Management Admin', 'Insider Risk Management Analysis', 'Insider Risk Management Investigation') @() 'Use this role group to manage insider risk management for your organization in a single group.'
    RoleGroup 'Communication Compliance' @('Communication Compliance Admin', 'Communication Compliance Analysis', 'Communication Compliance Investigation', 'Communication Compliance Viewer') @('Nestor Wilke') 'Provides permission to all the communication compliance roles: administrator, analyst, investigator, and viewer.'
    RoleGroup 'Global Reader' @('View-Only Configuration', 'View-Only Recipients', 'View-Only Audit Logs') @('Patti Fernandez') 'Members have read-only access to the Security & Compliance configuration.'
)

# ---------------------------------------------------------------- Assemble the snapshot
$collections = [ordered]@{}
function Add-Collection([string]$Key, $Items, [string]$State = 'Loaded', [string]$Message = $null, [double]$Seconds = 1.2) {
    $script:collections[$Key] = [ordered]@{
        command = $null; state = $State; retrievedUtc = (Dt 0 -1); durationSeconds = $Seconds; message = $Message; warnings = $null
        items = @($Items | Where-Object { $_ })
    }
}
Add-Collection 'InformationProtection.SensitivityLabels' $labels -Seconds 2.8
Add-Collection 'InformationProtection.LabelPolicies' $labelPolicies -Seconds 1.4
Add-Collection 'InformationProtection.AutoLabelingPolicies' $autoPolicies -Seconds 1.1
Add-Collection 'InformationProtection.AutoLabelingRules' $autoRules -Seconds 1.3
Add-Collection 'DataClassification.SensitiveInfoTypes' $sits -Seconds 4.6
Add-Collection 'DataClassification.RulePackages' $rulePackages -Seconds 2.2
Add-Collection 'DataClassification.KeywordDictionaries' $keywordDictionaries -Seconds 0.9
Add-Collection 'DataClassification.EdmSchemas' $edmSchemas -Seconds 0.8
Add-Collection 'DataLossPrevention.Policies' $dlpPolicies -Seconds 1.6
Add-Collection 'DataLossPrevention.Rules' $dlpRules -Seconds 2.1
Add-Collection 'DataLossPrevention.PolicyTips' $policyTips -Seconds 0.7
Add-Collection 'DataLossPrevention.GlobalSettings' $policyConfig -Seconds 0.9
Add-Collection 'DataLifecycleManagement.RetentionPolicies' $retentionPolicies -Seconds 1.9
Add-Collection 'DataLifecycleManagement.RetentionRules' $retentionRules -Seconds 1.5
Add-Collection 'DataLifecycleManagement.AppRetentionPolicies' $appPolicies -Seconds 1.0
Add-Collection 'DataLifecycleManagement.AppRetentionRules' $appRules -Seconds 0.9
Add-Collection 'DataLifecycleManagement.AdaptiveScopes' $scopes -Seconds 0.8
Add-Collection 'RecordsManagement.RetentionLabels' $tags -Seconds 1.2
Add-Collection 'RecordsManagement.EventTypes' $eventTypes -Seconds 0.6
Add-Collection 'RecordsManagement.Events' $events -Seconds 0.7
Add-Collection 'RecordsManagement.Departments' $departments -Seconds 0.5
Add-Collection 'RecordsManagement.Categories' $categories -Seconds 0.5
Add-Collection 'RecordsManagement.SubCategories' $subCategories -Seconds 0.5
Add-Collection 'RecordsManagement.Authorities' $authorities -Seconds 0.5
Add-Collection 'RecordsManagement.Citations' $citations -Seconds 0.5
Add-Collection 'RecordsManagement.ReferenceIds' $referenceIds -Seconds 0.5
Add-Collection 'eDiscovery.Cases' $cases -Seconds 1.1
Add-Collection 'eDiscovery.PremiumCases' $premiumCases -Seconds 1.3
Add-Collection 'eDiscovery.CaseHolds' $caseHolds -Seconds 3.4 -Message "Internal review 2026-07: The case 'Internal review 2026-07' is closed. Holds of closed cases are released."
Add-Collection 'eDiscovery.Searches' $searches -Seconds 2.5
Add-Collection 'eDiscovery.SecurityFilters' $securityFilters -Seconds 0.6
Add-Collection 'eDiscovery.CaseAdmins' @() -State 'Unavailable' -Seconds 0.2 -Message "Get-eDiscoveryCaseAdmin isn't available in this session. Your account might not have a Purview role that includes it, or the feature isn't available in this tenant."
Add-Collection 'Audit.RetentionPolicies' $auditPolicies -Seconds 0.9
Add-Collection 'Audit.Configuration' $auditConfig -Seconds 0.6
Add-Collection 'InsiderRiskManagement.Policies' $irmPolicies -Seconds 1.0
Add-Collection 'InsiderRiskManagement.Cases' $irmCases -Seconds 0.9
Add-Collection 'CommunicationCompliance.Policies' $ccPolicies -Seconds 1.2
Add-Collection 'CommunicationCompliance.Rules' $ccRules -Seconds 1.0
Add-Collection 'InformationBarriers.Policies' $ibPolicies -Seconds 0.8
Add-Collection 'InformationBarriers.Segments' $segments -Seconds 0.7
Add-Collection 'InformationBarriers.ApplicationStatus' $ibStatus -Seconds 0.6
Add-Collection 'Alerts.AlertPolicies' $alerts -Seconds 1.7
Add-Collection 'Permissions.RoleGroups' $roleGroups -Seconds 1.4

$snapshot = [ordered]@{
    format = 'PurviewExplorer.Snapshot'
    formatVersion = 1
    application = 'Purview Explorer (demo data generator)'
    createdUtc = (Dt 0)
    source = [ordered]@{
        kind = 'Demo'; organization = 'contoso.onmicrosoft.com'; account = 'meganb@contoso.onmicrosoft.com'
        tenantId = (G 'tenant/contoso'); environment = 'Microsoft 365 (Commercial / GCC)'; moduleVersion = '3.10.1'
    }
    collections = $collections
}
$json = $snapshot | ConvertTo-Json -Depth 30
$full = [System.IO.Path]::GetFullPath($OutFile)
[System.IO.File]::WriteAllText($full, ($json -replace "`r?`n", "`r`n") + "`r`n", [System.Text.UTF8Encoding]::new($false))
$count = ($collections.Values | ForEach-Object { $_.items.Count } | Measure-Object -Sum).Sum
Write-Host "Wrote $full ($($collections.Count) collections, $count items)"

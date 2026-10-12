// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SardaCloud.PurviewViewer.Catalog
{
    /// <summary>
    /// The Purview solutions and the read-only Security &amp; Compliance PowerShell cmdlets used to
    /// retrieve their configuration. Only Get-* cmdlets are ever invoked.
    /// </summary>
    public static class PurviewCatalog
    {
        private static readonly Lazy<IReadOnlyList<SolutionDef>> _solutions = new Lazy<IReadOnlyList<SolutionDef>>(Build);
        private static Dictionary<string, CollectionDef> _byKey;

        public static IReadOnlyList<SolutionDef> Solutions => _solutions.Value;

        public static IEnumerable<CollectionDef> AllCollections => Solutions.SelectMany(s => s.Collections);

        public static CollectionDef Find(string key)
        {
            var _ = Solutions;
            return key != null && _byKey.TryGetValue(key, out var def) ? def : null;
        }

        /// <summary>Distinct cmdlet names, used to limit what Connect-IPPSSession imports.</summary>
        public static IEnumerable<string> CmdletNames =>
            AllCollections.Select(c => c.Cmdlet).Distinct(StringComparer.OrdinalIgnoreCase);

        private static IReadOnlyList<SolutionDef> Build()
        {
            var list = new List<SolutionDef>
            {
                InformationProtection(),
                DataClassification(),
                DataLossPrevention(),
                DataLifecycleManagement(),
                RecordsManagement(),
                EDiscovery(),
                Audit(),
                InsiderRiskManagement(),
                CommunicationCompliance(),
                InformationBarriers(),
                AlertPolicies(),
                Permissions(),
            };

            _byKey = new Dictionary<string, CollectionDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in list)
            {
                foreach (var c in s.Collections)
                {
                    c.Solution = s;
                    c.Key = s.Key + "." + c.LocalKey;
                    _byKey.Add(c.Key, c);
                }
            }
            return list;
        }

        private static SolutionDef Solution(string key, string name, StockIcon icon, string description, string url, params CollectionDef[] collections)
        {
            var s = new SolutionDef { Key = key, Name = name, Icon = icon, Description = description, LearnMoreUrl = url };
            s.Collections.AddRange(collections);
            return s;
        }

        private static CollectionDef C(string localKey, string name, string cmdlet, params string[] columns) =>
            new CollectionDef { LocalKey = localKey, Name = name, Cmdlet = cmdlet, Columns = columns };

        private static readonly string[] PolicyChild = { "ParentPolicyName", "Policy" };

        private static SolutionDef InformationProtection() => Solution(
            "InformationProtection", "Information Protection", StockIcon.Lock,
            "Sensitivity labels, their publishing policies and auto-labeling policies.",
            "https://learn.microsoft.com/purview/information-protection",
            new CollectionDef
            {
                LocalKey = "SensitivityLabels", Name = "Sensitivity labels", Cmdlet = "Get-Label",
                Columns = new[] { "DisplayName", "Name", "Priority", "ContentType", "Mode", "Disabled", "Tooltip", "WhenChangedUTC" },
                SortProperty = "Priority",
                ParentProperty = "ParentId",
            }
                .Param("IncludeDetailedLabelActions")
                .Related("Sublabels", "InformationProtection.SensitivityLabels", new[] { "ParentId" }),
            C("LabelPolicies", "Label policies", "Get-LabelPolicy",
                "Name", "Priority", "Mode", "Enabled", "Labels", "DistributionStatus", "WhenChangedUTC")
                .Also(c => c.SortProperty = "Priority"),
            C("AutoLabelingPolicies", "Auto-labeling policies", "Get-AutoSensitivityLabelPolicy",
                "Name", "Priority", "Mode", "Enabled", "ApplySensitivityLabel", "Workload", "DistributionStatus", "WhenChangedUTC")
                .Also(c => c.SortProperty = "Priority")
                .Related("Rules", "InformationProtection.AutoLabelingRules", PolicyChild),
            C("AutoLabelingRules", "Auto-labeling rules", "Get-AutoSensitivityLabelRule",
                "Name", "ParentPolicyName", "Priority", "Mode", "Disabled", "Workload", "WhenChangedUTC"));

        private static SolutionDef DataClassification() => Solution(
            "DataClassification", "Data Classification", StockIcon.AutoList,
            "Sensitive information types, rule packages, keyword dictionaries and exact data match schemas.",
            "https://learn.microsoft.com/purview/data-classification-overview",
            C("SensitiveInfoTypes", "Sensitive information types", "Get-DlpSensitiveInformationType",
                "Name", "Publisher", "Type", "RecommendedConfidence", "Description", "Id"),
            C("RulePackages", "SIT rule packages", "Get-DlpSensitiveInformationTypeRulePackage",
                "RuleCollectionName", "LocalizedName", "Publisher", "Version", "Identity"),
            C("KeywordDictionaries", "Keyword dictionaries", "Get-DlpKeywordDictionary",
                "Name", "Description", "Identity", "WhenChangedUTC"),
            C("EdmSchemas", "Exact data match schemas", "Get-DlpEdmSchema",
                "Name", "DataStoreName", "Identity", "WhenChangedUTC"));

        private static SolutionDef DataLossPrevention() => Solution(
            "DataLossPrevention", "Data Loss Prevention", StockIcon.Shield,
            "DLP policies and rules, policy tips and global (endpoint) DLP settings.",
            "https://learn.microsoft.com/purview/dlp-learn-about-dlp",
            C("Policies", "DLP policies", "Get-DlpCompliancePolicy",
                "Name", "Priority", "Mode", "Enabled", "Workload", "Type", "DistributionStatus", "WhenChangedUTC")
                .Also(c => c.SortProperty = "Priority")
                .Related("Rules", "DataLossPrevention.Rules", PolicyChild),
            C("Rules", "DLP rules", "Get-DlpComplianceRule",
                "Name", "ParentPolicyName", "Priority", "Mode", "Disabled", "ReportSeverityLevel", "BlockAccess", "GenerateAlert", "WhenChangedUTC"),
            C("PolicyTips", "Custom policy tips", "Get-PolicyTipConfig",
                "Name", "Action", "Locale", "Value"),
            C("GlobalSettings", "Global and endpoint settings", "Get-PolicyConfig",
                "Identity", "Name", "WhenChangedUTC"));

        private static SolutionDef DataLifecycleManagement() => Solution(
            "DataLifecycleManagement", "Data Lifecycle Management", StockIcon.Recycler,
            "Retention policies and rules (including label publishing and auto-apply policies), app retention policies and adaptive scopes.",
            "https://learn.microsoft.com/purview/data-lifecycle-management",
            C("RetentionPolicies", "Retention policies", "Get-RetentionCompliancePolicy",
                "Name", "Enabled", "Mode", "Type", "RetentionRuleTypes", "Workload", "DistributionStatus", "WhenChangedUTC")
                .Param("RetentionRuleTypes")
                .Related("Rules", "DataLifecycleManagement.RetentionRules", PolicyChild),
            C("RetentionRules", "Retention rules", "Get-RetentionComplianceRule",
                "Name", "Policy", "RetentionDuration", "RetentionDurationDisplayHint", "RetentionComplianceAction", "ExpirationDateOption",
                "ApplyComplianceTag", "PublishComplianceTag", "Mode", "Disabled"),
            C("AppRetentionPolicies", "App retention policies", "Get-AppRetentionCompliancePolicy",
                "Name", "Applications", "Enabled", "Mode", "DistributionStatus", "WhenChangedUTC")
                .Related("Rules", "DataLifecycleManagement.AppRetentionRules", PolicyChild),
            C("AppRetentionRules", "App retention rules", "Get-AppRetentionComplianceRule",
                "Name", "Policy", "RetentionDuration", "RetentionComplianceAction", "ExpirationDateOption", "Mode", "Disabled"),
            C("AdaptiveScopes", "Adaptive scopes", "Get-AdaptiveScope",
                "Name", "LocationType", "FilterConditions", "RawQuery", "AdministrativeUnits", "WhenChangedUTC"));

        private static SolutionDef RecordsManagement() => Solution(
            "RecordsManagement", "Records Management", StockIcon.Stack,
            "Retention labels (file plan), event-based retention and file plan descriptors.",
            "https://learn.microsoft.com/purview/records-management",
            C("RetentionLabels", "Retention labels", "Get-ComplianceTag",
                "Name", "Priority", "RetentionAction", "RetentionDuration", "RetentionType", "IsRecordLabel", "Regulatory", "EventType", "Workload", "WhenChangedUTC"),
            C("EventTypes", "Event types", "Get-ComplianceRetentionEventType",
                "Name", "Comment", "WhenChangedUTC"),
            C("Events", "Events", "Get-ComplianceRetentionEvent",
                "Name", "EventType", "EventDateTime", "EventStatus", "SharePointAssetIdQuery", "ExchangeAssetIdQuery", "WhenChangedUTC"),
            C("Departments", "File plan: departments", "Get-FilePlanPropertyDepartment", "Name", "DisplayName", "WhenChangedUTC"),
            C("Categories", "File plan: categories", "Get-FilePlanPropertyCategory", "Name", "DisplayName", "WhenChangedUTC"),
            C("SubCategories", "File plan: subcategories", "Get-FilePlanPropertySubCategory", "Name", "DisplayName", "ParentId", "WhenChangedUTC"),
            C("Authorities", "File plan: authorities", "Get-FilePlanPropertyAuthority", "Name", "DisplayName", "WhenChangedUTC"),
            C("Citations", "File plan: citations", "Get-FilePlanPropertyCitation", "Name", "DisplayName", "CitationUrl", "CitationJurisdiction", "WhenChangedUTC"),
            C("ReferenceIds", "File plan: reference IDs", "Get-FilePlanPropertyReferenceId", "Name", "DisplayName", "WhenChangedUTC"));

        private static SolutionDef EDiscovery() => Solution(
            "eDiscovery", "eDiscovery", StockIcon.Find,
            "eDiscovery cases, case holds, content searches and compliance security filters.",
            "https://learn.microsoft.com/purview/edisc",
            C("Cases", "Cases (Standard)", "Get-ComplianceCase",
                "Name", "Status", "CaseType", "Description", "CreatedDateTime", "ClosedDateTime", "ClosedBy")
                .Related("Holds", "eDiscovery.CaseHolds", new[] { "Case", "CaseId" }),
            C("PremiumCases", "Cases (Premium)", "Get-ComplianceCase",
                "Name", "Status", "CaseType", "Description", "CreatedDateTime", "ClosedDateTime", "ClosedBy")
                .Param("CaseType", "AdvancedEdiscovery")
                .Related("Holds", "eDiscovery.CaseHolds", new[] { "Case", "CaseId" }),
            new CollectionDef
            {
                LocalKey = "CaseHolds", Name = "Case holds", Cmdlet = "Get-CaseHoldPolicy",
                Columns = new[] { "Case", "Name", "Enabled", "Mode", "DistributionStatus", "ExchangeLocation", "SharePointLocation", "WhenChangedUTC" },
                ForEachCollections = new[] { "eDiscovery.Cases", "eDiscovery.PremiumCases" },
                ForEachParameter = "Case",
                ForEachProperty = "Name",
                ForEachLabel = "Case",
            },
            C("Searches", "Content searches", "Get-ComplianceSearch",
                "Name", "Status", "ContentMatchQuery", "ExchangeLocation", "SharePointLocation", "CreatedBy", "JobEndTime"),
            C("SecurityFilters", "Compliance security filters", "Get-ComplianceSecurityFilter",
                "FilterName", "Action", "Users", "Filters", "Region"),
            C("CaseAdmins", "eDiscovery administrators", "Get-eDiscoveryCaseAdmin",
                "Name", "DisplayName", "PrimarySmtpAddress", "WindowsLiveID"));

        private static SolutionDef Audit() => Solution(
            "Audit", "Audit", StockIcon.DocAssoc,
            "Audit log retention policies and audit configuration.",
            "https://learn.microsoft.com/purview/audit-solutions-overview",
            C("RetentionPolicies", "Audit retention policies", "Get-UnifiedAuditLogRetentionPolicy",
                "Name", "Priority", "RetentionDuration", "RecordTypes", "Operations", "UserIds", "Enabled", "WhenChangedUTC")
                .Also(c => c.SortProperty = "Priority"),
            C("Configuration", "Audit configuration", "Get-AdminAuditLogConfig",
                "Name", "UnifiedAuditLogIngestionEnabled", "AdminAuditLogEnabled", "WhenChangedUTC"));

        private static SolutionDef InsiderRiskManagement() => Solution(
            "InsiderRiskManagement", "Insider Risk Management", StockIcon.Users,
            "Insider risk policies and cases.",
            "https://learn.microsoft.com/purview/insider-risk-management",
            C("Policies", "Insider risk policies", "Get-InsiderRiskPolicy",
                "Name", "InsiderRiskScenario", "Enabled", "Mode", "WhenChangedUTC")
                .Also(c =>
                {
                    c.Undocumented = true;
                    c.Notes = "Get-InsiderRiskPolicy isn't publicly documented; availability depends on your tenant and roles.";
                }),
            C("Cases", "Insider risk cases", "Get-ComplianceCase",
                "Name", "Status", "CaseType", "CreatedDateTime", "ClosedDateTime")
                .Param("CaseType", "InsiderRisk"));

        private static SolutionDef CommunicationCompliance() => Solution(
            "CommunicationCompliance", "Communication Compliance", StockIcon.Internet,
            "Communication compliance (supervisory review) policies and rules.",
            "https://learn.microsoft.com/purview/communication-compliance",
            C("Policies", "Policies", "Get-SupervisoryReviewPolicyV2",
                "Name", "Enabled", "Reviewers", "Comment", "WhenChangedUTC")
                .Related("Rules", "CommunicationCompliance.Rules", PolicyChild),
            C("Rules", "Rules", "Get-SupervisoryReviewRule",
                "Name", "Policy", "SamplingRate", "Condition", "Mode", "WhenChangedUTC"));

        private static SolutionDef InformationBarriers() => Solution(
            "InformationBarriers", "Information Barriers", StockIcon.NetworkConnect,
            "Organization segments, information barrier policies and policy application status.",
            "https://learn.microsoft.com/purview/information-barriers",
            C("Policies", "Policies", "Get-InformationBarrierPolicy",
                "Name", "State", "AssignedSegment", "SegmentsAllowed", "SegmentsBlocked", "ModerationAllowed", "WhenChanged"),
            C("Segments", "Segments", "Get-OrganizationSegment",
                "Name", "UserGroupFilter", "WhenChanged"),
            C("ApplicationStatus", "Application status", "Get-InformationBarrierPoliciesApplicationStatus",
                "Identity", "Status", "CreationTime", "LastModifiedTime", "ApplicationTotalCount", "ApplicationFailedCount"));

        private static SolutionDef AlertPolicies() => Solution(
            "Alerts", "Alert Policies", StockIcon.Warning,
            "Alert policies (protection alerts), including the built-in system policies.",
            "https://learn.microsoft.com/purview/alert-policies",
            C("AlertPolicies", "Alert policies", "Get-ProtectionAlert",
                "Name", "Category", "Severity", "Disabled", "IsSystemRule", "AggregationType", "NotifyUser", "WhenChangedUTC"));

        private static SolutionDef Permissions() => Solution(
            "Permissions", "Permissions", StockIcon.Key,
            "Microsoft Purview role groups, their roles and members.",
            "https://learn.microsoft.com/purview/purview-permissions",
            C("RoleGroups", "Role groups", "Get-RoleGroup",
                "DisplayName", "Name", "Members", "Roles", "Description", "WhenChanged"));

        private static CollectionDef Also(this CollectionDef c, Action<CollectionDef> configure)
        {
            configure(c);
            return c;
        }
    }
}

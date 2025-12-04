using System.Collections.Generic;
using System.Threading.Tasks;
using DevOpsMetrics.Core.Models.AzureDevOps;
using DevOpsMetrics.Core.Models.Common;
using DevOpsMetrics.Core.Models.GitHub;

namespace DevOpsMetrics.Web.Services
{
    public interface IServiceApiClient
    {
        Task<DeploymentFrequencyModel> GetAzureDevOpsDeploymentFrequency(bool getSampleData, string organization, string project, string repository, string branch, string buildName, string buildId, int numberOfDays, int maxNumberOfItems, bool useCache);
        Task<DeploymentFrequencyModel> GetGitHubDeploymentFrequency(bool getSampleData, string clientId, string clientSecret, string owner, string repo, string branch, string workflowName, string workflowId, int numberOfDays, int maxNumberOfItems, bool useCache);
        Task<LeadTimeForChangesModel> GetAzureDevOpsLeadTimeForChanges(bool getSampleData, string organization, string project, string repository, string branch, string buildName, string buildId, int numberOfDays, int maxNumberOfItems, bool useCache);
        Task<LeadTimeForChangesModel> GetGitHubLeadTimeForChanges(bool getSampleData, string clientId, string clientSecret, string owner, string repo, string branch, string workflowName, string workflowId, int numberOfDays, int maxNumberOfItems, bool useCache);
        Task<MeanTimeToRestoreModel> GetAzureMeanTimeToRestore(bool getSampleData, DevOpsPlatform targetDevOpsPlatform, string resourceGroup, int numberOfDays, int maxNumberOfItems);
        Task<ChangeFailureRateModel> GetChangeFailureRate(bool getSampleData, DevOpsPlatform targetDevOpsPlatform, string organization_owner, string project_repo, string branch, string buildName_workflowName, int numberOfDays, int maxNumberOfItems);
        Task<bool> UpdateChangeFailureRate(string organization_owner, string project_repo, string buildName_workflowName, int percentComplete, int numberOfDays);
        Task<List<AzureDevOpsSettings>> GetAzureDevOpsSettings();
        Task<List<GitHubSettings>> GetGitHubSettings();
        Task<bool> UpdateAzureDevOpsSetting(string patToken, string organization, string project, string repository, string branch, string buildName, string buildId, string resourceGroup, int itemOrder, bool showSetting);
        Task<bool> UpdateGitHubSetting(string clientId, string clientSecret, string owner, string repo, string branch, string workflowName, string workflowId, string resourceGroup, int itemOrder, bool showSetting);
        Task<List<ProjectLog>> GetAzureDevOpsProjectLogs(string organization, string project, string repository);
        Task<List<ProjectLog>> GetGitHubProjectLogs(string owner, string repo);
        Task<ProcessingResult> UpdateDORASummaryItem(string owner, string project, string repository, string branch, string workflowName, string workflowId, string resourceGroup, int numberOfDays, int maxNumberOfItems, bool isGitHub = true);
    }
}

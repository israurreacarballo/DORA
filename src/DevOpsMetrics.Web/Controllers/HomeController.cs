using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DevOpsMetrics.Core.DataAccess.TableStorage;
using DevOpsMetrics.Core.Models.AzureDevOps;
using DevOpsMetrics.Core.Models.Common;
using DevOpsMetrics.Core.Models.GitHub;
using DevOpsMetrics.Web.Models;
using DevOpsMetrics.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;

namespace DevOpsMetrics.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration Configuration;
        private readonly IServiceApiClient _serviceApiClient;

        public HomeController(IConfiguration configuration, IServiceApiClient serviceApiClient)
        {
            Configuration = configuration;
            _serviceApiClient = serviceApiClient;
        }

        private async Task<(List<AzureDevOpsSettings> azure, List<GitHubSettings> gh)> GetAllSettings()
        {
            List<AzureDevOpsSettings> azureDevOpsSettings = await _serviceApiClient.GetAzureDevOpsSettings();
            List<GitHubSettings> githubSettings = await _serviceApiClient.GetGitHubSettings();
            return (azureDevOpsSettings, githubSettings);
        }

        private SelectList BuildNumberOfDaysSelectList(IEnumerable<int> options)
        {
            var items = options.Select(d => new NumberOfDaysItem { NumberOfDays = d }).ToList();
            return new SelectList(items, "NumberOfDays", "NumberOfDays");
        }

        private SelectList BuildProjectSelectList(IEnumerable<AzureDevOpsSettings> azure, IEnumerable<GitHubSettings> gh)
        {
            List<KeyValuePair<string, string>> projects = new()
            {
                new("", "<Select project>")
            };

            projects.AddRange(azure.Select(a => new KeyValuePair<string, string>(PartitionKeys.CreateAzureDevOpsSettingsPartitionKey(a.Organization, a.Project, a.Repository), a.Project)));
            projects.AddRange(gh.Select(g => new KeyValuePair<string, string>(PartitionKeys.CreateGitHubSettingsPartitionKey(g.Owner, g.Repo), g.Repo)));

            return new SelectList(projects, "Key", "Value");
        }

        public async Task<IActionResult> Index(string projectId = null, string log = null)
        {
            //Get a list of settings
            var lists = await GetAllSettings();

            //Return the resultant list
            IndexViewModel result = new()
            {
                AzureDevOpsSettings = lists.azure,
                GitHubSettings = lists.gh,
                ProjectId = projectId,
                Log = log
            };
            return View(result);
        }

        [HttpGet("RefreshMetric")]
        public async Task<IActionResult> RefreshMetric(string projectId)
        {
            var lists = await GetAllSettings();
            var azureDevOpsSettings = lists.azure;
            var githubSettings = lists.gh;

            var found = await TryRefreshProject(projectId, azureDevOpsSettings, githubSettings);
            return RedirectToAction("Index", "Home", new { projectId = projectId, log = found });
        }

        private async Task<string> TryRefreshProject(string projectId, List<AzureDevOpsSettings> azureDevOpsSettings, List<GitHubSettings> githubSettings)
        {
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                if (item.RowKey == projectId)
                {
                    DateTime startTime = DateTime.Now;
                    await _serviceApiClient.UpdateDORASummaryItem(item.Organization,
                            item.Project, item.Repository, item.Branch,
                            item.BuildName, item.BuildId,
                            item.ProductionResourceGroup,
                            30, 20, false);
                    DateTime endTime = DateTime.Now;
                    return $"Successfully refreshed {item.Organization} {item.Project} {item.Repository} in {(endTime - startTime).TotalSeconds} seconds";
                }
            }

            foreach (GitHubSettings item in githubSettings)
            {
                if (item.RowKey == projectId)
                {
                    DateTime startTime = DateTime.Now;
                    await _serviceApiClient.UpdateDORASummaryItem(item.Owner,
                        "", item.Repo, item.Branch,
                        item.WorkflowName, item.WorkflowId,
                        item.ProductionResourceGroup,
                        30, 20, true);
                    DateTime endTime = DateTime.Now;
                    return $"Successfully refreshed {item.Owner} {item.Repo} in {(endTime - startTime).TotalSeconds} seconds";
                }
            }

            return "Project not found";
        }

        [HttpPost]
        public IActionResult ProjectUpdate(string RowKey, int NumberOfDaysSelected = 30)
        {
            return RedirectToAction("Project", "Home", new { projectId = RowKey, numberOfDays = NumberOfDaysSelected });
        }

        [HttpGet]
        public async Task<IActionResult> Project(string projectId, int numberOfDays = 30)
        {
            int maxNumberOfItems = 20;
            bool getSampleData = false;
            bool useCache = true;
            string clientId = Configuration["AppSettings:GitHubClientId"];
            string clientSecret = Configuration["AppSettings:GitHubClientSecret"];
            IsraProjectViewModel model = null;

            //Find the right project to load
            var lists = await GetAllSettings();
            var azureDevOpsSettings = lists.azure;
            var githubSettings = lists.gh;

            //Create the days to view dropdown
            var numberOfDaysList = BuildNumberOfDaysSelectList(new[] { 7, 14, 21, 30, 60, 90 });

            model = await BuildProjectViewModelForAzure(projectId, azureDevOpsSettings, numberOfDays, maxNumberOfItems, getSampleData, useCache, numberOfDaysList);
            if (model == null)
            {
                model = await BuildProjectViewModelForGitHub(projectId, githubSettings, clientId, clientSecret, numberOfDays, maxNumberOfItems, getSampleData, useCache, numberOfDaysList);
            }

            return View(model);
        }

        private async Task<IsraProjectViewModel> BuildProjectViewModelForAzure(string projectId, List<AzureDevOpsSettings> azureDevOpsSettings, int numberOfDays, int maxNumberOfItems, bool getSampleData, bool useCache, SelectList numberOfDaysList)
        {
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                if (item.RowKey == projectId)
                {
                    var deploymentFrequencyModel = await _serviceApiClient.GetAzureDevOpsDeploymentFrequency(getSampleData,
                        item.Organization, item.Project, item.Repository, item.Branch, item.BuildName, item.BuildId,
                        numberOfDays, maxNumberOfItems, useCache);
                    var leadTimeForChangesModel = await _serviceApiClient.GetAzureDevOpsLeadTimeForChanges(getSampleData,
                        item.Organization, item.Project, item.Repository, item.Branch, item.BuildName, item.BuildId,
                        numberOfDays, maxNumberOfItems, useCache);
                    var meanTimeToRestoreModel = await _serviceApiClient.GetAzureMeanTimeToRestore(getSampleData,
                       DevOpsPlatform.AzureDevOps, item.ProductionResourceGroup, numberOfDays, maxNumberOfItems);
                    var changeFailureRateModel = await _serviceApiClient.GetChangeFailureRate(getSampleData,
                        DevOpsPlatform.AzureDevOps, item.Organization, item.Project, item.Branch, item.BuildName,
                        numberOfDays, maxNumberOfItems);
                    deploymentFrequencyModel.IsProjectView = true;
                    leadTimeForChangesModel.IsProjectView = true;
                    meanTimeToRestoreModel.IsProjectView = true;
                    changeFailureRateModel.IsProjectView = true;
                    return new IsraProjectViewModel
                    {
                        RowKey = item.RowKey,
                        ProjectName = item.Project,
                        TargetDevOpsPlatform = DevOpsPlatform.AzureDevOps,
                        DeploymentFrequency = deploymentFrequencyModel,
                        LeadTimeForChanges = leadTimeForChangesModel,
                        MeanTimeToRestore = meanTimeToRestoreModel,
                        ChangeFailureRate = changeFailureRateModel,
                        NumberOfDays = numberOfDaysList,
                        NumberOfDaysSelected = numberOfDays
                    };
                }
            }
            return null;
        }

        private async Task<IsraProjectViewModel> BuildProjectViewModelForGitHub(string projectId, List<GitHubSettings> githubSettings, string clientId, string clientSecret, int numberOfDays, int maxNumberOfItems, bool getSampleData, bool useCache, SelectList numberOfDaysList)
        {
            foreach (GitHubSettings item in githubSettings)
            {
                if (item.RowKey == projectId)
                {
                    var deploymentFrequencyModel = await _serviceApiClient.GetGitHubDeploymentFrequency(getSampleData, clientId, clientSecret,
                        item.Owner, item.Repo, item.Branch, item.WorkflowName, item.WorkflowId,
                        numberOfDays, maxNumberOfItems, useCache);
                    var leadTimeForChangesModel = await _serviceApiClient.GetGitHubLeadTimeForChanges(getSampleData, clientId, clientSecret,
                        item.Owner, item.Repo, item.Branch, item.WorkflowName, item.WorkflowId,
                        numberOfDays, maxNumberOfItems, useCache);
                    var meanTimeToRestoreModel = await _serviceApiClient.GetAzureMeanTimeToRestore(getSampleData,
                        DevOpsPlatform.GitHub, item.ProductionResourceGroup, numberOfDays, maxNumberOfItems);
                    var changeFailureRateModel = await _serviceApiClient.GetChangeFailureRate(getSampleData,
                        DevOpsPlatform.GitHub, item.Owner, item.Repo, item.Branch, item.WorkflowName,
                        numberOfDays, maxNumberOfItems);
                    deploymentFrequencyModel.IsProjectView = true;
                    leadTimeForChangesModel.IsProjectView = true;
                    meanTimeToRestoreModel.IsProjectView = true;
                    changeFailureRateModel.IsProjectView = true;
                    return new IsraProjectViewModel
                    {
                        RowKey = item.RowKey,
                        ProjectName = item.Repo,
                        TargetDevOpsPlatform = DevOpsPlatform.GitHub,
                        DeploymentFrequency = deploymentFrequencyModel,
                        LeadTimeForChanges = leadTimeForChangesModel,
                        MeanTimeToRestore = meanTimeToRestoreModel,
                        ChangeFailureRate = changeFailureRateModel,
                        NumberOfDays = numberOfDaysList,
                        NumberOfDaysSelected = numberOfDays
                    };
                }
            }
            return null;
        }

        public async Task<IActionResult> DeploymentFrequency()
        {
            int maxNumberOfItems = 20; //20 is the optimium max that looks good with the current UI            
            int numberOfDays = 60; //TODO: Move number of days variable to a drop down list on the current UI 
            bool getSampleData = false;
            bool useCache = true; //Use Azure storage instead of hitting the API. Quicker, but data may be up to 4 hours out of date
            string clientId = Configuration["AppSettings:GitHubClientId"];
            string clientSecret = Configuration["AppSettings:GitHubClientSecret"];

            List<DeploymentFrequencyModel> items = new();

            //Get a list of settings
            var lists = await GetAllSettings();
            var azureDevOpsSettings = lists.azure;
            var githubSettings = lists.gh;

            //Create deployment frequency models from each Azure DevOps settings object
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                DeploymentFrequencyModel newDeploymentFrequencyModel = await _serviceApiClient.GetAzureDevOpsDeploymentFrequency(getSampleData,
                           item.Organization, item.Project, item.Repository, item.Branch, item.BuildName, item.BuildId,
                           numberOfDays, maxNumberOfItems, useCache);
                newDeploymentFrequencyModel.ItemOrder = item.ItemOrder;
                if (newDeploymentFrequencyModel != null)
                {
                    items.Add(newDeploymentFrequencyModel);
                }
            }

            //Create deployment frequency models from each GitHub settings object
            foreach (GitHubSettings item in githubSettings)
            {
                DeploymentFrequencyModel newDeploymentFrequencyModel = await _serviceApiClient.GetGitHubDeploymentFrequency(getSampleData, clientId, clientSecret,
                        item.Owner, item.Repo, item.Branch, item.WorkflowName, item.WorkflowId,
                        numberOfDays, maxNumberOfItems, useCache);
                newDeploymentFrequencyModel.ItemOrder = item.ItemOrder;
                if (newDeploymentFrequencyModel != null)
                {
                    items.Add(newDeploymentFrequencyModel);
                }
            }

            //Create the days to view dropdown
            var numberOfDaysList = BuildNumberOfDaysSelectList(new[] { 7, 14, 21, 30, 60, 90 });

            //sort the final list
            items = items.OrderBy(o => o.ItemOrder).ToList();
            return View(items);
        }

        public async Task<IActionResult> LeadTimeForChanges()
        {
            int maxNumberOfItems = 20; //20 is the optimium max that looks good with the current UI            
            int numberOfDays = 30; //TODO: Move number of days variable to a drop down list on the current UI 
            bool getSampleData = false;
            bool useCache = true; //Use Azure storage instead of hitting the API. Quicker, but data may be up to 4 hours out of date
            string clientId = Configuration["AppSettings:GitHubClientId"];
            string clientSecret = Configuration["AppSettings:GitHubClientSecret"];
            List<LeadTimeForChangesModel> items = new();

            //Get a list of settings
            var lists = await GetAllSettings();
            var azureDevOpsSettings = lists.azure;
            var githubSettings = lists.gh;

            //Create lead time for changes models from each Azure DevOps setting object
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                LeadTimeForChangesModel newLeadTimeForChangesModel = await _serviceApiClient.GetAzureDevOpsLeadTimeForChanges(getSampleData,
                        item.Organization, item.Project, item.Repository, item.Branch, item.BuildName, item.BuildId,
                        numberOfDays, maxNumberOfItems, useCache);
                newLeadTimeForChangesModel.ItemOrder = item.ItemOrder;
                if (newLeadTimeForChangesModel != null)
                {
                    items.Add(newLeadTimeForChangesModel);
                }
            }

            //Create lead time for changes models from each GitHub settings object
            foreach (GitHubSettings item in githubSettings)
            {
                LeadTimeForChangesModel newLeadTimeForChangesModel = await _serviceApiClient.GetGitHubLeadTimeForChanges(getSampleData, clientId, clientSecret,
                        item.Owner, item.Repo, item.Branch, item.WorkflowName, item.WorkflowId,
                        numberOfDays, maxNumberOfItems, useCache);
                newLeadTimeForChangesModel.ItemOrder = item.ItemOrder;
                if (newLeadTimeForChangesModel != null)
                {
                    items.Add(newLeadTimeForChangesModel);
                }
            }

            //sort the final list
            items = items.OrderBy(o => o.ItemOrder).ToList();
            return View(items);
        }

        public async Task<IActionResult> MeanTimeToRestore()
        {
            int maxNumberOfItems = 20; //20 is the optimium max that looks good with the current UI            
            int numberOfDays = 30; //TODO: Move number of days variable to a drop down list on the current UI 
            bool getSampleData = false;
            List<MeanTimeToRestoreModel> items = new();

            //Get a list of settings
            var lists = await GetAllSettings();
            var azureDevOpsSettings = lists.azure;
            var githubSettings = lists.gh;

            //Create MTTR models from each Azure DevOps settings object
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                MeanTimeToRestoreModel newMeanTimeToRestoreModel = await _serviceApiClient.GetAzureMeanTimeToRestore(getSampleData,
                        DevOpsPlatform.AzureDevOps, item.ProductionResourceGroup, numberOfDays, maxNumberOfItems);
                newMeanTimeToRestoreModel.ItemOrder = item.ItemOrder;
                if (newMeanTimeToRestoreModel != null)
                {
                    items.Add(newMeanTimeToRestoreModel);
                }
            }

            //Create MTTR models from each GitHub settings object
            foreach (GitHubSettings item in githubSettings)
            {
                MeanTimeToRestoreModel newMeanTimeToRestoreModel = await _serviceApiClient.GetAzureMeanTimeToRestore(getSampleData,
                        DevOpsPlatform.GitHub, item.ProductionResourceGroup, numberOfDays, maxNumberOfItems);
                newMeanTimeToRestoreModel.ItemOrder = item.ItemOrder;
                if (newMeanTimeToRestoreModel != null)
                {
                    items.Add(newMeanTimeToRestoreModel);
                }
            }

            //sort the final list
            items = items.OrderBy(o => o.ItemOrder).ToList();
            return View(items);
        }

        public async Task<IActionResult> ChangeFailureRate()
        {
            int maxNumberOfItems = 20; //20 is the optimium max that looks good with the current UI            
            int numberOfDays = 30; //TODO: Move number of days variable to a drop down list on the current UI 
            bool getSampleData = false;
            List<ChangeFailureRateModel> items = new();

            //Get a list of settings
            var lists = await GetAllSettings();
            var azureDevOpsSettings = lists.azure;
            var githubSettings = lists.gh;

            //Create change failure rate models from each Azure DevOps settings object
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                ChangeFailureRateModel changeFailureRateModel = await _serviceApiClient.GetChangeFailureRate(getSampleData,
                        DevOpsPlatform.AzureDevOps, item.Organization, item.Project, item.Branch, item.BuildName,
                        numberOfDays, maxNumberOfItems);
                //changeFailureRateModel.ItemOrder = item.ItemOrder;
                if (changeFailureRateModel != null)
                {
                    items.Add(changeFailureRateModel);
                }
            }

            //Create change failure rate models from each GitHub settings object
            foreach (GitHubSettings item in githubSettings)
            {
                ChangeFailureRateModel changeFailureRateModel = await _serviceApiClient.GetChangeFailureRate(getSampleData,
                        DevOpsPlatform.GitHub, item.Owner, item.Repo, item.Branch, item.WorkflowName,
                        numberOfDays, maxNumberOfItems);
                //changeFailureRateModel.ItemOrder = item.ItemOrder;
                if (changeFailureRateModel != null)
                {
                    items.Add(changeFailureRateModel);
                }
            }

            //sort the final list
            //items = items.OrderBy(o => o.ItemOrder).ToList();
            return View(items);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public async Task<IActionResult> ChangeFailureRateUpdates()
        {
            List<ProjectUpdateItem> projectList = new();

            //Get a list of settings
            var lists = await GetAllSettings();
            var azureDevOpsSettings = lists.azure;
            var githubSettings = lists.gh;

            //Create project items from each Azure DevOps setting and add it to a project list.
            projectList.AddRange(azureDevOpsSettings.Select(item => new ProjectUpdateItem { ProjectId = item.RowKey, ProjectName = item.Project }));
            projectList.AddRange(githubSettings.Select(item => new ProjectUpdateItem { ProjectId = item.RowKey, ProjectName = item.Repo }));

            //Create a percentage completed dropdown
            List<CompletionPercentItem> completionList = new()
            {
                new CompletionPercentItem { CompletionPercent = 0 },
                new CompletionPercentItem { CompletionPercent = 10 },
                new CompletionPercentItem { CompletionPercent = 25 },
                new CompletionPercentItem { CompletionPercent = 50 },
                new CompletionPercentItem { CompletionPercent = 75 },
                new CompletionPercentItem { CompletionPercent = 98 },
                new CompletionPercentItem { CompletionPercent = 100 }
            };

            //Create the days to process dropdown
            var numberOfDaysList = BuildNumberOfDaysSelectList(new[] { 1, 7, 21, 30, 60, 90 });

            ProjectUpdateViewModel model = new()
            {
                ProjectList = new SelectList(projectList, "ProjectId", "ProjectName"),
                CompletionPercentList = new SelectList(completionList, "CompletionPercent", "CompletionPercent"),
                NumberOfDaysList = numberOfDaysList
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateChangeFailureRate(string ProjectIdSelected, int CompletionPercentSelected, int NumberOfDaysSelected)
        {
            var lists = await GetAllSettings();
            var azureDevOpsSettings = lists.azure;
            var githubSettings = lists.gh;

            DevOpsPlatform targetDevOpsPlatform = DevOpsPlatform.UnknownDevOpsPlatform;
            string organization_owner = "";
            string project_repo = "";
            string repository = "";
            string buildName_workflowName = "";

            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                if (item.RowKey == ProjectIdSelected)
                {
                    targetDevOpsPlatform = DevOpsPlatform.AzureDevOps;
                    organization_owner = item.Organization;
                    project_repo = item.Project;
                    repository = item.Repository;
                    buildName_workflowName = item.BuildName;
                    break;
                }
            }

            if (targetDevOpsPlatform == DevOpsPlatform.UnknownDevOpsPlatform)
            {
                foreach (GitHubSettings item in githubSettings)
                {
                    if (item.RowKey == ProjectIdSelected)
                    {
                        targetDevOpsPlatform = DevOpsPlatform.GitHub;
                        organization_owner = item.Owner;
                        project_repo = item.Repo;
                        repository = "";
                        buildName_workflowName = item.WorkflowName;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(organization_owner) && !string.IsNullOrEmpty(project_repo) && !string.IsNullOrEmpty(buildName_workflowName))
            {
                await _serviceApiClient.UpdateChangeFailureRate(organization_owner, project_repo, buildName_workflowName, CompletionPercentSelected, NumberOfDaysSelected);
            }

            if (targetDevOpsPlatform == DevOpsPlatform.AzureDevOps)
            {
                return RedirectToAction("Project", "Home", new { projectId = organization_owner + "_" + project_repo + "_" + repository });
            }
            else if (targetDevOpsPlatform == DevOpsPlatform.GitHub)
            {
                return RedirectToAction("Project", "Home", new { projectId = organization_owner + "_" + project_repo });
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        public IActionResult LogsUpdate(string projectId)
        {
            return RedirectToAction("Logs", "Home", routeValues: new { projectId = projectId });
        }

        public async Task<IActionResult> Logs(string projectId = null)
        {
            var lists = await GetAllSettings();
            var azureDevOpsSettings = lists.azure;
            var githubSettings = lists.gh;

            var projectsSelect = BuildProjectSelectList(azureDevOpsSettings, githubSettings);

            List<ProjectLog> logs = new();
            if (!string.IsNullOrEmpty(projectId))
            {
                var parts = projectId.Split("_");
                if (parts.Length == 3)
                {
                    logs = await _serviceApiClient.GetAzureDevOpsProjectLogs(parts[0], parts[1], parts[2]);
                }
                else if (parts.Length >= 2)
                {
                    logs = await _serviceApiClient.GetGitHubProjectLogs(parts[0], parts[1]);
                }
            }

            //Flip the logs/ reverse the list of log items
            logs.Reverse();

            ProjectLogViewModel logViewModel = new()
            {
                ProjectId = projectId,
                Logs = logs,
                Projects = projectsSelect
            };
            return View(logViewModel);
        }

        public async Task<IActionResult> Settings()
        {
            //Find the right project to load
            var lists = await GetAllSettings();
            List<AzureDevOpsSettings> azureDevOpsSettings = lists.azure;
            List<GitHubSettings> githubSettings = lists.gh;

            (List<AzureDevOpsSettings>, List<GitHubSettings>) result = (azureDevOpsSettings, githubSettings);
            return View(result);
        }

        [HttpGet]
        public IActionResult AddAzureDevOpsSetting()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAzureDevOpsSetting(string patToken,
                string organization, string project, string repository,
                string branch, string buildName, string buildId, string resourceGroup,
                int itemOrder, bool showSetting)
        {
            if (!string.IsNullOrEmpty(patToken))
            {
                await _serviceApiClient.UpdateAzureDevOpsSetting(patToken,
                    organization, project, repository,
                    branch, buildName, buildId, resourceGroup,
                    itemOrder, showSetting);
            }

            return RedirectToAction("Settings", "Home");
        }

        [HttpGet]
        public IActionResult AddGitHubSetting()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateGitHubSetting(string clientId, string clientSecret,
            string owner, string repo,
            string branch, string workflowName, string workflowId, string resourceGroup,
            int itemOrder, bool showSetting)
        {
            await _serviceApiClient.UpdateGitHubSetting(clientId, clientSecret,
                owner, repo,
                branch, workflowName, workflowId, resourceGroup,
                itemOrder, showSetting);

            return RedirectToAction("Settings", "Home");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

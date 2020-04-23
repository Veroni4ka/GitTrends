﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using GitTrends.Mobile.Shared;
using GitTrends.Shared;
using Xamarin.Forms;

namespace GitTrends
{
    class TrendsViewModel : BaseViewModel
    {
        readonly GitHubApiV3Service _gitHubApiV3Service;
        readonly ReviewService _reviewService;

        bool _isFetchingData = true;
        bool _isChartEmpty = true;
        bool _isViewsSeriesVisible, _isUniqueViewsSeriesVisible, _isClonesSeriesVisible, _isUniqueClonesSeriesVisible;

        string _viewsStatisticsText = string.Empty;
        string _uniqueViewsStatisticsText = string.Empty;
        string _clonesStatisticsText = string.Empty;
        string _uniqueClonesStatisticsText = string.Empty;

        List<DailyViewsModel> _dailyViewsList = new List<DailyViewsModel>();
        List<DailyClonesModel> _dailyClonesList = new List<DailyClonesModel>();

        public TrendsViewModel(GitHubApiV3Service gitHubApiV3Service,
                                AnalyticsService analyticsService,
                                TrendsChartSettingsService trendsChartSettingsService,
                                ReviewService reviewService) : base(analyticsService)
        {
            _reviewService = reviewService;
            _gitHubApiV3Service = gitHubApiV3Service;

            IsViewsSeriesVisible = trendsChartSettingsService.ShouldShowViewsByDefault;
            IsUniqueViewsSeriesVisible = trendsChartSettingsService.ShouldShowUniqueViewsByDefault;
            IsClonesSeriesVisible = trendsChartSettingsService.ShouldShowClonesByDefault;
            IsUniqueClonesSeriesVisible = trendsChartSettingsService.ShouldShowUniqueClonesByDefault;

            ViewsCardTappedCommand = new Command(() => IsViewsSeriesVisible = !IsViewsSeriesVisible);
            UniqueViewsCardTappedCommand = new Command(() => IsUniqueViewsSeriesVisible = !IsUniqueViewsSeriesVisible);
            ClonesCardTappedCommand = new Command(() => IsClonesSeriesVisible = !IsClonesSeriesVisible);
            UniqueClonesCardTappedCommand = new Command(() => IsUniqueClonesSeriesVisible = !IsUniqueClonesSeriesVisible);

            FetchDataCommand = new AsyncCommand<Repository>(repo => ExecuteFetchDataCommand(repo));
        }

        public ICommand ViewsCardTappedCommand { get; }
        public ICommand UniqueViewsCardTappedCommand { get; }
        public ICommand ClonesCardTappedCommand { get; }
        public ICommand UniqueClonesCardTappedCommand { get; }

        public ICommand FetchDataCommand { get; }

        public double DailyViewsClonesMinValue { get; } = 0;

        public bool AreStatisticsVisible => !IsFetchingData;
        public bool IsChartVisible => !IsFetchingData && !IsChartEmpty;
        public bool IsEmptyStateVisible => !IsFetchingData && IsChartEmpty;
        public DateTime MinDateValue => DateTimeService.GetMinimumLocalDateTime(DailyViewsList, DailyClonesList);
        public DateTime MaxDateValue => DateTimeService.GetMaximumLocalDateTime(DailyViewsList, DailyClonesList);

        public double DailyViewsClonesMaxValue
        {
            get
            {
                const int minimumValue = 20;

                var dailyViewMaxValue = DailyViewsList.Any() ? DailyViewsList.Max(x => x.TotalViews) : 0;
                var dailyClonesMaxValue = DailyClonesList.Any() ? DailyClonesList.Max(x => x.TotalClones) : 0;

                return Math.Max(Math.Max(dailyViewMaxValue, dailyClonesMaxValue), minimumValue);
            }
        }

        public bool IsChartEmpty
        {
            get => _isChartEmpty;
            set => SetProperty(ref _isChartEmpty, value, () =>
            {
                OnPropertyChanged(nameof(IsEmptyStateVisible));
                OnPropertyChanged(nameof(IsChartVisible));
            });
        }

        public string ViewsStatisticsText
        {
            get => _viewsStatisticsText;
            set => SetProperty(ref _viewsStatisticsText, value);
        }

        public string UniqueViewsStatisticsText
        {
            get => _uniqueViewsStatisticsText;
            set => SetProperty(ref _uniqueViewsStatisticsText, value);
        }

        public string ClonesStatisticsText
        {
            get => _clonesStatisticsText;
            set => SetProperty(ref _clonesStatisticsText, value);
        }

        public string UniqueClonesStatisticsText
        {
            get => _uniqueClonesStatisticsText;
            set => SetProperty(ref _uniqueClonesStatisticsText, value);
        }

        public bool IsViewsSeriesVisible
        {
            get => _isViewsSeriesVisible;
            set => SetProperty(ref _isViewsSeriesVisible, value);
        }

        public bool IsUniqueViewsSeriesVisible
        {
            get => _isUniqueViewsSeriesVisible;
            set => SetProperty(ref _isUniqueViewsSeriesVisible, value);
        }

        public bool IsClonesSeriesVisible
        {
            get => _isClonesSeriesVisible;
            set => SetProperty(ref _isClonesSeriesVisible, value);
        }

        public bool IsUniqueClonesSeriesVisible
        {
            get => _isUniqueClonesSeriesVisible;
            set => SetProperty(ref _isUniqueClonesSeriesVisible, value);
        }

        public bool IsFetchingData
        {
            get => _isFetchingData;
            set => SetProperty(ref _isFetchingData, value, () =>
            {
                OnPropertyChanged(nameof(AreStatisticsVisible));
                OnPropertyChanged(nameof(IsChartVisible));
                OnPropertyChanged(nameof(IsEmptyStateVisible));
            });
        }

        public List<DailyViewsModel> DailyViewsList
        {
            get => _dailyViewsList;
            set => SetProperty(ref _dailyViewsList, value, UpdateDailyViewsListPropertiesChanged);
        }

        public List<DailyClonesModel> DailyClonesList
        {
            get => _dailyClonesList;
            set => SetProperty(ref _dailyClonesList, value, UpdateDailyClonesListPropertiesChanged);
        }

        async Task ExecuteFetchDataCommand(Repository repository)
        {
            _reviewService.TryRequestReview();

            IReadOnlyList<DailyViewsModel> repositoryViews;
            IReadOnlyList<DailyClonesModel> repositoryClones;

            var minimumTimeTask = Task.Delay(2000);

            try
            {
                if (repository.DailyClonesList.Any() && repository.DailyViewsList.Any())
                {
                    repositoryViews = repository.DailyViewsList;
                    repositoryClones = repository.DailyClonesList;

                    await minimumTimeTask.ConfigureAwait(false);
                }
                else
                {
                    IsFetchingData = true;

                    var getRepositoryViewStatisticsTask = _gitHubApiV3Service.GetRepositoryViewStatistics(repository.OwnerLogin, repository.Name);
                    var getRepositoryCloneStatisticsTask = _gitHubApiV3Service.GetRepositoryCloneStatistics(repository.OwnerLogin, repository.Name);

                    await Task.WhenAll(getRepositoryViewStatisticsTask, getRepositoryCloneStatisticsTask).ConfigureAwait(false);

                    var repositoryViewsResponse = await getRepositoryViewStatisticsTask.ConfigureAwait(false);
                    var repositoryClonesResponse = await getRepositoryCloneStatisticsTask.ConfigureAwait(false);

                    repositoryViews = repositoryViewsResponse.DailyViewsList;
                    repositoryClones = repositoryClonesResponse.DailyClonesList;
                }
            }
            catch (Exception e)
            {
                //ToDo Add note reporting to the user that the statistics are unavailable due to internet connectivity
                repositoryViews = Enumerable.Empty<DailyViewsModel>().ToList();
                repositoryClones = Enumerable.Empty<DailyClonesModel>().ToList();

                AnalyticsService.Report(e);
            }
            finally
            {
                //Display the Activity Indicator for a minimum time to ensure consistant UX 
                await minimumTimeTask.ConfigureAwait(false);
                IsFetchingData = false;
            }

            DailyViewsList = repositoryViews.OrderBy(x => x.Day).ToList();
            DailyClonesList = repositoryClones.OrderBy(x => x.Day).ToList();

            var viewsTotal = repositoryViews.Sum(x => x.TotalViews);
            var uniqueViewsTotal = repositoryViews.Sum(x => x.TotalUniqueViews);
            var clonesTotal = repositoryClones.Sum(x => x.TotalClones);
            var uniqueClonesTotal = repositoryClones.Sum(x => x.TotalUniqueClones);

            ViewsStatisticsText = repositoryViews.Sum(x => x.TotalViews).ConvertToAbbreviatedText();
            UniqueViewsStatisticsText = repositoryViews.Sum(x => x.TotalUniqueViews).ConvertToAbbreviatedText();

            ClonesStatisticsText = repositoryClones.Sum(x => x.TotalClones).ConvertToAbbreviatedText();
            UniqueClonesStatisticsText = repositoryClones.Sum(x => x.TotalUniqueClones).ConvertToAbbreviatedText();

            //Validate that there are insights to plot in the chart
            var sum = (viewsTotal + uniqueViewsTotal + clonesTotal + uniqueClonesTotal);
            IsChartEmpty = sum < 1;

            Debug.WriteLine($"Chart is Empty? {IsChartEmpty}, Is Fetching Data {IsFetchingData} Sum: {sum}");

            PrintDays();
        }

        void UpdateDailyClonesListPropertiesChanged()
        {
            OnPropertyChanged(nameof(DailyViewsClonesMaxValue));
            OnPropertyChanged(nameof(DailyViewsClonesMinValue));
            OnPropertyChanged(nameof(MinDateValue));
            OnPropertyChanged(nameof(MaxDateValue));
        }

        void UpdateDailyViewsListPropertiesChanged()
        {
            OnPropertyChanged(nameof(DailyViewsClonesMaxValue));
            OnPropertyChanged(nameof(DailyViewsClonesMaxValue));
            OnPropertyChanged(nameof(MinDateValue));
            OnPropertyChanged(nameof(MaxDateValue));
        }

        [Conditional("DEBUG")]
        void PrintDays()
        {
            Debug.WriteLine("Clones");
            foreach (var cloneDay in DailyClonesList.Select(x => x.Day))
                Debug.WriteLine(cloneDay);

            Debug.WriteLine("");

            Debug.WriteLine("Views");
            foreach (var viewDay in DailyViewsList.Select(x => x.Day))
                Debug.WriteLine(viewDay);
        }
    }
}

﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GitTrends.Mobile.Shared;
using GitTrends.Shared;
using GitTrends.Views.Base;
using Xamarin.Forms;

namespace GitTrends
{
    class ReferringSitesPage : BaseContentPage<ReferringSitesViewModel>
    {
        readonly RefreshView _refreshView;
        readonly DeepLinkingService _deepLinkingService;

        public ReferringSitesPage(DeepLinkingService deepLinkingService,
                                    ReferringSitesViewModel referringSitesViewModel,
                                    Repository repository,
                                    AnalyticsService analyticsService) : base(referringSitesViewModel, analyticsService, PageTitles.ReferringSitesPage)
        {
            const int titleRowHeight = 50;
            const int titleTopMargin = 15;
            _deepLinkingService = deepLinkingService;

            var collectionView = new CollectionView
            {
                AutomationId = ReferringSitesPageAutomationIds.CollectionView,
                BackgroundColor = Color.Transparent,
                ItemTemplate = new ReferringSitesDataTemplate(),
                SelectionMode = SelectionMode.Single,
                ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical),
                //Set iOS Header to `new BoxView { HeightRequest = titleRowHeight + titleTopMargin }` following this bug fix: https://github.com/xamarin/Xamarin.Forms/issues/9879
                Header = Device.RuntimePlatform is Device.Android ? new BoxView { HeightRequest = 8 } : null,
                Footer = Device.RuntimePlatform is Device.Android ? new BoxView { HeightRequest = 8 } : null,
                EmptyView = new ListEmptyState("EmptyReferringSitesList", 250, 250, "Your referring sites list is\nempty.")
            };
            collectionView.SelectionChanged += HandleCollectionViewSelectionChanged;
            collectionView.SetBinding(CollectionView.ItemsSourceProperty, nameof(ReferringSitesViewModel.MobileReferringSitesList));

            _refreshView = new RefreshView
            {
                AutomationId = ReferringSitesPageAutomationIds.RefreshView,
                CommandParameter = (repository.OwnerLogin, repository.Name),
                Content = collectionView
            };
            _refreshView.SetDynamicResource(RefreshView.RefreshColorProperty, nameof(BaseTheme.PullToRefreshColor));
            _refreshView.SetBinding(RefreshView.CommandProperty, nameof(ReferringSitesViewModel.RefreshCommand));
            _refreshView.SetBinding(RefreshView.IsRefreshingProperty, nameof(ReferringSitesViewModel.IsRefreshing));

            //Add Title and Close Button to UIModalPresentationStyle.FormSheet 
            if (Device.RuntimePlatform is Device.iOS)
            {
                var closeButton = new Button
                {
                    AutomationId = ReferringSitesPageAutomationIds.CloseButton,
                    Text = "Close",
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Center,
                    HeightRequest = titleRowHeight * 3 / 5,
                    Padding = new Thickness(5, 0)
                };
                closeButton.Clicked += HandleCloseButtonClicked;
                closeButton.SetDynamicResource(Button.TextColorProperty, nameof(BaseTheme.NavigationBarTextColor));
                closeButton.SetDynamicResource(Button.BorderColorProperty, nameof(BaseTheme.SettingsButtonBorderColor));
                closeButton.SetDynamicResource(BackgroundColorProperty, nameof(BaseTheme.NavigationBarBackgroundColor));


                var titleRowBlurView = new BoxView { Opacity = 0.5 };
                titleRowBlurView.SetDynamicResource(BackgroundColorProperty, nameof(BaseTheme.PageBackgroundColor));

                var titleLabel = new Label
                {
                    FontSize = 30,
                    FontAttributes = FontAttributes.Bold,
                    Text = PageTitles.ReferringSitesPage,
                };
                titleLabel.SetDynamicResource(Label.TextColorProperty, nameof(BaseTheme.TextColor));

                closeButton.Margin = titleLabel.Margin = new Thickness(0, titleTopMargin, 0, 0);

                var relativeLayout = new RelativeLayout();

                relativeLayout.Children.Add(_refreshView,
                                             Constraint.Constant(0),
                                             Constraint.Constant(titleRowHeight + 5), ////Set to `0` following this bug fix: https://github.com/xamarin/Xamarin.Forms/issues/9879
                                             Constraint.RelativeToParent(parent => parent.Width),
                                             Constraint.RelativeToParent(parent => parent.Height));

                relativeLayout.Children.Add(titleRowBlurView,
                                            Constraint.Constant(0),
                                            Constraint.Constant(0),
                                            Constraint.RelativeToParent(parent => parent.Width),
                                            Constraint.Constant(titleRowHeight));

                relativeLayout.Children.Add(titleLabel,
                                            Constraint.Constant(10),
                                            Constraint.Constant(0));

                relativeLayout.Children.Add(closeButton,
                                            Constraint.RelativeToParent(parent => parent.Width - GetWidth(parent, closeButton) - 10),
                                            Constraint.Constant(0),
                                            Constraint.RelativeToParent(parent => GetWidth(parent, closeButton)));

                Content = relativeLayout;
            }
            else
            {
                Content = _refreshView;
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (_refreshView.Content is CollectionView collectionView && IsNullOrEmpty(collectionView.ItemsSource))
                _refreshView.IsRefreshing = true;

            static bool IsNullOrEmpty(in IEnumerable? enumerable) => !enumerable?.GetEnumerator().MoveNext() ?? true;
        }

        async void HandleCollectionViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var collectionView = (CollectionView)sender;
            collectionView.SelectedItem = null;

            if (e?.CurrentSelection.FirstOrDefault() is ReferringSiteModel referingSite
                && referingSite.IsReferrerUriValid
                && referingSite.ReferrerUri != null)
            {
                AnalyticsService.Track("Referring Site Tapped", new Dictionary<string, string>
                {
                    { nameof(ReferringSiteModel.Referrer), referingSite.Referrer },
                    { nameof(ReferringSiteModel.ReferrerUri), referingSite.ReferrerUri.ToString() }
                });

                await _deepLinkingService.OpenBrowser(referingSite.ReferrerUri);
            }
        }

        async void HandleCloseButtonClicked(object sender, EventArgs e) => await Navigation.PopModalAsync();
    }
}

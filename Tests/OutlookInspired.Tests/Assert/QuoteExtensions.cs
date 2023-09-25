﻿using System.Reactive;
using System.Reactive.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.XtraLayout;
using OutlookInspired.Module.BusinessObjects;
using OutlookInspired.Tests.Common;
using XAF.Testing.RX;
using XAF.Testing.XAF;

namespace OutlookInspired.Tests.Assert{
    static class QuoteExtensions{
        public static IObservable<Frame> AssertOpportunitiesView(this XafApplication application, string navigationView, string viewVariant) 
            => application.AssertNavigationItems((action, item) => action.AssertNavigationItems(item))
                .If(action => action.CanNavigate(navigationView), action => action.AssertOpportunitiesView(navigationView, viewVariant));

        private static IObservable<Frame> AssertOpportunitiesView(this SingleChoiceAction action, string navigationView, string viewVariant){
            // return application.AssertNavigation(navigationView).AssertChangeViewVariant(viewVariant)
            // .AssertMapItAction(typeof(Quote));
            return action.Application.AssertDashboardListView(navigationView, viewVariant,
                    listViewFrameSelector: item => item.MasterViewItem())
                .FilterListViews(action.Application).DelayOnContext().Select(frame => frame)
                .AssertMapItAction(typeof(Quote))
                .AssertFilterAction(filtersCount:5)
                .CloseWindow()
                .ConcatDefer(() => action.Application.AssertDashboardListView(navigationView, viewVariant,
                    listViewFrameSelector: item => !item.MasterViewItem(), assert: frame => frame.AssertAction()));
        }
        
        public static IObservable<Unit> AssertNestedQuote(this IObservable<TabbedGroup> source,Frame nestedFrame,int tabIndex) 
            => source.AssertNestedListView(nestedFrame, typeof(Quote),tabIndex,AssertNestedQuoteItem,frame =>frame.AssertAction(nestedFrame) );

        public static IObservable<Unit> AssertNestedQuoteItem(this Frame nestedFrame) 
            => nestedFrame.AssertNestedListView(typeof(QuoteItem),assert:frame => frame.AssertAction(nestedFrame)).ToUnit();
    }
}
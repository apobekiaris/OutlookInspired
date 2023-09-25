﻿using System.Linq.Expressions;
using System.Reactive.Linq;
using DevExpress.ExpressApp;
using OutlookInspired.Module.BusinessObjects;
using OutlookInspired.Module.Services;
using XAF.Testing.RX;
using XAF.Testing.XAF;

namespace OutlookInspired.Tests.Common{
    static class FilterListView{

        internal static IObservable<Frame> FilterListViews(this IObservable<Frame> source, XafApplication application)
            => source.Merge(application.FilterAllListViews(source));
        
        static IObservable<Frame> FilterAllListViews(this XafApplication application,IObservable<Frame> assert) 
            => application.FilterListViews((view, expression) => view.FilterUserControl( expression).ToObservable(),Expressions())
                .IgnoreElements().TakeUntilCompleted(assert).To<Frame>();

        public static LambdaExpression[] Expressions()
            => new LambdaExpression[]{
                Customers(), 
                CustomerEmployees(), 
                Quotes(), 
                CustomerStores(),
                Orders(),
                Employees(),
                Tasks()
            };
        private static Expression<Func<Customer, bool>> Customers() 
            => customer => customer.Employees.Any() && customer.Orders.Any() && customer.Quotes.Any() && customer.CustomerStores.Any();
        
        private static Expression<Func<Employee, bool>> Employees() 
            => employee => employee.AssignedTasks.Any()&&employee.Evaluations.Any();
        
        private static Expression<Func<EmployeeTask, bool>> Tasks() 
            => employeeTask => employeeTask.AttachedFiles.Any()&&employeeTask.AssignedEmployees.Any();

        private static Expression<Func<CustomerStore, bool>> CustomerStores() 
            => store => store.CustomerEmployees.Any() && store.Orders.Any() && store.Quotes.Any();

        private static Expression<Func<Quote, bool>> Quotes() 
            => order => order.QuoteItems.Any();

        private static Expression<Func<Order, bool>> Orders() 
            => order => order.OrderItems.Any();

        private static Expression<Func<CustomerEmployee, bool>> CustomerEmployees() 
            => customerEmployee => customerEmployee.CustomerCommunications.Any();
        
    }
}
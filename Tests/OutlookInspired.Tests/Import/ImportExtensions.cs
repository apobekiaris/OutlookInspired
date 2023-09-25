﻿using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.BaseImpl.EF;
using Microsoft.EntityFrameworkCore;
using OutlookInspired.Module.BusinessObjects;
using XAF.Testing;
using XAF.Testing.RX;
using XAF.Testing.XAF;
using State = OutlookInspired.Module.BusinessObjects.State;
using StateEnum = DevExpress.DevAV.StateEnum;

namespace OutlookInspired.Tests.Import{
    public static class ImportExtensions{
        public static IObservable<Unit> ImportFromSqlLite(this IObjectSpace objectSpace) 
            => new DevAvDb($"Data Source={AppDomain.CurrentDomain.DBPath()}").Use(objectSpace.ImportFrom);
        
        static IObservable<Unit> CommitAndConcat(this IObservable<Unit> source, IObjectSpace objectSpace, Func<IObservable<Unit>> nextSource)
            => source.DoOnComplete(objectSpace.CommitChanges).ConcatDefer(nextSource);

        static IObservable<Unit> ImportFrom(this IObjectSpace objectSpace, DevAvDb sqliteContext) 
            => objectSpace.ZeroDependencies(sqliteContext)
                .CommitAndConcat(objectSpace, () => objectSpace.ImportCustomerStore(sqliteContext)
                    .Merge(objectSpace.ImportEmployee(sqliteContext)
                        .CommitAndConcat(objectSpace, () => objectSpace.EmployeeDependent(sqliteContext, objectSpace.ProductDependent(sqliteContext))))
                    .CommitAndConcat(objectSpace, () => objectSpace.EmployeeStoreDependent(sqliteContext, objectSpace.CustomerEmployeeDependent(sqliteContext))))
                .Finally(objectSpace.CommitChanges)
            ;

        private static IObservable<Unit> EmployeeStoreDependent(this IObjectSpace objectSpace, DevAvDb sqliteContext, IObservable<Unit> customerEmployeeDependent)
            => objectSpace.ImportCustomerEmployee(sqliteContext)
                .CommitAndConcat(objectSpace, () => customerEmployeeDependent);

        private static IObservable<Unit> CustomerEmployeeDependent(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => objectSpace.ImportCustomerCommunication(sqliteContext)
                .Merge(objectSpace.ImportEmployeeTasks(sqliteContext)
                    .CommitAndConcat(objectSpace, () => objectSpace.ImportTaskAttachedFiles(sqliteContext)));

        private static IObservable<Unit> EmployeeDependent(this IObjectSpace objectSpace, DevAvDb sqliteContext,
            IObservable<Unit> productDependent)
            => objectSpace.ImportEvaluation(sqliteContext)
                .Merge(objectSpace.ImportProduct(sqliteContext).CommitAndConcat(objectSpace, () => productDependent))
                .Merge(objectSpace.ImportOrder(sqliteContext)
                    .CommitAndConcat(objectSpace, () => objectSpace.ImportOrderItem(sqliteContext)))
                .Merge(objectSpace.ImportQuote(sqliteContext)
                    .CommitAndConcat(objectSpace, () => objectSpace.ImportQuoteItem(sqliteContext)));

        private static IObservable<Unit> ProductDependent(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => objectSpace.ImportProductImages(sqliteContext).Merge(objectSpace.ImportProductCatalog(sqliteContext));

        private static IObservable<Unit> ZeroDependencies(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => objectSpace.ImportCrest(sqliteContext)
                .Merge(objectSpace.ImportState(sqliteContext))
                .Merge(objectSpace.ImportCustomer(sqliteContext))
                .Merge(objectSpace.ImportPicture(sqliteContext))
                .Merge(objectSpace.ImportProbation(sqliteContext));

        static IObservable<Unit> ImportEmployee(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.Employees.Include(employee => employee.Picture)
                .Include(employee => employee.ProbationReason).ToNowObservable()
                .Select(sqlLite => {
                    var employee = objectSpace.CreateObject<Employee>();
                    employee.IdInt64 = sqlLite.Id;
                    employee.City = sqlLite.AddressCity;
                    employee.Address = sqlLite.AddressLine;
                    employee.ZipCode = sqlLite.AddressZipCode;
                    employee.Picture = objectSpace.FindSqlLiteObject<Picture>(sqlLite.Picture?.Id);
                    employee.State = Enum.Parse<OutlookInspired.Module.BusinessObjects.StateEnum>(sqlLite.AddressState.ToString());
                    employee.Department = (EmployeeDepartment)sqlLite.Department;
                    employee.Email = sqlLite.Email;
                    employee.Prefix = (PersonPrefix)sqlLite.Prefix;
                    employee.Skype = sqlLite.Skype;
                    employee.Status = (EmployeeStatus)sqlLite.Status;
                    employee.Title = sqlLite.Title;
                    employee.AddressLatitude = sqlLite.AddressLatitude;
                    employee.AddressLongitude = sqlLite.AddressLongitude;
                    employee.BirthDate = sqlLite.BirthDate;
                    employee.FirstName = sqlLite.FirstName;
                    employee.FullName = sqlLite.FullName;
                    employee.HireDate = sqlLite.HireDate;
                    employee.HomePhone = sqlLite.HomePhone;
                    employee.LastName = sqlLite.LastName;
                    // employee.UserName = employee.FirstName.ToLower().Concat(employee.LastName.ToLower().Take(1)).StringJoin("");
                    employee.MobilePhone = sqlLite.MobilePhone;
                    employee.PersonalProfile = sqlLite.PersonalProfile;
                    employee.ProbationReason = objectSpace.FindSqlLiteObject<Probation>(sqlLite.ProbationReason?.Id);
                    return employee;
                }).ToUnit();


        private static T FindSqlLiteObject<T>(this IObjectSpace objectSpace, long? id) where T : OutlookInspiredBaseObject{
            var baseObject = objectSpace.FindObject<T>(migrationBaseObject => id == migrationBaseObject.IdInt64);
            if (id.HasValue && baseObject == null){
                throw new NotImplementedException(typeof(T).Name);
            }
            return baseObject;
        }

        static IObservable<Unit> ImportEvaluation(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.Evaluations.Include(evaluation => evaluation.Employee)
                .Include(evaluation => evaluation.CreatedBy).ToNowObservable()
                .Select(sqlLite => {
                    var evaluation = objectSpace.CreateObject<Evaluation>();
                    evaluation.IdInt64 = sqlLite.Id;
                    evaluation.Subject = sqlLite.Subject;
                    evaluation.Employee = objectSpace.FindSqlLiteObject<Employee>(sqlLite.Employee?.Id);
                    evaluation.Manager = objectSpace.FindSqlLiteObject<Employee>(sqlLite.CreatedBy.Id);
                    evaluation.StartOn = sqlLite.CreatedOn;
                    evaluation.EndOn = sqlLite.CreatedOn.AddHours(1);
                    evaluation.Rating = (EvaluationRating)sqlLite.Rating;
                    evaluation.Description = sqlLite.Details;
                    var description = Regex.Replace(evaluation.Description, $@"Raise:\s*{Raise.Yes}", "");
                    if (!description.Equals(evaluation.Description)){
                        evaluation.Raise=Raise.Yes;
                    }
                    description = Regex.Replace(evaluation.Description, $@"Bonus:\s*{Bonus.Yes}", "");
                    if (!description.Equals(evaluation.Description)){
                        evaluation.Bonus=Bonus.Yes;
                    }
                    return evaluation;
                })
                .ToUnit();

        static IObservable<Unit> ImportOrder(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.Orders.Include(order => order.Employee).Include(order => order.Customer)
                .Include(order => order.Store).ToNowObservable()
                .Select(sqlLite => {
                    var order = objectSpace.CreateObject<Order>();
                    order.IdInt64 = sqlLite.Id;
                    order.Employee = objectSpace.FindSqlLiteObject<Employee>(sqlLite.Employee.Id);
                    order.Customer = objectSpace.FindSqlLiteObject<Customer>(sqlLite.Customer.Id);
                    order.PaymentTotal = sqlLite.PaymentTotal;
                    order.ShippingAmount = sqlLite.ShippingAmount;
                    order.RefundTotal = sqlLite.RefundTotal;
                    order.TotalAmount = sqlLite.TotalAmount;
                    order.Comments = sqlLite.Comments;
                    order.Store = objectSpace.FindSqlLiteObject<CustomerStore>(sqlLite.Store.Id);
                    order.InvoiceNumber = sqlLite.InvoiceNumber;
                    order.OrderDate = sqlLite.OrderDate;
                    order.OrderTerms = sqlLite.OrderTerms;
                    order.SaleAmount = sqlLite.SaleAmount;
                    order.ShipDate = sqlLite.ShipDate;
                    order.ShipmentCourier = (ShipmentCourier)sqlLite.ShipmentCourier;
                    order.ShipmentStatus = (ShipmentStatus)sqlLite.ShipmentStatus;
                    order.PONumber = sqlLite.PONumber;
                    return order;
                }).ToUnit();

        static IObservable<Unit> ImportProduct(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.Products.Include(product => product.Engineer).Include(product => product.Support)
                .Include(product => product.PrimaryImage).ToNowObservable()
                .Select(sqlLite => {
                    var product = objectSpace.CreateObject<Product>();
                    product.IdInt64 = sqlLite.Id;
                    product.Category = (ProductCategory)sqlLite.Category;
                    product.Name = sqlLite.Name;
                    product.Description = sqlLite.Description;
                    product.Available = sqlLite.Available;
                    product.Backorder = sqlLite.Backorder;
                    product.Cost = sqlLite.Cost;
                    product.Engineer = objectSpace.FindSqlLiteObject<Employee>(sqlLite.Engineer.Id);
                    product.Image = sqlLite.Image;
                    product.Manufacturing = sqlLite.Manufacturing;
                    product.Support = objectSpace.FindSqlLiteObject<Employee>(sqlLite.Support.Id);
                    product.Weight = sqlLite.Weight;
                    product.ConsumerRating = sqlLite.ConsumerRating;
                    product.CurrentInventory = sqlLite.CurrentInventory;
                    product.PrimaryImage = objectSpace.FindSqlLiteObject<Picture>(sqlLite.PrimaryImage.Id);
                    product.ProductionStart = sqlLite.ProductionStart;
                    product.RetailPrice = sqlLite.RetailPrice;
                    product.SalePrice = sqlLite.SalePrice;
                    return product;
                }).ToUnit();

        static IObservable<Unit> ImportProductImages(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.ProductImages.Include(productImage => productImage.Product)
                .Include(productImage => productImage.Picture).ToNowObservable()
                .Select(sqlLite => {
                    var productImage = objectSpace.CreateObject<ProductImage>();
                    productImage.IdInt64 = sqlLite.Id;
                    productImage.Product = objectSpace.FindSqlLiteObject<Product>(sqlLite.Product.Id);
                    productImage.Picture = objectSpace.FindSqlLiteObject<Picture>(sqlLite.Picture.Id);
                    return productImage;
                }).ToUnit();

        static IObservable<Unit> ImportProductCatalog(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.ProductCatalogs.Include(productCatalog => productCatalog.Product).ToNowObservable()
                .Select(sqlLite => {
                    var productCatalog = objectSpace.CreateObject<ProductCatalog>();
                    productCatalog.IdInt64 = sqlLite.Id;
                    productCatalog.Product = objectSpace.FindSqlLiteObject<Product>(sqlLite.Product.Id);
                    productCatalog.PDF = sqlLite.PDF;
                    return productCatalog;
                }).ToUnit();

        static IObservable<Unit> ImportOrderItem(this IObjectSpace objectSpace, DevAvDb sqliteContext){
            var products = objectSpace.GetObjects<Product>()
                .ToDictionary(product => product.IdInt64, product => product);
            var orders = objectSpace.GetObjects<Order>().ToDictionary(order => order.IdInt64, order => order);
            return sqliteContext.OrderItems.Include(orderItem => orderItem.Order)
                .Include(orderItem => orderItem.Product).ToNowObservable()
                .Select(sqlLite => {
                    var orderItem = objectSpace.CreateObject<OrderItem>();
                    orderItem.IdInt64 = sqlLite.Id;
                    orderItem.ProductPrice = sqlLite.ProductPrice;
                    orderItem.Discount = sqlLite.Discount;
                    orderItem.Order = orders[sqlLite.Order.Id];
                    orderItem.Product = products[sqlLite.Product.Id];
                    orderItem.Total = sqlLite.Total;
                    orderItem.ProductUnits = sqlLite.ProductUnits;
                    return orderItem;
                }).ToUnit();
        }

        static IObservable<Unit> ImportQuote(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.Quotes.Include(quote => quote.CustomerStore).Include(quote => quote.Employee)
                .Include(quote => quote.Customer).ToNowObservable()
                .Select(sqlLite => {
                    var quote = objectSpace.CreateObject<Quote>();
                    quote.IdInt64 = sqlLite.Id;
                    quote.CustomerStore = objectSpace.FindSqlLiteObject<CustomerStore>(sqlLite.CustomerStore.Id);
                    quote.Total = sqlLite.Total;
                    quote.Employee = objectSpace.FindSqlLiteObject<Employee>(sqlLite.Employee.Id);
                    quote.ShippingAmount = sqlLite.ShippingAmount;
                    quote.Date = sqlLite.Date;
                    quote.Customer = objectSpace.FindSqlLiteObject<Customer>(sqlLite.Customer.Id);
                    quote.Number = sqlLite.Number;
                    quote.Opportunity = sqlLite.Opportunity;
                    quote.SubTotal = sqlLite.SubTotal;
                    return quote;
                }).ToUnit();

        static IObservable<Unit> ImportQuoteItem(this IObjectSpace objectSpace, DevAvDb sqliteContext){
            var quotes = objectSpace.GetObjects<Quote>().ToDictionary(quote => quote.IdInt64, quote => quote);
            var products = objectSpace.GetObjects<Product>()
                .ToDictionary(product => product.IdInt64, product => product);
            return sqliteContext.QuoteItems.Include(quoteItem => quoteItem.Quote)
                .Include(quoteItem => quoteItem.Product).ToNowObservable()
                .Select(sqlLite => {
                    var quoteItem = objectSpace.CreateObject<QuoteItem>();
                    quoteItem.IdInt64 = sqlLite.Id;
                    quoteItem.Total = sqlLite.Total;
                    quoteItem.Quote = quotes[sqlLite.Quote.Id];
                    quoteItem.Discount = sqlLite.Discount;
                    quoteItem.ProductUnits = sqlLite.ProductUnits;
                    quoteItem.Product = products[sqlLite.Product.Id];
                    quoteItem.ProductPrice = sqlLite.ProductPrice;
                    return quoteItem;
                }).ToUnit();
        }

        static IObservable<Unit> ImportCustomerEmployee(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.CustomerEmployees.Include(customerEmployee => customerEmployee.Picture)
                .Include(customerEmployee => customerEmployee.Customer)
                .Include(customerEmployee => customerEmployee.CustomerStore).ToNowObservable()
                .Select(sqlLite => {
                    var customerEmployee = objectSpace.CreateObject<CustomerEmployee>();
                    customerEmployee.IdInt64 = sqlLite.Id;
                    customerEmployee.Prefix = (PersonPrefix)sqlLite.Prefix;
                    customerEmployee.FirstName = sqlLite.FirstName;
                    customerEmployee.FullName = sqlLite.FullName;
                    customerEmployee.Picture = objectSpace.FindSqlLiteObject<Picture>(sqlLite.Picture.Id);
                    customerEmployee.Email = sqlLite.Email;
                    customerEmployee.LastName = sqlLite.LastName;
                    customerEmployee.Customer = objectSpace.FindSqlLiteObject<Customer>(sqlLite.Customer.Id);
                    customerEmployee.Position = sqlLite.Position;
                    customerEmployee.CustomerStore =
                        objectSpace.FindSqlLiteObject<CustomerStore>(sqlLite.CustomerStore.Id);
                    customerEmployee.MobilePhone = sqlLite.MobilePhone;
                    customerEmployee.IsPurchaseAuthority = sqlLite.IsPurchaseAuthority;
                    return customerEmployee;
                }).ToUnit();

        static IObservable<Unit> ImportCustomerCommunication(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.CustomerCommunications.Include(customerCommunication => customerCommunication.Employee)
                .Include(customerCommunication => customerCommunication.CustomerEmployee).ToNowObservable()
                .Select(sqlLite => {
                    var customerCommunication = objectSpace.CreateObject<CustomerCommunication>();
                    customerCommunication.IdInt64 = sqlLite.Id;
                    customerCommunication.Employee = objectSpace.FindSqlLiteObject<Employee>(sqlLite.Employee.Id);
                    customerCommunication.CustomerEmployee = objectSpace.FindSqlLiteObject<CustomerEmployee>(sqlLite.CustomerEmployee.Id);
                    customerCommunication.Date = sqlLite.Date;
                    customerCommunication.Purpose = sqlLite.Purpose;
                    customerCommunication.Type = sqlLite.Type;
                    return customerCommunication;
                }).ToUnit();

        static IObservable<Unit> ImportProbation(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.Probations.ToNowObservable()
                .Select(sqlLite => {
                    var probation = objectSpace.CreateObject<Probation>();
                    probation.IdInt64 = sqlLite.Id;
                    probation.Reason = sqlLite.Reason;
                    return probation;
                }).ToUnit();

        static IObservable<Unit> ImportPicture(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.Pictures.ToNowObservable()
                .Select(sqlLite => {
                    var picture = objectSpace.CreateObject<Picture>();
                    picture.IdInt64 = sqlLite.Id;
                    picture.Data = sqlLite.Data;
                    return picture;
                })
                .ToUnit();
        
        static IObservable<Unit> ImportEmployeeTasks(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.EmployeeTasks.Include(employeeTask => employeeTask.CustomerEmployee)
                .Include(employeeTask => employeeTask.Owner).Include(employeeTask => employeeTask.AssignedEmployee)
                .Include(employeeTask => employeeTask.AssignedEmployees).ToNowObservable()
                .SelectMany(sqlLite => {
                    var task = objectSpace.CreateObject<EmployeeTask>();
                    task.IdInt64 = sqlLite.Id;
                    task.CustomerEmployee = objectSpace.FindSqlLiteObject<CustomerEmployee>(sqlLite.CustomerEmployee?.Id);
                    task.Owner = objectSpace.FindSqlLiteObject<Employee>(sqlLite.Owner?.Id);
                    task.AssignedEmployee = objectSpace.FindSqlLiteObject<Employee>(sqlLite.AssignedEmployee?.Id);
                    task.Status = (EmployeeTaskStatus)sqlLite.Status;
                    task.Category = sqlLite.Category;
                    task.Completion = sqlLite.Completion;
                    task.Description = sqlLite.Description;
                    task.Predecessors = sqlLite.Predecessors;
                    task.Priority = (EmployeeTaskPriority)sqlLite.Priority;
                    task.Private = sqlLite.Private;
                    task.Reminder = sqlLite.Reminder;
                    task.Subject = sqlLite.Subject;
                    task.DueDate = sqlLite.DueDate;
                    task.FollowUp = (EmployeeTaskFollowUp)sqlLite.FollowUp;
                    task.ParentId = sqlLite.ParentId;
                    task.StartDate = sqlLite.StartDate;
                    task.AttachedCollectionsChanged = sqlLite.AttachedCollectionsChanged;
                    task.ReminderDateTime = sqlLite.ReminderDateTime;
                    task.RtfTextDescription = sqlLite.RtfTextDescription;
                    return sqlLite.AssignedEmployees
                        .Do(employee => task.AssignedEmployees.Add(objectSpace.FindSqlLiteObject<Employee>(employee.Id)))
                        .To(task).ToNowObservable();
                }).ToUnit();

        static IObservable<Unit> ImportTaskAttachedFiles(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.TaskAttachedFiles.Include(taskAttachedFile => taskAttachedFile.EmployeeTask).ToNowObservable()
                .Select(sqlLite => {
                    var taskAttachedFile = objectSpace.CreateObject<TaskAttachedFile>();
                    taskAttachedFile.IdInt64 = sqlLite.Id;
                    var fileData = objectSpace.CreateObject<FileData>();
                    fileData.FileName = sqlLite.Name;
                    fileData.Content = sqlLite.Content;
                    taskAttachedFile.File=fileData;
                    taskAttachedFile.EmployeeTask = objectSpace.FindSqlLiteObject<EmployeeTask>(sqlLite.EmployeeTask.Id);
                    return taskAttachedFile;
                }).ToUnit();

        static IObservable<Unit> ImportCustomer(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.Customers.ToNowObservable()
                .Select(sqlLite => {
                    var customer = objectSpace.CreateObject<Customer>();
                    customer.IdInt64 = sqlLite.Id;
                    customer.Logo = sqlLite.Logo;
                    customer.Profile = sqlLite.Profile;
                    customer.Fax = sqlLite.Fax;
                    customer.Name = sqlLite.Name;
                    customer.Phone = sqlLite.Phone;
                    customer.Status = (CustomerStatus)sqlLite.Status;
                    customer.Website = sqlLite.Website;
                    customer.AnnualRevenue = sqlLite.AnnualRevenue;
                    customer.TotalEmployees = sqlLite.TotalEmployees;
                    customer.TotalStores = sqlLite.TotalStores;
                    customer.BillingAddressCity = sqlLite.BillingAddressCity;
                    customer.BillingAddressLatitude = sqlLite.BillingAddressLatitude;
                    customer.BillingAddressLine = sqlLite.BillingAddressLine;
                    customer.BillingAddressLongitude = sqlLite.BillingAddressLongitude;
                    customer.BillingAddressState = Enum.Parse<OutlookInspired.Module.BusinessObjects.StateEnum>(sqlLite.BillingAddressState.ToString());
                    customer.BillingAddressZipCode = sqlLite.BillingAddressZipCode;
                    customer.HomeOfficeCity = sqlLite.HomeOfficeCity;
                    customer.HomeOfficeLatitude = sqlLite.HomeOfficeLatitude;
                    customer.HomeOfficeLine = sqlLite.HomeOfficeLine;
                    customer.HomeOfficeLongitude = sqlLite.HomeOfficeLongitude;
                    customer.HomeOfficeState = Enum.Parse<OutlookInspired.Module.BusinessObjects.StateEnum>(sqlLite.HomeOfficeState.ToString());
                    customer.HomeOfficeZipCode = sqlLite.HomeOfficeZipCode;
                    return customer;
                }).ToUnit();

        static IObservable<Unit> ImportCustomerStore(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.CustomerStores.Include(customerStore => customerStore.Customer)
                .Include(customerStore => customerStore.Crest).ToNowObservable()
                .Select(sqlLite => {
                    var store = objectSpace.CreateObject<CustomerStore>();
                    store.IdInt64 = sqlLite.Id;
                    store.Customer = objectSpace.FindSqlLiteObject<Customer>(sqlLite.Customer.Id);
                    store.Crest = objectSpace.FindSqlLiteObject<Crest>(sqlLite.Crest.Id);
                    store.Phone = sqlLite.Phone;
                    store.Fax = sqlLite.Fax;
                    store.Location = sqlLite.Location;
                    store.City = sqlLite.Address_City;
                    store.Latitude = sqlLite.Address_Latitude;
                    store.Longitude = sqlLite.Address_Longitude;
                    store.State = Enum.Parse<OutlookInspired.Module.BusinessObjects.StateEnum>(sqlLite.Address_State.ToString());
                    store.Line = sqlLite.AddressLine;
                    store.ZipCode = sqlLite.Address_ZipCode;
                    store.AnnualSales = sqlLite.AnnualSales;
                    store.SquereFootage = sqlLite.SquereFootage;
                    store.TotalEmployees = sqlLite.TotalEmployees;
                    store.ZipCode = sqlLite.Address_ZipCode;
                    return store;
                }).ToUnit();

        static IObservable<Unit> ImportState(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.States.ToNowObservable()
                .Select(sqlLite => {
                    var state = objectSpace.CreateObject<State>();
                    state.IdInt64 = ((long)Enum.Parse<StateEnum>(sqlLite.ShortName.ToString()));
                    state.ShortName = Enum.Parse<OutlookInspired.Module.BusinessObjects.StateEnum>(sqlLite.ShortName.ToString());
                    state.LargeFlag = sqlLite.Flag48px;
                    state.SmallFlag = sqlLite.Flag24px;
                    state.LongName = sqlLite.LongName;
                    return state;
                }).ToUnit();

        static IObservable<Unit> ImportCrest(this IObjectSpace objectSpace, DevAvDb sqliteContext)
            => sqliteContext.Crests.ToNowObservable()
                .Select(sqlLite => {
                    var crest = objectSpace.CreateObject<Crest>();
                    crest.IdInt64 = sqlLite.Id;
                    crest.CityName = sqlLite.CityName;
                    crest.LargeImage = sqlLite.LargeImage;
                    crest.SmallImage = sqlLite.SmallImage;
                    return crest;
                }).ToUnit();

        public static void GenerateOrders(this IObjectSpace objectSpace,int factor=10) 
            => objectSpace.GetObjectsQuery<Order>().ToArray().SelectMany(order => 0.Range(factor)
                    .SelectMany(index => objectSpace.CreateObject<Order>()
                        .UpdateMembers(index,order).SelectMany(newOrder => order.OrderItems.ToArray()
                            .SelectMany(orderItem =>objectSpace.CreateObject<OrderItem>()
                                .UpdateMembers(newOrder, orderItem))))
                )
                .Finally(objectSpace.CommitChanges)
                .Enumerate();

        private static IEnumerable<IMemberInfo> UpdateMembers(this OrderItem newItem,Order newOrder,  OrderItem orderItem) 
            => newItem.ObjectSpace.CloneableOwnMembers(typeof(OrderItem))
                .Do(info => newItem.SetValue(info, orderItem)).Finally(() => newItem.Order=newOrder);

        private static IEnumerable<Order> UpdateMembers(this Order newOrder, int index, Order order) 
            => newOrder.ObjectSpace.CloneableOwnMembers(typeof(Order)).ToArray()
                .Do(info => newOrder.SetValue(info, order))
                .Finally(() => {
                    newOrder.OrderDate = order.OrderDate.AddDays(-1);
                    newOrder.InvoiceNumber = $"{index}{order.InvoiceNumber}";
                })
                .IgnoreElements().To(newOrder).Concat(newOrder.YieldItem());
        
        private static string DBPath(this AppDomain currentDomain){
            var path = $"{currentDomain.BaseDirectory}\\devav.sqlite3";
            typeof(ImportExtensions).Assembly.GetManifestResourceStream(s => s.EndsWith("devav.sqlite3")).Bytes().Save(path);
            return path;
        }

    }
}
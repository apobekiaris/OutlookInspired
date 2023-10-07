﻿using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Updating;
using DevExpress.Persistent.BaseImpl.EF;
using OutlookInspired.Module;
using OutlookInspired.Module.Features.Quotes;
using OutlookInspired.Win.Controllers;
using OutlookInspired.Win.Features;
using OutlookInspired.Win.Features.Customers;
using OutlookInspired.Win.Features.Employees;
using OutlookInspired.Win.Features.Evaluations;
using OutlookInspired.Win.Features.GridListEditor;
using OutlookInspired.Win.Features.Maps;
using OutlookInspired.Win.Features.Quotes;
using SplitterPositionController = OutlookInspired.Win.Controllers.SplitterPositionController;

namespace OutlookInspired.Win;

[ToolboxItemFilter("Xaf.Platform.Win")]
public sealed class OutlookInspiredWinModule : ModuleBase {
    private void Application_CreateCustomUserModelDifferenceStore(object sender, CreateCustomModelDifferenceStoreEventArgs e) {
        e.Store = new ModelDifferenceDbStore((XafApplication)sender, typeof(ModelDifference), false, "Win");
        e.Handled = true;
    }
    public OutlookInspiredWinModule() {
        FormattingProvider.UseMaskSettings = true;
        RequiredModuleTypes.Add(typeof(OutlookInspiredModule));
    }
    public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB) 
        => new[] { new ModuleUpdater(objectSpace, versionFromDB) };

    protected override IEnumerable<Type> GetDeclaredControllerTypes() 
        => new[]{
            typeof(RemoveMenuItemController), typeof(SchedulerResourceDeletingController),typeof(MapsViewController),
            typeof(FontSizeController), typeof(NewItemRowHandlingModeController),
            typeof(WinMapsController),typeof(PaletteEntriesController),
            typeof(RouteMapsViewController),typeof(Features.Orders.RouteMapsViewController), typeof(SalesMapsViewController),
            typeof(Features.Products.SalesMapsViewController),typeof(PropertyEditorController), typeof(DisableSkinsController), typeof(SplitterPositionController)
        };

    public override void Setup(XafApplication application) {
        base.Setup(application);
        // application.CreateCustomModelDifferenceStore += Application_CreateCustomModelDifferenceStore;
        application.CreateCustomUserModelDifferenceStore += Application_CreateCustomUserModelDifferenceStore;
    }
}

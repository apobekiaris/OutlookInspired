﻿using DevExpress.ExpressApp.Win;
using OutlookInspired.Module.BusinessObjects;
using XAF.Testing.XAF;
using ApplicationBuilder = OutlookInspired.Win.ApplicationBuilder;

namespace OutlookInspired.Tests.Common{
    public abstract class TestBase{
        public async Task<WinApplication> SetupWinApplication(Func<WinApplication, Task> beforeSetup = null,bool useServer=true,bool runInMainMonitor=false,bool useSecuredProvider=true){
            var connectionString = "Integrated Security=SSPI;Pooling=true;MultipleActiveResultSets=true;Data Source=(localdb)\\mssqllocaldb;Initial Catalog=OutlookInspired";
            var application = ApplicationBuilder.BuildApplication(useServer?null:connectionString,useSecuredProvider);
            application.DeleteModelDiffs(connectionString,nameof(OutlookInspiredEFCoreDbContext.ModelDifferences),nameof(OutlookInspiredEFCoreDbContext.ModelDifferenceAspects));
            application.SplashScreen = null;  
            if (beforeSetup != null){
                await beforeSetup(application);
            }
            application.Setup();
            application.ChangeStartupState(FormWindowState.Maximized,moveToInactiveMonitor:!runInMainMonitor);
            return application;
        }

        
    }
}
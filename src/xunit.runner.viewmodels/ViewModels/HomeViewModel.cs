﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Xunit.Runners.UI;


namespace Xunit.Runners
{
    public class HomeViewModel : ViewModelBase
    {
        readonly INavigation navigation;
        readonly ITestRunner runner;
        readonly DelegateCommand runEverythingCommand;

        public event EventHandler ScanComplete;
        readonly ManualResetEventSlim mre = new ManualResetEventSlim(false);
        bool isBusy;

        internal HomeViewModel(INavigation navigation, ITestRunner runner)
        {
            this.navigation = navigation;
            this.runner = runner;
            TestAssemblies = new ObservableCollection<TestAssemblyViewModel>();

            OptionsCommand = new DelegateCommand(OptionsExecute);
            CreditsCommand = new DelegateCommand(CreditsExecute);
            runEverythingCommand = new DelegateCommand(RunEverythingExecute, () => !isBusy);
            NavigateToTestAssemblyCommand = new DelegateCommand<object>(async vm => await navigation.NavigateTo(NavigationPage.AssemblyTestList, vm));




            StartAssemblyScan();
        }


        public ObservableCollection<TestAssemblyViewModel> TestAssemblies { get; }


        void OptionsExecute()
        {
            Debug.WriteLine("Options");
        }

        async void CreditsExecute()
        {
            await navigation.NavigateTo(NavigationPage.Credits);
        }

        async void RunEverythingExecute()
        {
            try
            {
                IsBusy = true;
                await Run();
            }
            finally
            {
                IsBusy = false;
            }
        }


        public ICommand OptionsCommand { get; private set; }
        public ICommand CreditsCommand { get; private set; }

        public ICommand RunEverythingCommand => runEverythingCommand;

        public ICommand NavigateToTestAssemblyCommand { get; private set; }

        public bool IsBusy
        {
            get { return isBusy; }
            private set
            {
                if (Set(ref isBusy, value))
                {
                    runEverythingCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public async void StartAssemblyScan()
        {
            IsBusy = true;
            try
            {
                var allTests = await runner.Discover();

                // Back on UI thread
                foreach (var vm in allTests)
                {
                    TestAssemblies.Add(vm);
                }

                var evt = ScanComplete;
                evt?.Invoke(this, EventArgs.Empty);

                mre.Set();

            }
            finally
            {
                IsBusy = false;
            }

            if (RunnerOptions.Current.AutoStart)
            {
                await Task.Run(() => mre.Wait());
                await Run();

                if (RunnerOptions.Current.TerminateAfterExecution)
                    PlatformHelpers.TerminateWithSuccess();
            }
        }

        Task Run()
        {
            return runner.Run(TestAssemblies.Select(t => t.RunInfo).ToList(), "Run Everything");
        }
    }
}

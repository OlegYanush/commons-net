using ReportPortal.Client.Requests;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReportPortal.Shared
{
    public interface IReportingClient
    {
        string StartLaunch(StartLaunchRequest request);
        string FinishLaunch(string launchId, FinishLaunchRequest request);
        string UpdateLaunch(string launchId, UpdateLaunchRequest request);

        string StartTest(string launchId, StartTestItemRequest request);
        string StartTest(string launchId, string parentTestId, StartTestItemRequest request);
        string FinishTest(string testId, FinishTestItemRequest request);
        string UpdateTest(string testId, UpdateTestItemRequest request);
        string LogMessage(string testId, AddLogItemRequest request);

        void WaitReporting();

        void StopReportingServer();
    }
}

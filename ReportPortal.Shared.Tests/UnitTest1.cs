using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReportPortal.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ReportPortal.Shared.Tests
{
    [TestClass]
    public class UnitTest1
    {
        Uri url = new Uri("https://rp.epam.com/api/v1/");
        string project = "default_project";
        string uuid = "7853c7a9-7f27-43ea-835a-cab01355fd17";

        [TestMethod]
        public void SimpleTestsTree()
        {
            ReportingServer.Start(url, project, uuid);
            var client = new ReportingClient();

            var sw = Stopwatch.StartNew();

            for (int i = 0; i < 1; i++)
            {
                var launchId = client.StartLaunch(new Client.Requests.StartLaunchRequest
                {
                    Name = "RemoteLaunch",
                    Mode = Client.Models.LaunchMode.Debug,
                    StartTime = DateTime.UtcNow
                });

                client.UpdateLaunch(launchId, new Client.Requests.UpdateLaunchRequest
                {
                    Description = "new desc",
                    Mode = Client.Models.LaunchMode.Debug
                });

                for (int j = 0; j < 2; j++)
                {
                    var suiteId = client.StartTest(launchId, new Client.Requests.StartTestItemRequest
                    {
                        Name = $"Suite {j}",
                        Description = "desc",
                        StartTime = DateTime.UtcNow,
                        Type = Client.Models.TestItemType.Suite
                    });

                    client.UpdateTest(suiteId, new Client.Requests.UpdateTestItemRequest
                    {
                        Description = "new desc"
                    });

                    for (int k = 0; k < 4; k++)
                    {
                        var testId = client.StartTest(launchId, suiteId, new Client.Requests.StartTestItemRequest
                        {
                            Name = $"Test {k}",
                            Description = "desc",
                            StartTime = DateTime.UtcNow,
                            Type = Client.Models.TestItemType.Step
                        });

                        for (int l = 0; l < 10; l++)
                        {
                            client.LogMessage(testId, new Client.Requests.AddLogItemRequest
                            {
                                Level = Client.Models.LogLevel.Debug,
                                Text = $"Message {l}",
                                Time = DateTime.UtcNow
                            });
                        }

                        client.FinishTest(testId, new Client.Requests.FinishTestItemRequest
                        {
                            EndTime = DateTime.UtcNow,
                            Status = Client.Models.Status.Passed
                        });
                    }

                    client.FinishTest(suiteId, new Client.Requests.FinishTestItemRequest
                    {
                        EndTime = DateTime.UtcNow,
                        Status = Client.Models.Status.Passed
                    });
                }

                client.FinishLaunch(launchId, new Client.Requests.FinishLaunchRequest
                {
                    EndTime = DateTime.UtcNow
                });

            }

            Console.WriteLine($"All requests are sent. Duration: {sw.Elapsed}");
            sw.Restart();
            Console.WriteLine("Waiting reporting...");
            client.WaitReporting();
            Console.WriteLine($"All requests proceeded. Sync time: {sw.Elapsed}");

            client.StopReportingServer();
        }
    }
}

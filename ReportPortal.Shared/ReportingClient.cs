using System;
using System.Collections.Generic;
using System.Text;
using ReportPortal.Client.Requests;
using System.IO.Pipes;
using System.IO;
using ReportPortal.Client.Converters;

namespace ReportPortal.Shared
{
    public class ReportingClient : IReportingClient
    {
        public string FinishLaunch(string launchTaskId, FinishLaunchRequest request)
        {
            var serverRequest = new ServerRequest
            {
                Action = ServerAction.FinishLaunch,
                ParentLaunchTaskId = Guid.Parse(launchTaskId),
                Body = ModelSerializer.Serialize<FinishLaunchRequest>(request)
            };

            var taskId = SendRemoteMessage(serverRequest);
            return taskId;
        }

        public string UpdateLaunch(string launchId, UpdateLaunchRequest request)
        {
            var serverRequest = new ServerRequest
            {
                Action = ServerAction.UpdateLaunch,
                ParentLaunchTaskId = Guid.Parse(launchId),
                Body = ModelSerializer.Serialize<UpdateLaunchRequest>(request)
            };

            var taskId = SendRemoteMessage(serverRequest);
            return taskId;
        }

        public string StartLaunch(StartLaunchRequest request)
        {
            var serverRequest = new ServerRequest
            {
                Action = ServerAction.StartLaunch,
                Body = ModelSerializer.Serialize<StartLaunchRequest>(request)
            };

            var taskId = SendRemoteMessage(serverRequest);
            return taskId;
        }

        public string StartTest(string launchId, StartTestItemRequest request)
        {
            var serverRequest = new ServerRequest
            {
                Action = ServerAction.StartTest,
                ParentLaunchTaskId = Guid.Parse(launchId),
                Body = ModelSerializer.Serialize<StartTestItemRequest>(request)
            };

            var taskId = SendRemoteMessage(serverRequest);
            return taskId;
        }

        public string StartTest(string launchId, string parentTestId, StartTestItemRequest request)
        {
            var serverRequest = new ServerRequest
            {
                Action = ServerAction.StartTest,
                ParentLaunchTaskId = Guid.Parse(launchId),
                ParentTestTaskId = Guid.Parse(parentTestId),
                Body = ModelSerializer.Serialize<StartTestItemRequest>(request)
            };

            var taskId = SendRemoteMessage(serverRequest);
            return taskId;
        }

        public string FinishTest(string testId, FinishTestItemRequest request)
        {
            var serverRequest = new ServerRequest
            {
                Action = ServerAction.FinishTest,
                ParentTestTaskId = Guid.Parse(testId),
                Body = ModelSerializer.Serialize<FinishTestItemRequest>(request)
            };

            var taskId = SendRemoteMessage(serverRequest);
            return taskId;
        }

        public string UpdateTest(string testId, UpdateTestItemRequest request)
        {
            var serverRequest = new ServerRequest
            {
                Action = ServerAction.UpdateTest,
                ParentTestTaskId = Guid.Parse(testId),
                Body = ModelSerializer.Serialize<UpdateTestItemRequest>(request)
            };

            var taskId = SendRemoteMessage(serverRequest);
            return taskId;
        }

        public string LogMessage(string testId, AddLogItemRequest request)
        {
            var serverRequest = new ServerRequest
            {
                Action = ServerAction.AddLog,
                ParentTestTaskId = Guid.Parse(testId),
                Body = ModelSerializer.Serialize<AddLogItemRequest>(request)
            };

            var taskId = SendRemoteMessage(serverRequest);
            return taskId;
        }

        private string SendRemoteMessage(ServerRequest request)
        {
            using (NamedPipeClientStream namedPipeClient = new NamedPipeClientStream("ReportPortalPipe"))
            {
                try
                {
                    namedPipeClient.Connect(20000);
                }
                catch (FileNotFoundException)
                {
                    System.Threading.Thread.Sleep(100);
                    namedPipeClient.Connect(20000);
                }
                namedPipeClient.ReadMode = PipeTransmissionMode.Message;

                var message = ModelSerializer.Serialize<ServerRequest>(request);
                byte[] messageBytes = Encoding.Default.GetBytes(message);
                namedPipeClient.Write(messageBytes, 0, messageBytes.Length);

                var result = ReadMessage(namedPipeClient);

                return Encoding.UTF8.GetString(result);
            }
        }

        private byte[] ReadMessage(PipeStream pipe)
        {
            byte[] buffer = new byte[10];
            using (var ms = new MemoryStream())
            {
                do
                {
                    var readBytes = pipe.Read(buffer, 0, buffer.Length);
                    ms.Write(buffer, 0, readBytes);
                }
                while (!pipe.IsMessageComplete);

                return ms.ToArray();
            }
        }

        public void WaitReporting()
        {
            var serverRequest = new ServerRequest
            {
                Action = ServerAction.WaitReporting
            };

            SendRemoteMessage(serverRequest);
        }

        public void StopReportingServer()
        {
            var serverRequest = new ServerRequest
            {
                Action = ServerAction.Exit
            };

            SendRemoteMessage(serverRequest);
        }
    }
}

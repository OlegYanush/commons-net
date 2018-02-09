using ReportPortal.Client;
using ReportPortal.Client.Converters;
using ReportPortal.Client.Requests;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReportPortal.Shared
{
    public class ReportingServer
    {
        private static Process _process;

        private static ConcurrentBag<ServerRequest> _requests = new ConcurrentBag<ServerRequest>();
        private static Task _requestsProcessorTask = Task.Run(() => { });
        private static Service _httpClient;

        public static void Main(string[] args)
        {
            Console.WriteLine("Loading extensions...");
            LoadExtensions();

            _httpClient = new Service(new Uri(args[0]), args[1], args[2]);

            Thread[] threads = new Thread[4];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(Listen);
                threads[i].Start();
            }

            while (true)
            {
                for (int i = 0; i < threads.Length; i++)
                {
                    if (threads[i].Join(250))
                    {
                        // thread is finished, starting a new thread
                        threads[i] = new Thread(Listen);
                        threads[i].Start();
                    }
                }
            }
        }

        private static List<IBridgeExtension> Extensions = new List<IBridgeExtension>();

        private static void LoadExtensions()
        {
            var currentDirectory = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            try
            {
                foreach (var file in currentDirectory.GetFiles("ReportPortal.*.dll"))
                {
                    AppDomain.CurrentDomain.Load(Path.GetFileNameWithoutExtension(file.Name));
                }

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.GetInterfaces().Contains(typeof(IBridgeExtension)))
                            {
                                var extension = Activator.CreateInstance(type);
                                Extensions.Add((IBridgeExtension)extension);
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            }
            catch(Exception)
            { }

            Extensions = Extensions.OrderBy(ext => ext.Order).ToList();
        }

        private static void Listen()
        {
            using (NamedPipeServerStream namedPipeServer = new NamedPipeServerStream("ReportPortalPipe", PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message))
            {
                namedPipeServer.WaitForConnection();

                var message = Encoding.UTF8.GetString(ReadMessage(namedPipeServer));
                var request = ModelSerializer.Deserialize<ServerRequest>(message);

                if (request.Action == ServerAction.Exit)
                {
                    Exit();
                }
                else if (request.Action == ServerAction.WaitReporting)
                {
                    Console.WriteLine($"Count of requests: {_requests.Count}");
                    _requestsProcessorTask.Wait();

                    // response
                    var response = Encoding.UTF8.GetBytes("tasks finished");
                    namedPipeServer.Write(response, 0, response.Length);
                }
                else
                {
                    var id = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());

                    request.TaskId = Guid.Parse(Encoding.UTF8.GetString(id));

                    _requests.Add(request);
                    _requestsProcessorTask = _requestsProcessorTask.ContinueWith((t) => Proceed(request));

                    // response
                    namedPipeServer.Write(id, 0, id.Length);
                }
            }
        }

        private static void Proceed(ServerRequest request)
        {
            try
            {
                switch (request.Action)
                {
                    case ServerAction.StartLaunch:
                        var startLaunchRequest = ModelSerializer.Deserialize<StartLaunchRequest>(request.Body);
                        request.ReportPortalItemId = _httpClient.StartLaunchAsync(startLaunchRequest).Result.Id;
                        break;

                    case ServerAction.FinishLaunch:
                        var finishLaunchRequest = ModelSerializer.Deserialize<FinishLaunchRequest>(request.Body);

                        var dependentRequest = _requests.First(r => r.TaskId == request.ParentLaunchTaskId);

                        _httpClient.FinishLaunchAsync(dependentRequest.ReportPortalItemId, finishLaunchRequest).Wait();
                        break;

                    case ServerAction.UpdateLaunch:
                        var updateLaunchRequest = ModelSerializer.Deserialize<UpdateLaunchRequest>(request.Body);

                        var dependentStartRequest = _requests.First(r => r.TaskId == request.ParentLaunchTaskId);

                        _httpClient.UpdateLaunchAsync(dependentStartRequest.ReportPortalItemId, updateLaunchRequest).Wait();
                        break;

                    case ServerAction.StartTest:
                        var startTestRequest = ModelSerializer.Deserialize<StartTestItemRequest>(request.Body);

                        startTestRequest.LaunchId = _requests.First(r => r.TaskId == request.ParentLaunchTaskId).ReportPortalItemId;

                        var dependentStartTestRequest = _requests.FirstOrDefault(r => r.TaskId == request.ParentTestTaskId);
                        if (dependentStartTestRequest == null)
                        {
                            request.ReportPortalItemId = _httpClient.StartTestItemAsync(startTestRequest).Result.Id;
                        }
                        else
                        {
                            request.ReportPortalItemId = _httpClient.StartTestItemAsync(dependentStartTestRequest.ReportPortalItemId, startTestRequest).Result.Id;
                        }

                        break;
                    case ServerAction.FinishTest:
                        var finishTestRequest = ModelSerializer.Deserialize<FinishTestItemRequest>(request.Body);

                        var dependentFinishTestRequest = _requests.First(r => r.TaskId == request.ParentTestTaskId);

                        _httpClient.FinishTestItemAsync(dependentFinishTestRequest.ReportPortalItemId, finishTestRequest).Wait();
                        break;

                    case ServerAction.UpdateTest:
                        var updateTestRequest = ModelSerializer.Deserialize<UpdateTestItemRequest>(request.Body);

                        dependentStartTestRequest = _requests.First(r => r.TaskId == request.ParentTestTaskId);

                        _httpClient.UpdateTestItemAsync(dependentStartTestRequest.ReportPortalItemId, updateTestRequest).Wait();
                        break;

                    case ServerAction.AddLog:
                        var logRequest = ModelSerializer.Deserialize<AddLogItemRequest>(request.Body);

                        var dependentTestRequest = _requests.FirstOrDefault(r => r.TaskId == request.ParentTestTaskId);

                        if (dependentTestRequest == null)
                        {
                            dependentTestRequest = _requests.LastOrDefault(r => r.Action == ServerAction.StartTest && _requests.Any(f => f.ParentTestTaskId == r.TaskId && f.Action == ServerAction.FinishTest) == false);
                        }

                        if (dependentTestRequest != null)
                        {
                            logRequest.TestItemId = dependentTestRequest.ReportPortalItemId;

                            _httpClient.AddLogItemAsync(logRequest).Wait();
                        }
                        break;
                }
            }
            catch (Exception exp)
            {
                File.AppendAllText("ReportPortal.Errors.log", exp.ToString() + Environment.NewLine);
                Console.WriteLine(exp.Message);
            }

        }

        private static byte[] ReadMessage(PipeStream pipe)
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

        public static void Start(Uri url, string project, string uuid)
        {
            var file = Assembly.GetExecutingAssembly().Location;
            var workingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            Console.WriteLine($"Executing: {file}");
            Console.WriteLine($"Working directory: {Environment.CurrentDirectory}");
            var processInfo = new ProcessStartInfo();

#if NET45
            processInfo.FileName = file;
            processInfo.Arguments = string.Join(" ", url, project, uuid);
#else
            processInfo.FileName = "dotnet";
            processInfo.Arguments = file + " " + string.Join(" ", url, project, uuid);
#endif
            processInfo.UseShellExecute = true;
            processInfo.WorkingDirectory = workingDirectory;
            _process = Process.Start(processInfo);
        }

        private static void Exit()
        {
            Process.GetCurrentProcess().Close();
            Process.GetCurrentProcess().Kill();
        }
    }
}
